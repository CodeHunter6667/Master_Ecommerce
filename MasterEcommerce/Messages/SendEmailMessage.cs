namespace MasterEcommerce.Messages;

public class SendEmailMessage
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public EmailType Type { get; set; }
}

public enum EmailType
{
    OrderConfirmed,
    OrderFailed,
    OrderShipped
}