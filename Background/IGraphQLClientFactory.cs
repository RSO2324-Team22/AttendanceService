using GraphQL.Client.Abstractions;

namespace AttendanceService.Background;

public interface IGraphQLClientFactory {
    IGraphQLClient GetConcertGraphQLClient();
    IGraphQLClient GetMembersGraphQLClient();
    IGraphQLClient GetRehearsalGraphQLClient();
}
