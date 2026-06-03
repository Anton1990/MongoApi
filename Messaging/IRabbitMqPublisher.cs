namespace MongoApi.Messaging;

public interface IRabbitMqPublisher
{
    void Publish<T>(string routingKey, T message);
    void PublishRaw(string routingKey, string json);
}
