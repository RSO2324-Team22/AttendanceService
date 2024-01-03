namespace AttendanceService.Background;

public class DataUpdaterBackgroundService : BackgroundService
{
    private readonly ILogger<DataUpdaterBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataUpdaterBackgroundService(
            ILogger<DataUpdaterBackgroundService> logger,
            IServiceScopeFactory scopeFactory) {
        this._logger = logger;
        this._scopeFactory  = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = this._scopeFactory.CreateScope()) {
            IDataUpdater updater = scope.ServiceProvider
                .GetRequiredService<IDataUpdater>();

            await updater.LoopAsync(stoppingToken);
        }
    }
}
