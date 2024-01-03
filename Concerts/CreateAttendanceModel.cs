namespace AttendanceService.Concerts;

public class CreateConcertAttendanceModel {
    public required int MemberId { get; set; }
    public required bool IsPresent { get; set; }
    public string? ReasonForAbsence { get; set; }
}
