using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Messaging;

public class OutboxPublisher : BackgroundService
{
    private readonly IMongoCollection<OutboxMessage> _outbox;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        MongoDbContext context,
        IRabbitMqPublisher publisher,
        ILogger<OutboxPublisher> logger)
    {
        _outbox = context.Outbox;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _outbox
                    .Find(m => !m.Published)
                    .ToListAsync(stoppingToken);

                foreach (var message in pending)
                {
                    var published = _publisher.PublishRaw(message.RoutingKey, message.Payload);

                    if (!published)
                    {
                        _logger.LogWarning(
                            "Outbox failed to publish: {RoutingKey} (id={Id}), will retry",
                            message.RoutingKey, message.Id);
                        continue;
                    }

                    await _outbox.UpdateOneAsync(
                        m => m.Id == message.Id,
                        Builders<OutboxMessage>.Update.Set(m => m.Published, true),
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "Outbox published: {RoutingKey} (id={Id})",
                        message.RoutingKey, message.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPublisher error");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
