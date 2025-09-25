using MasterEcommerce.Messages;
using System.Collections.Concurrent;

namespace MasterEcommerce.Services;

public interface IOrderOrchestrator
{
    Task HandlePaymentProcessedAsync(PaymentProcessedMessage message);
    Task HandleInventoryCheckedAsync(InventoryCheckedMessage message);
    void InitializeOrder(OrderCreatedMessage order);
    void StartListening();
    Task StartBackgroundProcessingAsync(CancellationToken cancellationToken);
}

public class OrderOrchestrator : IOrderOrchestrator
{
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<OrderOrchestrator> _logger;

    // Dicionário para armazenar o estado dos pedidos em processamento (thread-safe)
    private readonly ConcurrentDictionary<Guid, OrderProcessingState> _orderStates;
    
    // Configurações de timeout e retry
    private readonly TimeSpan _orderTimeout = TimeSpan.FromMinutes(5); // Timeout de 5 minutos
    private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1); // Retry a cada 1 minuto
    private readonly int _maxRetries = 3; // Máximo de 3 tentativas

    private readonly Timer _backgroundTimer;

    public OrderOrchestrator(IMessageBusService messageBus, ILogger<OrderOrchestrator> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
        _orderStates = new ConcurrentDictionary<Guid, OrderProcessingState>();
        
        // Timer para verificar pedidos pendentes a cada 30 segundos
        _backgroundTimer = new Timer(ProcessPendingOrders, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task HandlePaymentProcessedAsync(PaymentProcessedMessage message)
    {
        _logger.LogInformation("Handling payment result for order {OrderId}. Approved: {IsApproved}",
            message.OrderId, message.IsApproved);

        var state = _orderStates.GetOrAdd(message.OrderId, _ => new OrderProcessingState
        {
            OrderId = message.OrderId,
            CreatedAt = DateTime.UtcNow
        });

        lock (state)
        {
            state.PaymentProcessed = true;
            state.PaymentApproved = message.IsApproved;
            state.PaymentFailureReason = message.FailureReason;
            state.PaymentReceivedAt = DateTime.UtcNow;
        }

        await CheckOrderCompletionAsync(state);
    }

    public async Task HandleInventoryCheckedAsync(InventoryCheckedMessage message)
    {
        _logger.LogInformation("Handling inventory result for order {OrderId}. Available: {IsAvailable}",
            message.OrderId, message.IsAvailable);

        var state = _orderStates.GetOrAdd(message.OrderId, _ => new OrderProcessingState
        {
            OrderId = message.OrderId,
            CreatedAt = DateTime.UtcNow
        });

        lock (state)
        {
            state.InventoryChecked = true;
            state.InventoryAvailable = message.IsAvailable;
            state.InventoryFailureReason = message.FailureReason;
            state.InventoryReceivedAt = DateTime.UtcNow;
        }

        await CheckOrderCompletionAsync(state);
    }

    private async Task CheckOrderCompletionAsync(OrderProcessingState state)
    {
        bool shouldProcess = false;
        
        lock (state)
        {
            // Verifica se já foi processado
            if (state.IsCompleted)
            {
                return;
            }

            // Aguarda tanto o pagamento quanto o inventário serem processados
            if (!state.PaymentProcessed || !state.InventoryChecked)
            {
                _logger.LogInformation("Order {OrderId} still waiting for processing. Payment: {PaymentProcessed}, Inventory: {InventoryChecked}",
                    state.OrderId, state.PaymentProcessed, state.InventoryChecked);
                return;
            }

            // Marca como em processamento para evitar processamento duplo
            if (!state.IsProcessing)
            {
                state.IsProcessing = true;
                shouldProcess = true;
            }
        }

        if (shouldProcess)
        {
            try
            {
                // Verifica se ambos foram aprovados
                if (state.PaymentApproved && state.InventoryAvailable)
                {
                    await HandleOrderApprovedAsync(state);
                }
                else
                {
                    await HandleOrderRejectedAsync(state);
                }

                // Marca como concluído e remove o estado após processar
                lock (state)
                {
                    state.IsCompleted = true;
                }
                
                _orderStates.TryRemove(state.OrderId, out _);
                
                _logger.LogInformation("Order {OrderId} processing completed and removed from state", state.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}", state.OrderId);
                
                lock (state)
                {
                    state.IsProcessing = false;
                    state.RetryCount++;
                    state.LastRetryAt = DateTime.UtcNow;
                }
            }
        }
    }

    private async Task HandleOrderApprovedAsync(OrderProcessingState state)
    {
        _logger.LogInformation("Order {OrderId} approved - sending confirmation email and scheduling shipping",
            state.OrderId);

        // Envia email de confirmação
        var confirmationEmail = new SendEmailMessage
        {
            To = state.CustomerEmail ?? "",
            Subject = $"Pedido {state.OrderId} confirmado!",
            Body = $"Olá {state.CustomerName},\n\n" +
                   $"Seu pedido {state.OrderId} foi confirmado com sucesso!\n" +
                   $"Pagamento aprovado e itens reservados.\n" +
                   $"Em breve você receberá informações sobre o envio.\n\n" +
                   $"Total: R$ {state.TotalAmount:F2}",
            OrderId = state.OrderId,
            Type = EmailType.OrderConfirmed
        };

        _messageBus.Publish("email.send", confirmationEmail);

        // Agenda o envio
        var shippingMessage = new ScheduleShippingMessage
        {
            OrderId = state.OrderId,
            CustomerName = state.CustomerName ?? "",
            CustomerEmail = state.CustomerEmail ?? "",
            Items = state.Items ?? new List<OrderItemMessage>(),
            ScheduledAt = DateTime.UtcNow
        };

        _messageBus.Publish("shipping.schedule", shippingMessage);

        await Task.CompletedTask;
    }

    private async Task HandleOrderRejectedAsync(OrderProcessingState state)
    {
        var reasons = new List<string>();

        if (!state.PaymentApproved && !string.IsNullOrEmpty(state.PaymentFailureReason))
            reasons.Add($"Pagamento: {state.PaymentFailureReason}");

        if (!state.InventoryAvailable && !string.IsNullOrEmpty(state.InventoryFailureReason))
            reasons.Add($"Estoque: {state.InventoryFailureReason}");

        var failureReason = string.Join("; ", reasons);

        _logger.LogInformation("Order {OrderId} rejected. Reason: {FailureReason}",
            state.OrderId, failureReason);

        // Envia email de falha
        var failureEmail = new SendEmailMessage
        {
            To = state.CustomerEmail ?? "",
            Subject = $"Pedido {state.OrderId} não pôde ser processado",
            Body = $"Olá {state.CustomerName},\n\n" +
                   $"Infelizmente seu pedido {state.OrderId} não pôde ser processado.\n\n" +
                   $"Motivo(s): {failureReason}\n\n" +
                   $"Por favor, tente novamente ou entre em contato conosco.",
            OrderId = state.OrderId,
            Type = EmailType.OrderFailed
        };

        _messageBus.Publish("email.send", failureEmail);

        await Task.CompletedTask;
    }

    public void InitializeOrder(OrderCreatedMessage order)
    {
        var state = new OrderProcessingState
        {
            OrderId = order.OrderId,
            CustomerEmail = order.CustomerEmail,
            CustomerName = order.CustomerName,
            TotalAmount = order.TotalAmount,
            Items = order.Items,
            CreatedAt = DateTime.UtcNow
        };

        _orderStates[order.OrderId] = state;
        
        _logger.LogInformation("Order {OrderId} initialized and waiting for payment and inventory responses", order.OrderId);
    }

    public void StartListening()
    {
        _messageBus.Subscribe<PaymentProcessedMessage>("payment.processed", HandlePaymentProcessedAsync);
        _messageBus.Subscribe<InventoryCheckedMessage>("inventory.checked", HandleInventoryCheckedAsync);

        _logger.LogInformation("OrderOrchestrator started listening for payment.processed and inventory.checked messages");
    }

    public async Task StartBackgroundProcessingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting background processing for pending orders");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_retryInterval, cancellationToken);
                ProcessPendingOrders(null);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background processing");
            }
        }
    }

    private void ProcessPendingOrders(object? state)
    {
        var now = DateTime.UtcNow;
        var pendingOrders = _orderStates.Values.Where(os => !os.IsCompleted).ToList();

        foreach (var orderState in pendingOrders)
        {
            try
            {
                ProcessPendingOrder(orderState, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending order {OrderId}", orderState.OrderId);
            }
        }
    }

    private void ProcessPendingOrder(OrderProcessingState orderState, DateTime now)
    {
        lock (orderState)
        {
            if (orderState.IsCompleted || orderState.IsProcessing)
                return;

            var timeSinceCreation = now - orderState.CreatedAt;
            
            // Verifica timeout
            if (timeSinceCreation > _orderTimeout)
            {
                _logger.LogWarning("Order {OrderId} timed out after {Timeout}. Payment: {PaymentProcessed}, Inventory: {InventoryChecked}",
                    orderState.OrderId, _orderTimeout, orderState.PaymentProcessed, orderState.InventoryChecked);

                HandleTimeoutOrder(orderState);
                return;
            }

            // Verifica se precisa de retry para mensagens não recebidas
            var timeSinceLastRetry = orderState.LastRetryAt.HasValue ? now - orderState.LastRetryAt.Value : timeSinceCreation;
            
            if (timeSinceLastRetry > _retryInterval && orderState.RetryCount < _maxRetries)
            {
                HandleRetryOrder(orderState, now);
            }
        }
    }

    private void HandleTimeoutOrder(OrderProcessingState orderState)
    {
        _logger.LogWarning("Processing timeout order {OrderId}", orderState.OrderId);

        // Define razões de falha baseadas no que não foi recebido
        var reasons = new List<string>();
        
        if (!orderState.PaymentProcessed)
            reasons.Add("Timeout no processamento do pagamento");
        
        if (!orderState.InventoryChecked)
            reasons.Add("Timeout na verificação do estoque");

        // Simula uma resposta de falha para completar o processamento
        if (!orderState.PaymentProcessed)
        {
            orderState.PaymentProcessed = true;
            orderState.PaymentApproved = false;
            orderState.PaymentFailureReason = "Timeout no processamento";
        }

        if (!orderState.InventoryChecked)
        {
            orderState.InventoryChecked = true;
            orderState.InventoryAvailable = false;
            orderState.InventoryFailureReason = "Timeout na verificação";
        }

        // Agenda o processamento assíncrono
        Task.Run(async () =>
        {
            try
            {
                await CheckOrderCompletionAsync(orderState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timeout order {OrderId}", orderState.OrderId);
            }
        });
    }

    private void HandleRetryOrder(OrderProcessingState orderState, DateTime now)
    {
        orderState.RetryCount++;
        orderState.LastRetryAt = now;

        _logger.LogInformation("Retrying order {OrderId} (attempt {RetryCount}/{MaxRetries}). Payment: {PaymentProcessed}, Inventory: {InventoryChecked}",
            orderState.OrderId, orderState.RetryCount, _maxRetries, orderState.PaymentProcessed, orderState.InventoryChecked);

        // Reenviar mensagens para serviços que não responderam
        // Isso pode ser implementado se você tiver mensagens específicas para retry
        // Por enquanto, apenas logamos para monitoramento
        
        if (!orderState.PaymentProcessed)
        {
            _logger.LogWarning("Payment still pending for order {OrderId} after {RetryCount} retries", 
                orderState.OrderId, orderState.RetryCount);
        }
        
        if (!orderState.InventoryChecked)
        {
            _logger.LogWarning("Inventory check still pending for order {OrderId} after {RetryCount} retries", 
                orderState.OrderId, orderState.RetryCount);
        }
    }

    // Método para limpeza de recursos
    public void Dispose()
    {
        _backgroundTimer?.Dispose();
    }
}

public class OrderProcessingState
{
    public Guid OrderId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemMessage>? Items { get; set; }
    
    // Controle de estado
    public DateTime CreatedAt { get; set; }
    public bool IsProcessing { get; set; }
    public bool IsCompleted { get; set; }
    
    // Controle de retry
    public int RetryCount { get; set; }
    public DateTime? LastRetryAt { get; set; }

    // Estado do pagamento
    public bool PaymentProcessed { get; set; }
    public bool PaymentApproved { get; set; }
    public string? PaymentFailureReason { get; set; }
    public DateTime? PaymentReceivedAt { get; set; }

    // Estado do inventário
    public bool InventoryChecked { get; set; }
    public bool InventoryAvailable { get; set; }
    public string? InventoryFailureReason { get; set; }
    public DateTime? InventoryReceivedAt { get; set; }
}