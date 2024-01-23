namespace AttendanceService.Rehearsals.GraphQL;

public class RehearsalGraphResponse {
    public required RehearsalGraph RehearsalGraph { get; set; }
}

public class RehearsalGraph {
    public List<Rehearsal>? All { get; set; }
    public Rehearsal? Rehearsal { get; set; }
}
