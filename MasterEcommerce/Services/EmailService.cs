using MasterEcommerce.Messages;
using MasterEcommerce.Services;

namespace MasterEcommerce.Services;

public interface IEmailService
{
    Task SendEmailAsync(SendEmailMessage emailMessage);
}

public class EmailService : IEmailService
{
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IMessageBusService messageBus, ILogger<EmailService> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task SendEmailAsync(SendEmailMessage emailMessage)
    {
        _logger.LogInformation("Sending email to {To} for order {OrderId}", 
            emailMessage.To, emailMessage.OrderId);

        // Simula o envio do email
        await Task.Delay(1000);

        // Log do "envio" do email (em um cenário real, aqui seria integrado com um provedor de email)
        _logger.LogInformation("EMAIL SENT:\n" +
                              "To: {To}\n" +
                              "Subject: {Subject}\n" +
                              "Body: {Body}\n" +
                              "Order ID: {OrderId}\n" +
                              "Type: {Type}",
            emailMessage.To, emailMessage.Subject, emailMessage.Body, 
            emailMessage.OrderId, emailMessage.Type);
    }

    public void StartListening()
    {
        _messageBus.Subscribe<SendEmailMessage>("email.send", SendEmailAsync);
        _logger.LogInformation("EmailService started listening for email.send messages");
    }
}