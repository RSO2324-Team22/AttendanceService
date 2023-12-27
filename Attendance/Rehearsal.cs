namespace AttendanceService.Attendance;

public class Rehearsal {
    public int Id { get; init; }
    public required string Name { get; init; }
    public required RehearsalType Type { get; init; }
}
