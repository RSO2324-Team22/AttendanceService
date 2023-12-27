namespace AttendanceService.Attendance;

public class Member {
    public int Id { get; private set; }
    public required string Name { get; init; }
    public required Section Section { get; init; }
}
