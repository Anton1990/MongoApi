using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace MongoApi.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private const string ExchangeName = "product-events";

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        var host = configuration["RabbitMq__Host"] ?? "rabbitmq";
        TryConnect(host);
    }

    private void TryConnect(string host)
    {
        try
        {
            var factory = new ConnectionFactory { HostName = host };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
            _logger.LogInformation("Connected to RabbitMQ at {Host}", host);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ not available at startup. Events will not be published.");
        }
    }

    public void Publish<T>(string routingKey, T message)
    {
        if (_channel is null || !_channel.IsOpen)
        {
            _logger.LogWarning("RabbitMQ channel not available. Skipping publish for {RoutingKey}", routingKey);
            return;
        }

        try
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            _channel.BasicPublish(ExchangeName, routingKey, props, body);
            _logger.LogInformation("Published event to {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to {RoutingKey}", routingKey);
        }
    }

    public void PublishRaw(string routingKey, string json)
    {
        if (_channel is null || !_channel.IsOpen)
        {
            _logger.LogWarning("RabbitMQ channel not available. Skipping publish for {RoutingKey}", routingKey);
            return;
        }

        try
        {
            var body = Encoding.UTF8.GetBytes(json);
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            _channel.BasicPublish(ExchangeName, routingKey, props, body);
            _logger.LogInformation("Published raw event to {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish raw event to {RoutingKey}", routingKey);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
