public interface IDataUpdater {
    Task FetchDataAsync(CancellationToken stoppingToken);
    Task LoopAsync(CancellationToken stoppingToken);
}
