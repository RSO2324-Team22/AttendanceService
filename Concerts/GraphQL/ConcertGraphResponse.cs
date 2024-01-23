namespace AttendanceService.Concerts.GraphQL;

public class ConcertGraphResponse {
    public required ConcertGraph ConcertGraph { get; set; }
}

public class ConcertGraph {
    public List<Concert>? All { get; set; }
    public Concert? Concert { get; set; }
}
