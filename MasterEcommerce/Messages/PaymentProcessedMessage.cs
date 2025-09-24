namespace MasterEcommerce.Messages;

public class PaymentProcessedMessage
{
    public Guid OrderId { get; set; }
    public bool IsApproved { get; set; }
    public string? FailureReason { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProcessedAt { get; set; }
}