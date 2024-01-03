namespace AttendanceService.Concerts;

public class Concert {
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<ConcertAttendance> Attendances { get; set; } = new List<ConcertAttendance>();
}
