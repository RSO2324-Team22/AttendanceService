using AttendanceService.Common;
using AttendanceService.Concerts;
using AttendanceService.Database;
using AttendanceService.Rehearsals;
using Confluent.Kafka;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Background;

public class KafkaUpdater : IDataUpdater {
    private readonly ILogger<KafkaUpdater> _logger;
    private readonly AttendanceDbContext _dbContext;
    private readonly IConsumer<string, int> _kafkaConsumer;
    private readonly IGraphQLClient _membersGraphQLClient;
    private readonly IGraphQLClient _concertGraphQLClient;
    private readonly IGraphQLClient _rehearsalGraphQLClient;

    public KafkaUpdater(
            ILogger<KafkaUpdater> logger,
            AttendanceDbContext dbContext,
            IConsumer<string, int> kafkaConsumer,
            GraphQLClientFactory graphQLFactory) {
        this._logger = logger;
        this._dbContext = dbContext;
        this._kafkaConsumer = kafkaConsumer;
        this._membersGraphQLClient = graphQLFactory.GetMembersGraphQLClient();
        this._concertGraphQLClient = graphQLFactory.GetConcertGraphQLClient();
        this._rehearsalGraphQLClient = graphQLFactory.GetRehearsalGraphQLClient();
    }

    public async Task FetchDataAsync(CancellationToken stoppingToken) {
        await this._dbContext.Database.EnsureCreatedAsync();
        await this.FetchMembersAsync();
        await this.FetchConcertsAsync();
        await this.FetchMembersAsync();
        await this._dbContext.SaveChangesAsync();
    }

    private async Task FetchMembersAsync() {
        GraphQLRequest query = this.MakeAllMembersQuery();
        GraphQLResponse<List<Member>> response = 
            await this._membersGraphQLClient.SendQueryAsync<List<Member>>(query);
        List<Member> members = response.Data;
        this._dbContext.Add(members);
    }

    private async Task FetchConcertsAsync() {
        GraphQLRequest query = this.MakeAllConcertsQuery();
        GraphQLResponse<List<Concert>> response = 
            await this._concertGraphQLClient.SendQueryAsync<List<Concert>>(query);
        List<Concert> concerts = response.Data;
        this._dbContext.Add(concerts);
    }

    private async Task FetchRehearsalsAsync() {
        GraphQLRequest query = this.MakeAllRehearsalsQuery();
        GraphQLResponse<List<Rehearsal>> response = 
            await this._rehearsalGraphQLClient.SendQueryAsync<List<Rehearsal>>(query);
        List<Rehearsal> rehearsals = response.Data;
        this._dbContext.Add(rehearsals);
    }

