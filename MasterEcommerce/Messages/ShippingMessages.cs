namespace MasterEcommerce.Messages;

public class ScheduleShippingMessage
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItemMessage> Items { get; set; } = new();
    public DateTime ScheduledAt { get; set; }
}

public class ShippingProcessedMessage
{
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
}