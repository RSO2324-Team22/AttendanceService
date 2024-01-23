using AttendanceService.Common;
using AttendanceService.Database;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Rehearsals.GraphQL;

public class RehearsalGraphQLService : IDataFetchService<Rehearsal> {
    private readonly ILogger<RehearsalGraphQLService> _logger;
    private readonly AttendanceDbContext _dbContext;
    private readonly IGraphQLClient _graphQLClient;

    public RehearsalGraphQLService(
            ILogger<RehearsalGraphQLService> logger,
            AttendanceDbContext dbContext,
            IGraphQLClient graphQLClient) {
        this._logger = logger;
        this._dbContext = dbContext;
        this._graphQLClient = graphQLClient;
    }

    public async Task AddAllAsync(CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Fetching rehearsals");
        try {
            GraphQLResponse<RehearsalGraphResponse> response = 
                await this._graphQLClient.SendQueryAsync<RehearsalGraphResponse>(AllRehearsalsQuery);

            List<Rehearsal> rehearsals = response.Data.RehearsalGraph.All ?? new List<Rehearsal>();
            this._dbContext.AddRange(rehearsals);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Successfully fetched rehearsals");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while fetching rehearsals");
        }
    }

    public async Task AddAsync(int rehearsalId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Adding rehearsal {0}", rehearsalId);
        try {
            GraphQLRequest query = MakeRehearsalQuery(rehearsalId);
            GraphQLResponse<RehearsalGraphResponse> response = 
                await this._graphQLClient.SendQueryAsync<RehearsalGraphResponse>(query);

            if (response.Errors is not null) {
                this._logger.LogError("GraphQL error while fetching rehearsal {id}.", rehearsalId);
                return;
            }

            Rehearsal rehearsal = response.Data.RehearsalGraph.Rehearsal!;
            this._dbContext.Add(rehearsal);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Added rehearsal {id}", rehearsalId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while adding rehearsal {id}", rehearsalId);
        }
    }

    public async Task DeleteAsync(int rehearsalId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        try {
            this._logger.LogInformation("Deleting rehearsal {id}", rehearsalId);
            Rehearsal rehearsal = await this._dbContext.Rehearsals
                .Where(m => m.Id == rehearsalId)
                .SingleAsync();
            
            this._dbContext.Remove(rehearsal);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Deleted rehearsal {id}", rehearsalId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while deleting rehearsal {id}", rehearsalId);
        }
    }

    public async Task EditAsync(int rehearsalId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Editing rehearsal {id}", rehearsalId);
        try {
            Rehearsal rehearsal = await this._dbContext.Rehearsals
                .Where(m => m.Id == rehearsalId)
                .SingleAsync();

            GraphQLRequest query = MakeRehearsalQuery(rehearsalId);
            GraphQLResponse<Rehearsal> response = 
                await this._graphQLClient.SendQueryAsync<Rehearsal>(query);
            rehearsal.Title = response.Data.Title;
            this._dbContext.Add(rehearsal);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Edited rehearsal {id}", rehearsalId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing rehearsal {id}", rehearsalId);
        }
    }


    private static GraphQLRequest AllRehearsalsQuery = new GraphQLRequest {
        Query = @"
            query GetAllRehearsals {
                rehearsalGraph {
                    all {
                        id
                        title
                    }
                }
            }",
        OperationName = "GetAllRehearsals",
    };

    private static GraphQLRequest MakeRehearsalQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetRehearsal($id: ID) {
                    rehearsalGraph {
                        rehearsal(id: $id) {
                            id
                            title
                        }
                    }
                }",
            OperationName = "GetRehearsal",
            Variables = new {
                id = id
            }
        };
    }
}
