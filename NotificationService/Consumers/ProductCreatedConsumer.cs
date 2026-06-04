using System.Text;
using System.Text.Json;
using Contracts.Events;
using MongoDB.Driver;
using NotificationService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging;

namespace NotificationService.Consumers;

public class ProductCreatedConsumer : BackgroundService
{
    private readonly IMongoCollection<Notification> _notifications;
    private readonly ILogger<ProductCreatedConsumer> _logger;
    private readonly string _hostName;

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

        // Consumer объявляет свою очередь (страховка если IaC не отработал)
        RabbitMqTopology.Declare(channel, queues: [RabbitMqTopology.Queues.ProductCreatedNotifications]);
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

        channel.BasicConsume(RabbitMqTopology.Queues.ProductCreatedNotifications, autoAck: false, consumer: consumer);

        _logger.LogInformation("ProductCreatedConsumer started, listening on queue: {Queue}", RabbitMqTopology.Queues.ProductCreatedNotifications);

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }
}
