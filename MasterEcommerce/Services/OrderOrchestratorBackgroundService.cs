using MasterEcommerce.Services;

namespace MasterEcommerce.Services;

public class OrderOrchestratorBackgroundService : BackgroundService
{
    private readonly IOrderOrchestrator _orderOrchestrator;
    private readonly ILogger<OrderOrchestratorBackgroundService> _logger;

    public OrderOrchestratorBackgroundService(
        IOrderOrchestrator orderOrchestrator,
        ILogger<OrderOrchestratorBackgroundService> logger)
    {
        _orderOrchestrator = orderOrchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderOrchestrator background service starting");

        try
        {
            await _orderOrchestrator.StartBackgroundProcessingAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrderOrchestrator background service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OrderOrchestrator background service encountered an error");
            throw;
        }
        finally
        {
            _logger.LogInformation("OrderOrchestrator background service stopped");
        }
    }
}