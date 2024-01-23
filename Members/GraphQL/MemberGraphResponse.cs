namespace AttendanceService.Members.GraphQL;

public class MemberGraphResponse {
    public required MemberGraph MemberGraph { get; set; }
}

public class MemberGraph {
    public List<Member>? All { get; set; }
    public Member? Member { get; set; }
}
