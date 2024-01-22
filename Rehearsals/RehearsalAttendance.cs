using AttendanceService.Members;

namespace AttendanceService.Rehearsals;

public class RehearsalAttendance {
    public int Id { get; set; }
    public required Member Member { get; set; }
    public required Rehearsal Rehearsal { get; set; }
    public required bool IsPresent { get; set; }
    public string? ReasonForAbsence { get; set; }
}
