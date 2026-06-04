using System.Text;
using System.Text.Json;
using Contracts.Events;
using MongoDB.Driver;
using NotificationService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Consumers;

public class ProductCreatedConsumer : BackgroundService
{
    private readonly IMongoCollection<Notification> _notifications;
    private readonly ILogger<ProductCreatedConsumer> _logger;
    private readonly string _hostName;
    private const string ExchangeName = "product-events";
    private const string QueueName = "product-created-notifications";
    private const string RoutingKey = "product.created";

    public ProductCreatedConsumer(
        IMongoCollection<Notification> notifications,
        ILogger<ProductCreatedConsumer> logger,
        IConfiguration configuration)
    {
        _notifications = notifications;
        _logger = logger;
        _hostName = configuration["RabbitMq:Host"] ?? "rabbitmq";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ connection failed. Retrying in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _hostName,
            DispatchConsumersAsync = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(QueueName, ExchangeName, RoutingKey);
        channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<ProductCreatedEvent>(json);

                if (evt is not null)
                {
                    var notification = new Notification
                    {
                        ProductId = evt.Id,
                        ProductName = evt.Name,
                        Price = evt.Price,
                        ReceivedAt = DateTime.UtcNow,
                        Message = $"Product '{evt.Name}' was created at {evt.OccurredAt:u}"
                    };

                    await _notifications.InsertOneAsync(notification, cancellationToken: stoppingToken);
                    _logger.LogInformation("Saved notification for product: {Name}", evt.Name);
                }

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ProductCreatedEvent");
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("ProductCreatedConsumer started, listening on queue: {Queue}", QueueName);

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }
}
