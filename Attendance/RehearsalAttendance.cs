namespace AttendanceService.Attendance;

public class RehearsalAttendance {
    public int Id { get; init; }
    public required Member Member { get; init; }
    public required Rehearsal Rehearsal { get; init; }
    public required bool IsPresent { get; set; }
    public string? ReasonForAbsence { get; set; }
}
