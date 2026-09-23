using backend.Data;

namespace backend.Services;

public sealed class EndpointInitializationWorker : BackgroundService
{
    private readonly EndpointService endpointService;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<EndpointInitializationWorker> logger;

    public EndpointInitializationWorker(
        EndpointService endpointService,
        IServiceScopeFactory scopeFactory,
        ILogger<EndpointInitializationWorker> logger)
    {
        this.endpointService = endpointService;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            var repository =
                scope.ServiceProvider
                    .GetRequiredService<IEndpointRepository>();

            var endpoints =
                await repository.GetAllAsync(stoppingToken);

            endpointService.Load(endpoints);

            logger.LogInformation(
                "Loaded {Count} monitored endpoints from PostgreSQL.",
                endpoints.Count);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Endpoint initialization was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to load monitored endpoints from PostgreSQL.");
        }
        finally
        {
            // Always release MonitoringService.
            // This prevents it from waiting indefinitely.
            endpointService.MarkInitialized();
        }
    }
}