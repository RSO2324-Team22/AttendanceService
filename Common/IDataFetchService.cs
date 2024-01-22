namespace AttendanceService.Common;

public interface IDataFetchService<T> {
    Task AddAllAsync(CancellationToken stoppingToken = default);
    Task AddAsync(int id, CancellationToken stoppingToken = default);
    Task EditAsync(int id, CancellationToken stoppingToken = default);
    Task DeleteAsync(int id, CancellationToken stoppingToken = default);
}
