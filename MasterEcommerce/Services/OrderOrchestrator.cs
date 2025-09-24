using MasterEcommerce.Messages;
using MasterEcommerce.Models;
using MasterEcommerce.Services;

namespace MasterEcommerce.Services;

public interface IOrderOrchestrator
{
    Task HandlePaymentProcessedAsync(PaymentProcessedMessage message);
    Task HandleInventoryCheckedAsync(InventoryCheckedMessage message);
}

public class OrderOrchestrator : IOrderOrchestrator
{
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<OrderOrchestrator> _logger;
    
    // Dicionário para armazenar o estado dos pedidos em processamento
    private readonly Dictionary<Guid, OrderProcessingState> _orderStates;

    public OrderOrchestrator(IMessageBusService messageBus, ILogger<OrderOrchestrator> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
        _orderStates = new Dictionary<Guid, OrderProcessingState>();
    }

    public async Task HandlePaymentProcessedAsync(PaymentProcessedMessage message)
    {
        _logger.LogInformation("Handling payment result for order {OrderId}. Approved: {IsApproved}", 
            message.OrderId, message.IsApproved);

        if (!_orderStates.ContainsKey(message.OrderId))
        {
            _orderStates[message.OrderId] = new OrderProcessingState 
            { 
                OrderId = message.OrderId 
            };
        }

        var state = _orderStates[message.OrderId];
        state.PaymentProcessed = true;
        state.PaymentApproved = message.IsApproved;
        state.PaymentFailureReason = message.FailureReason;

        await CheckOrderCompletionAsync(state);
    }

    public async Task HandleInventoryCheckedAsync(InventoryCheckedMessage message)
    {
        _logger.LogInformation("Handling inventory result for order {OrderId}. Available: {IsAvailable}", 
            message.OrderId, message.IsAvailable);

        if (!_orderStates.ContainsKey(message.OrderId))
        {
            _orderStates[message.OrderId] = new OrderProcessingState 
            { 
                OrderId = message.OrderId 
            };
        }

        var state = _orderStates[message.OrderId];
        state.InventoryChecked = true;
        state.InventoryAvailable = message.IsAvailable;
        state.InventoryFailureReason = message.FailureReason;

        await CheckOrderCompletionAsync(state);
    }

    private async Task CheckOrderCompletionAsync(OrderProcessingState state)
    {
        // Aguarda tanto o pagamento quanto o inventário serem processados
        if (!state.PaymentProcessed || !state.InventoryChecked)
        {
            _logger.LogInformation("Order {OrderId} still waiting for processing. Payment: {PaymentProcessed}, Inventory: {InventoryChecked}",
                state.OrderId, state.PaymentProcessed, state.InventoryChecked);
            return;
        }

        // Verifica se ambos foram aprovados
        if (state.PaymentApproved && state.InventoryAvailable)
        {
            await HandleOrderApprovedAsync(state);
        }
        else
        {
            await HandleOrderRejectedAsync(state);
        }

        // Remove o estado após processar
        _orderStates.Remove(state.OrderId);
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
    }

    public void InitializeOrder(OrderCreatedMessage order)
    {
        var state = new OrderProcessingState
        {
            OrderId = order.OrderId,
            CustomerEmail = order.CustomerEmail,
            CustomerName = order.CustomerName,
            TotalAmount = order.TotalAmount,
            Items = order.Items
        };

        _orderStates[order.OrderId] = state;
    }

    public void StartListening()
    {
        _messageBus.Subscribe<PaymentProcessedMessage>("payment.processed", HandlePaymentProcessedAsync);
        _messageBus.Subscribe<InventoryCheckedMessage>("inventory.checked", HandleInventoryCheckedAsync);
        
        _logger.LogInformation("OrderOrchestrator started listening for payment.processed and inventory.checked messages");
    }
}

public class OrderProcessingState
{
    public Guid OrderId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemMessage>? Items { get; set; }
    
    public bool PaymentProcessed { get; set; }
    public bool PaymentApproved { get; set; }
    public string? PaymentFailureReason { get; set; }
    
    public bool InventoryChecked { get; set; }
    public bool InventoryAvailable { get; set; }
    public string? InventoryFailureReason { get; set; }
}