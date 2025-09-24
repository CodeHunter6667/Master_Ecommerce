using MasterEcommerce.Messages;
using MasterEcommerce.Services;

namespace MasterEcommerce.Services;

public interface IShippingService
{
    Task ProcessShippingAsync(ScheduleShippingMessage shippingMessage);
}

public class ShippingService : IShippingService
{
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<ShippingService> _logger;

    public ShippingService(IMessageBusService messageBus, ILogger<ShippingService> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task ProcessShippingAsync(ScheduleShippingMessage shippingMessage)
    {
        _logger.LogInformation("Processing shipping for order {OrderId}", shippingMessage.OrderId);

        // Simula o agendamento e processamento da entrega
        await Task.Delay(3000);

        // Gera um número de rastreamento fictício
        var trackingNumber = $"TRK{DateTime.UtcNow.Ticks.ToString()[^8..]}";

        var shippingResult = new ShippingProcessedMessage
        {
            OrderId = shippingMessage.OrderId,
            TrackingNumber = trackingNumber,
            ShippedAt = DateTime.UtcNow
        };

        // Envia mensagem informando que o pedido foi enviado
        _messageBus.Publish("shipping.processed", shippingResult);

        // Envia email para o cliente informando sobre o envio
        var emailMessage = new SendEmailMessage
        {
            To = shippingMessage.CustomerEmail,
            Subject = $"Pedido {shippingMessage.OrderId} foi enviado!",
            Body = $"Olá {shippingMessage.CustomerName},\n\n" +
                   $"Seu pedido {shippingMessage.OrderId} foi enviado!\n" +
                   $"Número de rastreamento: {trackingNumber}\n" +
                   $"Data de envio: {DateTime.UtcNow:dd/MM/yyyy HH:mm}\n\n" +
                   $"Acompanhe sua entrega através do código de rastreamento.",
            OrderId = shippingMessage.OrderId,
            Type = EmailType.OrderShipped
        };

        _messageBus.Publish("email.send", emailMessage);
        
        _logger.LogInformation("Shipping processed for order {OrderId}. Tracking: {TrackingNumber}", 
            shippingMessage.OrderId, trackingNumber);
    }

    public void StartListening()
    {
        _messageBus.Subscribe<ScheduleShippingMessage>("shipping.schedule", ProcessShippingAsync);
        _logger.LogInformation("ShippingService started listening for shipping.schedule messages");
    }
}