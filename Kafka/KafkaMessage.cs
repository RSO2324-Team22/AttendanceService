namespace AttendanceService.Kafka;

public class KafkaMessage {
    public required int EntityId { get; set; }
    public required string CorrelationId { get; set; }
}
