using RabbitMQ.Client;

namespace Shared.Messaging;

/// <summary>
/// Единый источник правды для топологии RabbitMQ.
/// Используется producer, consumer и IaC.
/// </summary>
public static class RabbitMqTopology
{
    // Exchange
    public const string ProductEventsExchange = "product-events";

    // Queues
    public static class Queues
    {
        public const string ProductCreatedNotifications = "product-created-notifications";
    }

    // Routing keys
    public static class RoutingKeys
    {
        public const string ProductCreated = "product.created";
    }

    /// <summary>
    /// Объявляет топологию в RabbitMQ.
    /// queues=null  → всё (IaC)
    /// queues=[]    → только exchange (producer)
    /// queues=[..] → exchange + указанные очереди (consumer)
    /// </summary>
    public static void Declare(IModel channel, string[]? queues = null)
    {
        // Exchange — всегда объявляем
        channel.ExchangeDeclare(
            exchange: ProductEventsExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        // Очередь product-created-notifications
        if (queues is null || queues.Contains(Queues.ProductCreatedNotifications))
        {
            // durable: true + сообщения с Persistent=true = хранение на диске
            channel.QueueDeclare(
                queue: Queues.ProductCreatedNotifications,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            channel.QueueBind(
                queue: Queues.ProductCreatedNotifications,
                exchange: ProductEventsExchange,
                routingKey: RoutingKeys.ProductCreated);
        }
    }
}
