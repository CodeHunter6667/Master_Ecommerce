using MasterEcommerce.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services
builder.Services.AddSingleton<IMessageBusService, RabbitMQService>();
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton<InventoryService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<ShippingService>();
builder.Services.AddSingleton<IOrderOrchestrator, OrderOrchestrator>();
builder.Services.AddSingleton<OrderOrchestrator>(provider => 
    (OrderOrchestrator)provider.GetRequiredService<IOrderOrchestrator>());

// Add background service for order orchestrator
builder.Services.AddHostedService<OrderOrchestratorBackgroundService>();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

// Initialize microservices
var paymentService = app.Services.GetRequiredService<PaymentService>();
var inventoryService = app.Services.GetRequiredService<InventoryService>();
var emailService = app.Services.GetRequiredService<EmailService>();
var shippingService = app.Services.GetRequiredService<ShippingService>();
var orchestrator = app.Services.GetRequiredService<IOrderOrchestrator>();

// Start listening to messages
paymentService.StartListening();
inventoryService.StartListening();
emailService.StartListening();
shippingService.StartListening();
orchestrator.StartListening();

app.Logger.LogInformation("MasterEcommerce application started - All microservices are listening");
app.Logger.LogInformation("OrderOrchestrator background processing will start automatically");
app.Logger.LogInformation("Available endpoints:");
app.Logger.LogInformation("POST /api/orders - Create a new order");
app.Logger.LogInformation("GET /api/orders/{id} - Get order status");

app.Run();
