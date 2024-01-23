namespace AttendanceService.Rehearsals;

public class Rehearsal {
    public int Id { get; set; }
    public required string Title { get; set; }
    public List<RehearsalAttendance> Attendances { get; set; } = new List<RehearsalAttendance>();
}
