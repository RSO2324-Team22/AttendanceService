using AttendanceService.Common;
using AttendanceService.Concerts;
using AttendanceService.Database;
using AttendanceService.Rehearsals;
using Confluent.Kafka;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceService.Background;

public class KafkaGraphQLUpdater : IDataUpdater {
    private readonly ILogger<KafkaGraphQLUpdater> _logger;
    private readonly AttendanceDbContext _dbContext;
    private readonly IConsumer<string, int> _kafkaConsumer;
    private readonly IGraphQLClientFactory _graphQLClientFactory;

    public KafkaGraphQLUpdater(
            ILogger<KafkaGraphQLUpdater> logger,
            AttendanceDbContext dbContext,
            IConsumer<string, int> kafkaConsumer,
            IGraphQLClientFactory graphQLFactory) {
        this._logger = logger;
        this._dbContext = dbContext;
        this._kafkaConsumer = kafkaConsumer;
        this._graphQLClientFactory = graphQLFactory;
    }

    public async Task FetchDataAsync(CancellationToken stoppingToken) {
        try {
            this._logger.LogInformation("Syncing data from upstream services");
            await this._dbContext.Database.EnsureCreatedAsync();
            await Task.WhenAll(
                this.FetchMembersAsync(),
                this.FetchConcertsAsync(),
                this.FetchRehearsalsAsync()
            );
            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Sync successful");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while syncing data.");
        }
    }

    private async Task FetchMembersAsync() {
        try {
            this._logger.LogInformation("Fetching members");
            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetMembersGraphQLClient();
            GraphQLRequest query = allMembersQuery;
            GraphQLResponse<List<Member>> response = 
                await graphQLClient.SendQueryAsync<List<Member>>(query);
            List<Member> members = response.Data ?? new List<Member>();
            this._dbContext.AddRange(members);
            this._logger.LogInformation("Successfully fetched members");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while fetching members");
        }
    }

    private async Task FetchConcertsAsync() {
        try {
            this._logger.LogInformation("Fetching concerts");
            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetConcertGraphQLClient();
            GraphQLRequest query = allConcertsQuery;
            GraphQLResponse<List<Concert>> response = 
                await graphQLClient.SendQueryAsync<List<Concert>>(query);
            List<Concert> concerts = response.Data ?? new List<Concert>();
            this._dbContext.AddRange(concerts);
            this._logger.LogInformation("Successfully fetched concerts");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while fetching concerts");
        }
    }

    private async Task FetchRehearsalsAsync() {
        try {
            this._logger.LogInformation("Fetching rehearsals");
            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetRehearsalGraphQLClient();
            GraphQLRequest query = allRehearsalsQuery;
            GraphQLResponse<List<Rehearsal>> response = 
                await graphQLClient.SendQueryAsync<List<Rehearsal>>(query);
            List<Rehearsal> rehearsals = response.Data ?? new List<Rehearsal>();
            this._dbContext.AddRange(rehearsals);
            this._logger.LogInformation("Successfully fetched rehearsals");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while fetching rehearsals");
        }
    }

    private static GraphQLRequest allMembersQuery = new GraphQLRequest {
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

    private static GraphQLRequest allConcertsQuery = new GraphQLRequest {
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

    private static GraphQLRequest allRehearsalsQuery = new GraphQLRequest {
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

    public async Task LoopAsync(CancellationToken stoppingToken) {
        string[] topics = new string[] { "members", "concerts", "rehearsals" };
        this._kafkaConsumer.Subscribe(topics);

        this._logger.LogInformation("Starting Kafka consumer loop");
        while (!stoppingToken.IsCancellationRequested) {
            ConsumeResult<string, int> result = this._kafkaConsumer.Consume(1000);
            if (result is { Message: not null }) {
                await this.ProcessMessage(result.Topic, result.Message, stoppingToken);
            }
            await Task.Delay(10000);
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
        try {
            this._logger.LogInformation("Adding member {id}", memberId);
            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetMembersGraphQLClient();
            GraphQLRequest query = MakeMemberQuery(memberId);
            GraphQLResponse<Member> response = 
                await graphQLClient.SendQueryAsync<Member>(query);
            Member member = response.Data;
            this._dbContext.Add(member);
            this._logger.LogInformation("Added member {id}", memberId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while adding member {id}", memberId);
        }
    }

    private async Task EditMemberAsync(int memberId) {
        this._logger.LogInformation("Editing member {id}", memberId);
        try {
            Member member = await this._dbContext.Members
                .Where(m => m.Id == memberId)
                .SingleAsync();

            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetMembersGraphQLClient();
            GraphQLRequest query = MakeMemberQuery(memberId);
            GraphQLResponse<Member> response = 
                await graphQLClient.SendQueryAsync<Member>(query);
            member.Name = response.Data.Name;
            this._dbContext.Add(member);
            this._logger.LogInformation("Edited member {id}", memberId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing member {id}", memberId);
        }
    }

    private async Task DeleteMemberAsync(int memberId) {
        this._logger.LogInformation("Deleting member {id}", memberId);
        try {
            Member member = await this._dbContext.Members
                .Where(m => m.Id == memberId)
                .SingleAsync();
            
            this._dbContext.Remove(member);
            this._logger.LogInformation("Deleted member {id}", memberId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while deleting member {id}", memberId);
        }
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
        this._logger.LogInformation("Adding concert {id}", concertId);
        try {
            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetConcertGraphQLClient();
            GraphQLRequest query = MakeConcertQuery(concertId);
            GraphQLResponse<Concert> response = 
                await graphQLClient.SendQueryAsync<Concert>(query);
            Concert concert = response.Data;
            this._dbContext.Add(concert);
            this._logger.LogInformation("Added concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while adding concert {id}", concertId);
        }
    }

    private async Task EditConcertAsync(int concertId) {
        this._logger.LogInformation("Editing concert {id}", concertId);
        try {
            Concert concert = await this._dbContext.Concerts
                .Where(m => m.Id == concertId)
                .SingleAsync();

            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetConcertGraphQLClient();
            GraphQLRequest query = MakeConcertQuery(concertId);
            GraphQLResponse<Concert> response = 
                await graphQLClient.SendQueryAsync<Concert>(query);
            concert.Name = response.Data.Name;
            this._dbContext.Add(concert);
            this._logger.LogInformation("Edited concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing concert {id}", concertId);
        }
    }

    private async Task DeleteConcertAsync(int concertId) {
        try {
            this._logger.LogInformation("Deleting concert {id}", concertId);
            Concert concert = await this._dbContext.Concerts
                .Where(m => m.Id == concertId)
                .SingleAsync();
            
            this._dbContext.Remove(concert);
            this._logger.LogInformation("Deleted concert {id}", concertId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while deleting concert {id}", concertId);
        }
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
        this._logger.LogInformation("Adding rehearsal {id}", rehearsalId);
        try {
            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetRehearsalGraphQLClient();
            GraphQLRequest query = MakeRehearsalQuery(rehearsalId);
            GraphQLResponse<Rehearsal> response = 
                await graphQLClient.SendQueryAsync<Rehearsal>(query);
            Rehearsal rehearsal = response.Data;
            this._dbContext.Add(rehearsal);
            this._logger.LogInformation("Added rehearsal {id}", rehearsalId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while adding rehearsal {id}", rehearsalId);
        }
    }

    private async Task EditRehearsalAsync(int rehearsalId) {
        this._logger.LogInformation("Editing rehearsal {id}", rehearsalId);
        try {
            Rehearsal rehearsal = await this._dbContext.Rehearsals
                .Where(m => m.Id == rehearsalId)
                .SingleAsync();

            IGraphQLClient graphQLClient = this._graphQLClientFactory.GetRehearsalGraphQLClient();
            GraphQLRequest query = MakeRehearsalQuery(rehearsalId);
            GraphQLResponse<Rehearsal> response = 
                await graphQLClient.SendQueryAsync<Rehearsal>(query);
            rehearsal.Name = response.Data.Name;
            this._dbContext.Add(rehearsal);
            this._logger.LogInformation("Edited rehearsal {id}", rehearsalId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while editing rehearsal {id}", rehearsalId);
        }
}

    private async Task DeleteRehearsalAsync(int rehearsalId) {
        this._logger.LogInformation("Deleting rehearsal {id}", rehearsalId);
        try {
            Rehearsal rehearsal = await this._dbContext.Rehearsals
                .Where(m => m.Id == rehearsalId)
                .SingleAsync();
            
            this._dbContext.Remove(rehearsal);
            this._logger.LogInformation("Deleted rehearsal {id}", rehearsalId);
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while deleting rehearsal {id}", rehearsalId);
        }
    }

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

    private static GraphQLRequest MakeConcertQuery(int id) {
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

    private static GraphQLRequest MakeRehearsalQuery(int id) {
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
