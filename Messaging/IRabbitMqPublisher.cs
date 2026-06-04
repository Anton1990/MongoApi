namespace MongoApi.Messaging;

public interface IRabbitMqPublisher
{
    bool Publish<T>(string routingKey, T message);
    bool PublishRaw(string routingKey, string json);
}
