
namespace AttendanceService.Background;

public class DataFetchService : IHostedService
{
    private readonly ILogger<DataFetchService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataFetchService(
            ILogger<DataFetchService> logger,
            IServiceScopeFactory scopeFactory) {
        this._logger = logger;
        this._scopeFactory  = scopeFactory;
    }

    public async Task StartAsync(CancellationToken stoppingToken) {
        using (var scope = this._scopeFactory.CreateScope()) {
            IDataUpdater updater = scope.ServiceProvider
                .GetRequiredService<IDataUpdater>();

            await updater.FetchDataAsync(stoppingToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}