    private GraphQLRequest MakeAllMembersQuery() {
        return new GraphQLRequest {
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
    }

    private GraphQLRequest MakeAllConcertsQuery() {
        return new GraphQLRequest {
            Query = @"
                query GetAllConcerts {
                    concerts {
                        All {
                            Id Name 
                        }
                    }
                }",
            OperationName = "GetAllConcerts",
        };
    }

    private GraphQLRequest MakeAllRehearsalsQuery() {
        return new GraphQLRequest {
            Query = @"
                query GetAllRehearsals {
                    rehearsals {
                        All {
                            Id Name 
                        }
                    }
                }",
            OperationName = "GetAllRehearsals",
        };
    }

    public async Task LoopAsync(CancellationToken stoppingToken) {
        string[] topics = new string[] { "members", "concerts", "rehearsals" };
        this._kafkaConsumer.Subscribe(topics);

        while(!stoppingToken.IsCancellationRequested) {
            ConsumeResult<string, int> result = this._kafkaConsumer.Consume(stoppingToken);
            if (result.Message != null) {
                await this.ProcessMessage(result.Topic, result.Message, stoppingToken);
            }
        }

        this._logger.LogInformation("Kafka consumer loop has stopped");
    }

    private async Task ProcessMessage(
            string topic,
            Message<string, int> message, 
            CancellationToken stoppingToken) {
        switch (topic) {
            case "members":
                await this.ProcessMembersMessage(message, stoppingToken);
                break;
            case "concerts":
                await this.ProcessConcertMessage(message, stoppingToken);
                break;
            case "rehearsals":
                await this.ProcessRehearsalMessage(message, stoppingToken);
                break;
        }

        await this._dbContext.SaveChangesAsync();
    }

    private async Task ProcessMembersMessage(
            Message<string, int> message, 
            CancellationToken stoppingToken) {
        string key = message.Key;
        int memberId = message.Value;
        switch (key) {
            case "add_member":                
                await this.AddMemberAsync(memberId);
                break;
            case "edit_member":
                await this.EditMemberAsync(memberId);
                break;
            case "delete_member":
                await this.DeleteMemberAsync(memberId);
                break;
        } 

        await this._dbContext.SaveChangesAsync();
    }

    private async Task AddMemberAsync(int memberId) {
        GraphQLRequest query = this.MakeMemberQuery(memberId);
        GraphQLResponse<Member> response = 
            await this._membersGraphQLClient.SendQueryAsync<Member>(query);
        Member member = response.Data;
        this._dbContext.Add(member);
    }

    private async Task EditMemberAsync(int memberId) {
        Member member = await this._dbContext.Members
            .Where(m => m.Id == memberId)
            .SingleAsync();

        GraphQLRequest query = this.MakeMemberQuery(memberId);
        GraphQLResponse<Member> response = 
            await this._membersGraphQLClient.SendQueryAsync<Member>(query);
        member.Name = response.Data.Name;
        this._dbContext.Add(member);
    }

    private async Task DeleteMemberAsync(int memberId) {
        Member member = await this._dbContext.Members
            .Where(m => m.Id == memberId)
            .SingleAsync();
        
        this._dbContext.Remove(member);
    }

    private async Task ProcessConcertMessage(
            Message<string, int> message,
            CancellationToken stoppingToken) {
        string key = message.Key;
        int memberId = message.Value;
        switch (key) {
            case "add_concert":                
                await this.AddConcertAsync(memberId);
                break;
            case "edit_concert":
                await this.EditConcertAsync(memberId);
                break;
            case "delete_concert":
                await this.DeleteConcertAsync(memberId);
                break;
        } 

        await this._dbContext.SaveChangesAsync();
    }

    private async Task AddConcertAsync(int concertId) {
        GraphQLRequest query = this.MakeConcertQuery(concertId);
        GraphQLResponse<Concert> response = 
            await this._concertGraphQLClient.SendQueryAsync<Concert>(query);
        Concert concert = response.Data;
        this._dbContext.Add(concert);
    }

    private async Task EditConcertAsync(int concertId) {
        Concert concert = await this._dbContext.Concerts
            .Where(m => m.Id == concertId)
            .SingleAsync();

        GraphQLRequest query = this.MakeConcertQuery(concertId);
        GraphQLResponse<Concert> response = 
            await this._concertGraphQLClient.SendQueryAsync<Concert>(query);
        concert.Name = response.Data.Name;
        this._dbContext.Add(concert);
    }

    private async Task DeleteConcertAsync(int concertId) {
        Concert concert = await this._dbContext.Concerts
            .Where(m => m.Id == concertId)
            .SingleAsync();
        
        this._dbContext.Remove(concert);
    }

    private async Task ProcessRehearsalMessage(
            Message<string, int> message,
            CancellationToken stoppingToken) {
        string key = message.Key;
        int memberId = message.Value;
        switch (key) {
            case "add_rehearsal":                
                await this.AddRehearsalAsync(memberId);
                break;
            case "edit_rehearsal":
                await this.EditRehearsalAsync(memberId);
                break;
            case "delete_rehearsal":
                await this.DeleteRehearsalAsync(memberId);
                break;
        } 

        await this._dbContext.SaveChangesAsync();
    }

    private async Task AddRehearsalAsync(int rehearsalId) {
        GraphQLRequest query = this.MakeRehearsalQuery(rehearsalId);
        GraphQLResponse<Rehearsal> response = 
            await this._rehearsalGraphQLClient.SendQueryAsync<Rehearsal>(query);
        Rehearsal rehearsal = response.Data;
        this._dbContext.Add(rehearsal);
    }

    private async Task EditRehearsalAsync(int rehearsalId) {
        Rehearsal rehearsal = await this._dbContext.Rehearsals
            .Where(m => m.Id == rehearsalId)
            .SingleAsync();

        GraphQLRequest query = this.MakeRehearsalQuery(rehearsalId);
        GraphQLResponse<Rehearsal> response = 
            await this._rehearsalGraphQLClient.SendQueryAsync<Rehearsal>(query);
        rehearsal.Name = response.Data.Name;
        this._dbContext.Add(rehearsal);
    }

    private async Task DeleteRehearsalAsync(int rehearsalId) {
        Rehearsal rehearsal = await this._dbContext.Rehearsals
            .Where(m => m.Id == rehearsalId)
            .SingleAsync();
        
        this._dbContext.Remove(rehearsal);
    }

    private GraphQLRequest MakeMemberQuery(int id) {
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

    private GraphQLRequest MakeConcertQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetConcert($id: ID) {
                    Concert(id: $id) {
                        Id Name 
                    }
                }",
            OperationName = "GetConcerts",
            Variables = new {
                id = id
            }
        };
    }

    private GraphQLRequest MakeRehearsalQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetRehearsal($id: ID) {
                    Rehearsal(id: $id) {
                        Id Name 
                    }
                }",
            OperationName = "GetRehearsal",
            Variables = new {
                id = id
            }
        };
    }
}
