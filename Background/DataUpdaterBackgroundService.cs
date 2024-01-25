using AttendanceService.Kafka;
using Confluent.Kafka;

namespace AttendanceService.Background;

public class DataUpdaterBackgroundService : BackgroundService
{
    private readonly ILogger<DataUpdaterBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataUpdaterBackgroundService(
            ILogger<DataUpdaterBackgroundService> logger,
            IServiceScopeFactory scopeFactory) {
        this._logger = logger;
        this._scopeFactory  = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) {
            return;
        }
        
        using (var scope = this._scopeFactory.CreateScope()) {
            IConsumer<string, KafkaMessage> _kafkaConsumer = scope.ServiceProvider
                .GetRequiredService<IConsumer<string, KafkaMessage>>();

            string[] topics = new string[] { "members", "concerts", "rehearsals" };
            _kafkaConsumer.Subscribe(topics);
        }

        this._logger.LogInformation("Starting Kafka consumer loop");
        while (!stoppingToken.IsCancellationRequested) {
            using (var scope = this._scopeFactory.CreateScope()) {
                IDataUpdater updater = scope.ServiceProvider
                    .GetRequiredService<IDataUpdater>();

                IConsumer<string, KafkaMessage> kafkaConsumer = scope.ServiceProvider
                    .GetRequiredService<IConsumer<string, KafkaMessage>>();

                ConsumeResult<string, KafkaMessage> result = kafkaConsumer.Consume(1000);
                if (result is { Message: not null }) {
                    this._logger.LogInformation("Received message with CorrelationId {0}", 
                                                result.Message.Value.CorrelationId);
                    await updater.ProcessMessage(result.Topic, result.Message, stoppingToken);
                }
                await Task.Delay(10000);
            }
        }

        this._logger.LogInformation("Kafka consumer loop has stopped");
    }
}
