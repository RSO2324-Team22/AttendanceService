using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace AttendanceService.Background;

public class GraphQLClientFactory : IGraphQLClientFactory
{
    private readonly ILogger<GraphQLClientFactory> _logger;
    private readonly IConfiguration _config;

    public GraphQLClientFactory(
            ILogger<GraphQLClientFactory> logger,
            IConfiguration config)
    {
        this._logger = logger;
        this._config = config;
    }

    public IGraphQLClient GetMembersGraphQLClient()
    {
        string membersGraphQLUrl = this._config["MembersService:GraphQL:Url"] ?? "";

        return new GraphQLHttpClient(
            membersGraphQLUrl,
            new SystemTextJsonSerializer());
    }

    public IGraphQLClient GetConcertGraphQLClient()
    {
        string planningGraphQLUrl = this._config["PlanningService:GraphQL:Url"] ?? "";

        return new GraphQLHttpClient(
            planningGraphQLUrl,
            new SystemTextJsonSerializer());
    }

    public IGraphQLClient GetRehearsalGraphQLClient()
    {
        string planningGraphQLUrl = this._config["PlanningService:GraphQL:Url"] ?? "";

        return new GraphQLHttpClient(
            planningGraphQLUrl,
            new SystemTextJsonSerializer());
    }
}
