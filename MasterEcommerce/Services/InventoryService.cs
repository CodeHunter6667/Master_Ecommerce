using MasterEcommerce.Messages;
using MasterEcommerce.Services;

namespace MasterEcommerce.Services;

public interface IInventoryService
{
    Task CheckInventoryAsync(OrderCreatedMessage order);
}

public class InventoryService : IInventoryService
{
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<InventoryService> _logger;
    private readonly Dictionary<int, int> _inventory;

    public InventoryService(IMessageBusService messageBus, ILogger<InventoryService> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
        
        // Mock de inventário - inicializa com alguns produtos
        _inventory = new Dictionary<int, int>
        {
            { 1, 10 }, // Produto 1 - 10 unidades
            { 2, 5 },  // Produto 2 - 5 unidades
            { 3, 0 },  // Produto 3 - sem estoque
            { 4, 20 }, // Produto 4 - 20 unidades
            { 5, 3 }   // Produto 5 - 3 unidades
        };
    }

    public async Task CheckInventoryAsync(OrderCreatedMessage order)
    {
        _logger.LogInformation("Checking inventory for order {OrderId}", order.OrderId);

        // Simula delay do processamento
        await Task.Delay(1500);

        var itemResults = new List<InventoryItemResult>();
        var allAvailable = true;
        var failureReason = string.Empty;

        foreach (var item in order.Items)
        {
            var availableQuantity = _inventory.GetValueOrDefault(item.ProductId, 0);
            var isAvailable = availableQuantity >= item.Quantity;
            
            if (!isAvailable)
            {
                allAvailable = false;
                if (!string.IsNullOrEmpty(failureReason))
                    failureReason += "; ";
                failureReason += $"Produto {item.ProductName} indisponível (solicitado: {item.Quantity}, disponível: {availableQuantity})";
            }
            else
            {
                // Reserva o produto (diminui do estoque)
                _inventory[item.ProductId] -= item.Quantity;
            }

            itemResults.Add(new InventoryItemResult
            {
                ProductId = item.ProductId,
                RequestedQuantity = item.Quantity,
                AvailableQuantity = availableQuantity,
                IsAvailable = isAvailable
            });
        }

        var inventoryResult = new InventoryCheckedMessage
        {
            OrderId = order.OrderId,
            IsAvailable = allAvailable,
            FailureReason = allAvailable ? null : failureReason,
            ItemResults = itemResults,
            CheckedAt = DateTime.UtcNow
        };

        _messageBus.Publish("inventory.checked", inventoryResult);
        
        _logger.LogInformation("Inventory check for order {OrderId} completed. Available: {IsAvailable}", 
            order.OrderId, allAvailable);
    }

    public void StartListening()
    {
        _messageBus.Subscribe<OrderCreatedMessage>("order.inventory", CheckInventoryAsync);
        _logger.LogInformation("InventoryService started listening for order.inventory messages");
    }
}