namespace Contracts.Events;

public record ProductCreatedEvent
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime OccurredAt { get; init; }
}
