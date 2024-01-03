using AttendanceService.Common;
using AttendanceService.Database;
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
    private readonly IGraphQLClient _planningGraphQLClient;

    public KafkaUpdater(
            ILogger<KafkaUpdater> logger,
            AttendanceDbContext dbContext,
            IConsumer<string, int> kafkaConsumer,
            GraphQLClientFactory graphQLFactory) {
        this._logger = logger;
        this._dbContext = dbContext;
        this._kafkaConsumer = kafkaConsumer;
        this._membersGraphQLClient = graphQLFactory.GetMembersGraphQLClient();
        this._planningGraphQLClient = graphQLFactory.GetPlanningGraphQLClient();
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
                await this.ProcessConcertsMessage(message, stoppingToken);
                break;
            case "rehearsals":
                await this.ProcessRehearsalsMessage(message, stoppingToken);
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

    private async Task ProcessConcertsMessage(
            Message<string, int> message,
            CancellationToken stoppingToken) {

    }

    private async Task ProcessRehearsalsMessage(
            Message<string, int> message,
            CancellationToken stoppingToken) {

    }

    private GraphQLRequest MakeMemberQuery(int id) {
        return new GraphQLRequest {
            Query = @"
                query GetMember($id: ID) {
                    Member(id: $id) {
                        Id Name 
                    }
                }
                ",
            OperationName = "GetMember",
            Variables = new {
                id = id
            }
        };
    }
}
