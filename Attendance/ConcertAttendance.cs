namespace AttendanceService.Attendance;

public class ConcertAttendance {
    public int Id { get; private set; }
    public required Member Member { get; init; }
    public required Concert Concert { get; init; }
    public required bool IsPresent { get; set; }
    public string? ReasonForAbsence { get; set; }
}
