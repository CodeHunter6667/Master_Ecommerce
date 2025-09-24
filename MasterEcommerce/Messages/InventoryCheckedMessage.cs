namespace MasterEcommerce.Messages;

public class InventoryCheckedMessage
{
    public Guid OrderId { get; set; }
    public bool IsAvailable { get; set; }
    public string? FailureReason { get; set; }
    public List<InventoryItemResult> ItemResults { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class InventoryItemResult
{
    public int ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}