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

    private readonly string _host;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        _host = configuration["RabbitMq__Host"] ?? "rabbitmq";
        TryConnect();
    }

    private void TryConnect()
    {
        try
        {
            _connection?.Dispose();
            _channel?.Dispose();

            var factory = new ConnectionFactory { HostName = _host };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Producer объявляет только exchange — очереди создают consumer и IaC
            RabbitMqTopology.Declare(_channel, queues: []);

            // Publisher Confirms — брокер подтверждает что принял сообщение
            _channel.ConfirmSelect();

            _logger.LogInformation("Connected to RabbitMQ at {Host}", _host);
        }
        catch (Exception ex)
        {
            _channel = null;
            _connection = null;
            _logger.LogWarning(ex, "RabbitMQ not available. Will retry on next publish.");
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
            _logger.LogWarning("RabbitMQ channel not available, attempting reconnect...");
            TryConnect();
        }

        if (_channel is null || !_channel.IsOpen)
        {
            _logger.LogWarning("RabbitMQ unavailable. Skipping publish for {RoutingKey}", routingKey);
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
