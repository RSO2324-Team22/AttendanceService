using AttendanceService.Common;
using AttendanceService.Concerts;
using AttendanceService.Kafka;
using AttendanceService.Members;
using AttendanceService.Rehearsals;
using Confluent.Kafka;

namespace AttendanceService.Background;

public class KafkaUpdater : IDataUpdater {
    private readonly ILogger<KafkaUpdater> _logger;
    private readonly IConsumer<string, KafkaMessage> _kafkaConsumer;
    private readonly IDataFetchService<Member> _memberFetchService;
    private readonly IDataFetchService<Concert> _concertFetchService;
    private readonly IDataFetchService<Rehearsal> _rehearsalFetchService;

    public KafkaUpdater(
            ILogger<KafkaUpdater> logger,
            IConsumer<string, KafkaMessage> kafkaConsumer,
            IDataFetchService<Member> memberFetchService,
            IDataFetchService<Concert> concertFetchService,
            IDataFetchService<Rehearsal> rehearsalFetchService) {
        this._logger = logger;
        this._kafkaConsumer = kafkaConsumer;
        this._memberFetchService = memberFetchService;
        this._concertFetchService = concertFetchService;
        this._rehearsalFetchService = rehearsalFetchService;
    }

    public async Task FetchDataAsync(CancellationToken stoppingToken) {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        this._logger.LogInformation("Syncing data from upstream services");
        try {
            await this._memberFetchService.AddAllAsync();
            await this._concertFetchService.AddAllAsync();
            await this._rehearsalFetchService.AddAllAsync();
            this._logger.LogInformation("Sync successful");
        }
        catch (Exception e) {
            this._logger.LogError(e, "Error while syncing data.");
        }
    }

    public async Task LoopAsync(CancellationToken stoppingToken) {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        string[] topics = new string[] { "members", "concerts", "rehearsals" };
        this._kafkaConsumer.Subscribe(topics);

        this._logger.LogInformation("Starting Kafka consumer loop");
        while (!stoppingToken.IsCancellationRequested) {
            ConsumeResult<string, KafkaMessage> result = this._kafkaConsumer.Consume(1000);
            if (result is { Message: not null }) {
                this._logger.LogInformation("Received message with CorrelationId {0}", 
                                            result.Message.Value.CorrelationId);
                await this.ProcessMessage(result.Topic, result.Message, stoppingToken);
            }
            await Task.Delay(10000);
        }

        this._logger.LogInformation("Kafka consumer loop has stopped");
    }

    private async Task ProcessMessage(
            string topic,
            Message<string, KafkaMessage> message, 
            CancellationToken stoppingToken) {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

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
    }

    private async Task ProcessMembersMessage(
            Message<string, KafkaMessage> message, 
            CancellationToken stoppingToken) {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        string key = message.Key;
        int memberId = message.Value.EntityId;
        switch (key) {
            case "add_member":                
                await this._memberFetchService.AddAsync(memberId, stoppingToken);
                break;
            case "edit_member":
                await this._memberFetchService.EditAsync(memberId, stoppingToken);
                break;
            case "delete_member":
                await this._memberFetchService.DeleteAsync(memberId, stoppingToken);
                break;
        } 
    }

    private async Task ProcessConcertMessage(
            Message<string, KafkaMessage> message,
            CancellationToken stoppingToken) {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        string key = message.Key;
        int concertId = message.Value.EntityId;
        switch (key) {
            case "add_concert":                
                await this._concertFetchService.AddAsync(concertId, stoppingToken);
                break;
            case "edit_concert":
                await this._concertFetchService.EditAsync(concertId, stoppingToken);
                break;
            case "delete_concert":
                await this._concertFetchService.DeleteAsync(concertId, stoppingToken);
                break;
        } 
    }

    private async Task ProcessRehearsalMessage(
            Message<string, KafkaMessage> message,
            CancellationToken stoppingToken) {
        if (stoppingToken.IsCancellationRequested) {
            throw new OperationCanceledException();
        }

        string key = message.Key;
        int rehearsalId = message.Value.EntityId;
        switch (key) {
            case "add_rehearsal":                
                await this._rehearsalFetchService.AddAsync(rehearsalId, stoppingToken);
                break;
            case "edit_rehearsal":
                await this._rehearsalFetchService.EditAsync(rehearsalId, stoppingToken);
                break;
            case "delete_rehearsal":
                await this._rehearsalFetchService.DeleteAsync(rehearsalId, stoppingToken);
                break;
        } 
    }
}
