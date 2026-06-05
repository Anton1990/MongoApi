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

    // Dead Letter Exchange — принимает сообщения которые не удалось обработать
    public const string DeadLetterExchange = "product-events.dlx";

    // Queues
    public static class Queues
    {
        public const string ProductCreatedNotifications = "product-created-notifications";

        // Dead Letter Queue — битые сообщения после N попыток
        public const string ProductCreatedDlq = "product-created-notifications.dlq";
    }

    // Routing keys
    public static class RoutingKeys
    {
        public const string ProductCreated = "product.created";
        public const string DeadLetter     = "dead";
    }

    /// <summary>
    /// Объявляет топологию в RabbitMQ.
    /// queues=null  → всё (IaC)
    /// queues=[]    → только exchange (producer)
    /// queues=[..] → exchange + указанные очереди (consumer)
    /// </summary>
    public static void Declare(IModel channel, string[]? queues = null)
    {
        // Main exchange
        channel.ExchangeDeclare(
            exchange:   ProductEventsExchange,
            type:       ExchangeType.Direct,
            durable:    true,
            autoDelete: false);

        // Dead Letter Exchange
        channel.ExchangeDeclare(
            exchange:   DeadLetterExchange,
            type:       ExchangeType.Direct,
            durable:    true,
            autoDelete: false);

        // Основная очередь
        if (queues is null || queues.Contains(Queues.ProductCreatedNotifications))
        {
            channel.QueueDeclare(
                queue:      Queues.ProductCreatedNotifications,
                durable:    true,
                exclusive:  false,
                autoDelete: false,
                arguments:  new Dictionary<string, object>
                {
                    // При BasicNack(requeue:false) сообщение уходит в DLX
                    ["x-dead-letter-exchange"]    = DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = RoutingKeys.DeadLetter
                });

            channel.QueueBind(
                queue:      Queues.ProductCreatedNotifications,
                exchange:   ProductEventsExchange,
                routingKey: RoutingKeys.ProductCreated);
        }

        // Dead Letter Queue
        if (queues is null || queues.Contains(Queues.ProductCreatedDlq))
        {
            channel.QueueDeclare(
                queue:      Queues.ProductCreatedDlq,
                durable:    true,
                exclusive:  false,
                autoDelete: false,
                arguments:  null);

            channel.QueueBind(
                queue:      Queues.ProductCreatedDlq,
                exchange:   DeadLetterExchange,
                routingKey: RoutingKeys.DeadLetter);
        }
    }
}
