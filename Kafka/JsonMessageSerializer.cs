using System.Text.Json;
using Confluent.Kafka;

namespace AttendanceService.Kafka;

public class JsonMessageSerializer<T> : IAsyncSerializer<T>, IAsyncDeserializer<T>
{
    public async Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, bool isNull, SerializationContext context)
    {
        using (var ms = new MemoryStream(data.ToArray())) {
            T? instance = await JsonSerializer.DeserializeAsync<T>(ms);
            if (instance is null) {
                throw new Exception($"Could not deserialize object of type {nameof(T)}");
            }

            return instance;
        }
    }

    public async Task<byte[]> SerializeAsync(T data, SerializationContext context)
    {
        using (var ms = new MemoryStream())
        {
            await JsonSerializer.SerializeAsync(ms, data);
            var writer = new StreamWriter(ms);
            writer.Flush();
            ms.Position = 0;

            return ms.ToArray();
        }    
    }
}
