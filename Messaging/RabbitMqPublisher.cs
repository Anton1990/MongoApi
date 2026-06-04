using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Messaging;

namespace MongoApi.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

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

            // Producer объявляет только exchange — очереди создают consumer и IaC
            RabbitMqTopology.Declare(_channel, queues: []);

            // Publisher Confirms — брокер подтверждает что принял сообщение
            _channel.ConfirmSelect();

            _logger.LogInformation("Connected to RabbitMQ at {Host}", host);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ not available at startup. Events will not be published.");
        }
    }

    public bool Publish<T>(string routingKey, T message)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        return PublishInternal(routingKey, body);
    }

    public bool PublishRaw(string routingKey, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        return PublishInternal(routingKey, body);
    }

    private bool PublishInternal(string routingKey, byte[] body)
    {
        if (_channel is null || !_channel.IsOpen)
        {
            _logger.LogWarning("RabbitMQ channel not available. Skipping publish for {RoutingKey}", routingKey);
            return false;
        }

        try
        {
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            _channel.BasicPublish(RabbitMqTopology.ProductEventsExchange, routingKey, props, body);

            // Ждём подтверждения от брокера (таймаут 5 сек)
            var confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));
            if (!confirmed)
            {
                _logger.LogWarning("RabbitMQ NACK for {RoutingKey}", routingKey);
                return false;
            }

            _logger.LogInformation("Published and confirmed: {RoutingKey}", routingKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to {RoutingKey}", routingKey);
            return false;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
