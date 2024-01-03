public interface IDataUpdater {
    Task LoopAsync(CancellationToken stoppingToken);
}
