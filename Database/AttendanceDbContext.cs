using AttendanceService.Attendance;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Database;

public class AttendanceDbContext : DbContext {
    private readonly ILogger<AttendanceDbContext> _logger;

    public DbSet<Concert> Concerts { get; private set; }
    public DbSet<Rehearsal> Rehearsals { get; private set; }
    public DbSet<ConcertAttendance> ConcertAttendances { get; private set; }
    public DbSet<RehearsalAttendance> RehearsalAttendances { get; private set; }

    public AttendanceDbContext(
            DbContextOptions<AttendanceDbContext> options,
            ILogger<AttendanceDbContext> logger) : base(options) {
        this._logger = logger;
    }
}
