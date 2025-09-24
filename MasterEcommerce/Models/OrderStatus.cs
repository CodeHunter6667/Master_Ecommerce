namespace MasterEcommerce.Models;

public enum OrderStatus
{
    Created,
    ProcessingPayment,
    CheckingInventory,
    ReadyToShip,
    Shipped,
    Failed,
    Cancelled
}