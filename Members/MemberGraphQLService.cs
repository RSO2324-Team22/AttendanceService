using AttendanceService.Common;
using AttendanceService.Database;
using Confluent.Kafka;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Members;

public class MemberGraphQLService : IDataFetchService<Member> {
    private readonly ILogger<MemberGraphQLService> _logger;
    private readonly AttendanceDbContext _dbContext;
    private readonly IGraphQLClient _graphQLClient;

    public MemberGraphQLService(
            ILogger<MemberGraphQLService> logger,
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

        this._logger.LogInformation("Fetching members");
        try {
            GraphQLResponse<List<Member>> response = 
                await this._graphQLClient.SendQueryAsync<List<Member>>(AllMembersQuery);
            List<Member> members = response.Data ?? new List<Member>();
            this._dbContext.AddRange(members);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Successfully fetched members");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while fetching members");
        }
    }

    public async Task AddAsync(int memberId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Adding member {id}", memberId);
        try {
            GraphQLRequest query = MakeMemberQuery(memberId);
            GraphQLResponse<Member> response = 
                await this._graphQLClient.SendQueryAsync<Member>(query);
            Member member = response.Data;
            this._dbContext.Add(member);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Added member {id}", memberId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while adding member {id}", memberId);
        }
    }

    public async Task DeleteAsync(int memberId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Deleting member {id}", memberId);
        try {
            Member member = await this._dbContext.Members
                .Where(m => m.Id == memberId)
                .SingleAsync();
            
            this._dbContext.Remove(member);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Deleted member {id}", memberId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while deleting member {id}", memberId);
        }
    }

    public async Task EditAsync(int memberId, CancellationToken stoppingToken = default)
    {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Editing member {id}", memberId);
        try {
            Member member = await this._dbContext.Members
                .Where(m => m.Id == memberId)
                .SingleAsync();

            GraphQLRequest query = MakeMemberQuery(memberId);
            GraphQLResponse<Member> response = 
                await this._graphQLClient.SendQueryAsync<Member>(query);
            member.Name = response.Data.Name;
            this._dbContext.Add(member);
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Edited member {id}", memberId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing member {id}", memberId);
        }
    }

    private static GraphQLRequest AllMembersQuery = new GraphQLRequest {
        Query = @"
            query GetAllMembers {
                members {
                    All {
                        Id Name 
                    }
                }
            }",
        OperationName = "GetAllMembers",
    };

    private static GraphQLRequest MakeMemberQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetMember($id: ID) {
                    Member(id: $id) {
                        Id Name 
                    }
                }",
            OperationName = "GetMember",
            Variables = new {
                id = id
            }
        };
    }
}
