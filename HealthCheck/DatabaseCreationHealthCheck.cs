using AttendanceService.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AttendanceService.HealthCheck;

public class DatabaseCreationHealthCheck : IHealthCheck
{
    private readonly ILogger<DatabaseCreationHealthCheck> _logger;
    private AttendanceDbContext dbContext;
    private readonly Task<bool> dbCreationTask;

    public DatabaseCreationHealthCheck(
            AttendanceDbContext dbContext,
            ILogger<DatabaseCreationHealthCheck> logger) {
        this._logger = logger;
        this.dbContext = dbContext;
        this.dbCreationTask = dbContext.Database.EnsureCreatedAsync();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (this.dbCreationTask.IsCompleted) {
            this._logger.LogInformation("Startup healthcheck succeeded.");
            return Task.FromResult(HealthCheckResult.Healthy("Startup is completed"));
        }

        this._logger.LogInformation("Startup healthcheck failed.");
        return Task.FromResult(HealthCheckResult.Unhealthy("Startup is still running")); 
    }
}
