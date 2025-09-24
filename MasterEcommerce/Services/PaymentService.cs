using MasterEcommerce.Messages;
using MasterEcommerce.Services;

namespace MasterEcommerce.Services;

public interface IPaymentService
{
    Task ProcessPaymentAsync(OrderCreatedMessage order);
}

public class PaymentService : IPaymentService
{
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<PaymentService> _logger;
    private readonly Random _random;

    public PaymentService(IMessageBusService messageBus, ILogger<PaymentService> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
        _random = new Random();
    }

    public async Task ProcessPaymentAsync(OrderCreatedMessage order)
    {
        _logger.LogInformation("Processing payment for order {OrderId}", order.OrderId);

        // Simula processamento do pagamento com delay
        await Task.Delay(2000);

        // Simula aprovação ou rejeição aleatória (70% de chance de aprovação)
        var isApproved = _random.NextDouble() > 0.3;
        
        var paymentResult = new PaymentProcessedMessage
        {
            OrderId = order.OrderId,
            IsApproved = isApproved,
            Amount = order.TotalAmount,
            ProcessedAt = DateTime.UtcNow,
            FailureReason = isApproved ? null : "Pagamento rejeitado pelo banco"
        };

        _messageBus.Publish("payment.processed", paymentResult);
        
        _logger.LogInformation("Payment for order {OrderId} processed. Approved: {IsApproved}", 
            order.OrderId, isApproved);
    }

    public void StartListening()
    {
        _messageBus.Subscribe<OrderCreatedMessage>("order.created", ProcessPaymentAsync);
        _logger.LogInformation("PaymentService started listening for order.created messages");
    }
}