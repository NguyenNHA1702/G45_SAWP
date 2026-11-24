using Services.Interfaces;

namespace API.Common;

/// <summary>Runs the items of <see cref="IBackgroundWorkQueue"/> one by one, each in a new scope; failures are logged.</summary>
public class BackgroundWorkService : BackgroundService
{
    private readonly IBackgroundWorkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundWorkService> _logger;

    public BackgroundWorkService(IBackgroundWorkQueue queue, IServiceScopeFactory scopeFactory, ILogger<BackgroundWorkService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            (string Description, Func<IServiceProvider, CancellationToken, Task> Work) item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await item.Work(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Background work failed: {Description}", item.Description);
            }
        }
    }
}
