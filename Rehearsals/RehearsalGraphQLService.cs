using AttendanceService.Common;
using AttendanceService.Database;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Rehearsals;

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

        this._logger.LogInformation("Fetching concerts");
        try {
            GraphQLResponse<List<Rehearsal>> response = 
                await this._graphQLClient.SendQueryAsync<List<Rehearsal>>(AllRehearsalsQuery);
            List<Rehearsal> concerts = response.Data ?? new List<Rehearsal>();
            this._dbContext.AddRange(concerts);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Successfully fetched concerts");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while fetching concerts");
        }
    }

    public async Task AddAsync(int concertId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Adding concert {id}", concertId);
        try {
            GraphQLRequest query = MakeRehearsalQuery(concertId);
            GraphQLResponse<Rehearsal> response = 
                await this._graphQLClient.SendQueryAsync<Rehearsal>(query);
            Rehearsal concert = response.Data;
            this._dbContext.Add(concert);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Added concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while adding concert {id}", concertId);
        }
    }

    public async Task DeleteAsync(int concertId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Deleting concert {id}", concertId);
        try {
            Rehearsal concert = await this._dbContext.Rehearsals
                .Where(m => m.Id == concertId)
                .SingleAsync();
            
            this._dbContext.Remove(concert);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Deleted concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while deleting concert {id}", concertId);
        }
    }

    public async Task EditAsync(int concertId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Editing concert {id}", concertId);
        try {
            Rehearsal concert = await this._dbContext.Rehearsals
                .Where(m => m.Id == concertId)
                .SingleAsync();

            GraphQLRequest query = MakeRehearsalQuery(concertId);
            GraphQLResponse<Rehearsal> response = 
                await this._graphQLClient.SendQueryAsync<Rehearsal>(query);
            concert.Name = response.Data.Name;
            this._dbContext.Add(concert);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Edited concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing concert {id}", concertId);
        }
    }


    private static GraphQLRequest AllRehearsalsQuery = new GraphQLRequest {
        Query = @"
            query GetAllRehearsals {
                concerts {
                    All {
                        Id Name 
                    }
                }
            }",
        OperationName = "GetAllRehearsals",
    };

    private static GraphQLRequest MakeRehearsalQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetRehearsal($id: ID) {
                    Rehearsal(id: $id) {
                        Id Name 
                    }
                }",
            OperationName = "GetRehearsals",
            Variables = new {
                id = id
            }
        };
    }
}
