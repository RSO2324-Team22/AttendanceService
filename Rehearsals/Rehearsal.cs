namespace AttendanceService.Rehearsals;

public class Rehearsal {
    public int Id { get; set; }
    public required string Name { get; set; }
    public required RehearsalType Type { get; set; }
    public List<RehearsalAttendance> Attendances { get; set; } = new List<RehearsalAttendance>();
}
