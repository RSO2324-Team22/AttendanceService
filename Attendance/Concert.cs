namespace AttendanceService.Attendance;

public class Concert {
    public int Id { get; init; }
    public required string Name { get; init; }
    public List<ConcertAttendance> Attendances { get; init; } = new List();
}
