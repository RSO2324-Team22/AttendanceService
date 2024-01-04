using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace AttendanceService.Background;

public class GraphQLClientFactory {
    private readonly ILogger<GraphQLClientFactory> _logger;
    private readonly IConfiguration _config;

    public GraphQLClientFactory(
            ILogger<GraphQLClientFactory> logger,
            IConfiguration config) {
        this._logger = logger;
        this._config = config;
    }

    public IGraphQLClient GetMembersGraphQLClient() {
        string membersGraphQLUrl = this._config["MembersService:GraphQL:Url"] ?? "";
        int membersGraphQLPort = int.Parse(this._config["MembersService:GraphQL:Port"] ?? "");

        return new GraphQLHttpClient(
            membersGraphQLUrl + ":" + membersGraphQLPort, 
            new SystemTextJsonSerializer());
    }

    public IGraphQLClient GetConcertGraphQLClient() {
        string planningGraphQLUrl = this._config["PlanningService:GraphQL:Url"] ?? "";
        int planningGraphQLPort = int.Parse(this._config["PlanningService:GraphQL:Port"] ?? "");
        
        return new GraphQLHttpClient(
            planningGraphQLUrl + "/concert" + ":" + planningGraphQLPort, 
            new SystemTextJsonSerializer());
    }

    public IGraphQLClient GetRehearsalGraphQLClient() {
        string planningGraphQLUrl = this._config["PlanningService:GraphQL:Url"] ?? "";
        int planningGraphQLPort = int.Parse(this._config["PlanningService:GraphQL:Port"] ?? "");
        
        return new GraphQLHttpClient(
            planningGraphQLUrl + "/rehearsal" + ":" + planningGraphQLPort, 
            new SystemTextJsonSerializer());
    }
}
