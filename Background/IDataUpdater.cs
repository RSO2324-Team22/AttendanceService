using AttendanceService.Kafka;
using Confluent.Kafka;

namespace AttendanceService.Background;

public interface IDataUpdater {
    Task FetchDataAsync(CancellationToken stoppingToken);
    Task LoopAsync(CancellationToken stoppingToken);
    Task ProcessMessage(string operation, 
                        Message<string, KafkaMessage> message, 
                        CancellationToken stoppingToken);
}
