using AttendanceService.Common;
using AttendanceService.Database;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Concerts;

public class ConcertGraphQLService : IDataFetchService<Concert> {
    private readonly ILogger<ConcertGraphQLService> _logger;
    private readonly AttendanceDbContext _dbContext;
    private readonly IGraphQLClient _graphQLClient;

    public ConcertGraphQLService(
            ILogger<ConcertGraphQLService> logger,
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
            GraphQLResponse<List<Concert>> response = 
                await this._graphQLClient.SendQueryAsync<List<Concert>>(AllConcertsQuery);
            List<Concert> concerts = response.Data ?? new List<Concert>();
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

        this._logger.LogInformation("Adding concert {0}", concertId);
        try {
            GraphQLRequest query = MakeConcertQuery(concertId);
            GraphQLResponse<Concert> response = 
                await this._graphQLClient.SendQueryAsync<Concert>(query);
            Concert concert = response.Data;
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

        try {
            this._logger.LogInformation("Deleting concert {id}", concertId);
            Concert concert = await this._dbContext.Concerts
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
            Concert concert = await this._dbContext.Concerts
                .Where(m => m.Id == concertId)
                .SingleAsync();

            GraphQLRequest query = MakeConcertQuery(concertId);
            GraphQLResponse<Concert> response = 
                await this._graphQLClient.SendQueryAsync<Concert>(query);
            concert.Title = response.Data.Title;
            this._dbContext.Add(concert);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Edited concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing concert {id}", concertId);
        }
    }


    private static GraphQLRequest AllConcertsQuery = new GraphQLRequest {
        Query = @"
            query GetAllConcerts {
                concertGraph {
                    all {
                        id
                        title
                    }
                }
            }",
        OperationName = "GetAllConcerts",
    };

    private static GraphQLRequest MakeConcertQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetConcert($id: ID) {
                    concertGraph {
                        concert(id: $id) {
                            id
                            title
                        }
                    }
                }",
            OperationName = "GetConcert",
            Variables = new {
                id = id
            }
        };
    }
}
