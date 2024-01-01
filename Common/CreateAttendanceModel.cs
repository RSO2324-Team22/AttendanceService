namespace AttendanceService.Common;

public class CreateAttendanceModel {
    public required int MemberId { get; set; }
    public required bool IsPresent { get; set; }
    public string? ReasonForAbsence { get; set; }
}
