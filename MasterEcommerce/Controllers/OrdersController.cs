using Microsoft.AspNetCore.Mvc;
using MasterEcommerce.Models;
using MasterEcommerce.Messages;
using MasterEcommerce.Services;

namespace MasterEcommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMessageBusService _messageBus;
    private readonly IOrderOrchestrator _orchestrator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IMessageBusService messageBus, 
        IOrderOrchestrator orchestrator,
        ILogger<OrdersController> logger)
    {
        _messageBus = messageBus;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            // Validações básicas
            if (string.IsNullOrEmpty(request.CustomerEmail) || 
                string.IsNullOrEmpty(request.CustomerName) ||
                request.Items == null || !request.Items.Any())
            {
                return BadRequest("Dados do pedido inválidos");
            }

            var orderId = Guid.NewGuid();
            var totalAmount = request.Items.Sum(item => item.Price * item.Quantity);

            var order = new Order
            {
                OrderId = orderId,
                CustomerEmail = request.CustomerEmail,
                CustomerName = request.CustomerName,
                Items = request.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Price = item.Price,
                    Quantity = item.Quantity
                }).ToList(),
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Created
            };

            var orderMessage = new OrderCreatedMessage
            {
                OrderId = orderId,
                CustomerEmail = request.CustomerEmail,
                CustomerName = request.CustomerName,
                Items = request.Items.Select(item => new OrderItemMessage
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Price = item.Price,
                    Quantity = item.Quantity
                }).ToList(),
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow
            };

            // Inicializa o estado no orquestrador
            _orchestrator.InitializeOrder(orderMessage);

            // Publica a mensagem para os serviços processarem
            _messageBus.Publish("order.created", orderMessage);

            _logger.LogInformation("Order {OrderId} created for customer {CustomerEmail}", 
                orderId, request.CustomerEmail);

            return Ok(new CreateOrderResponse
            {
                OrderId = orderId,
                Message = "Pedido criado com sucesso! Processamento iniciado.",
                TotalAmount = totalAmount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpGet("{orderId}")]
    public IActionResult GetOrderStatus(Guid orderId)
    {
        // Em um cenário real, consultaria o banco de dados
        return Ok(new { OrderId = orderId, Status = "Processing" });
    }
}

public class CreateOrderRequest
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderResponse
{
    public Guid OrderId { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}