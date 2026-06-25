using System.Text.Json;
using Contracts.Events;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class ProductService : BaseMongoService<Product>, IProductService
{
    private readonly IMongoCollection<OutboxMessage> _outbox;

    public ProductService(MongoDbContext context)
        : base(context.Products)
    {
        _outbox = context.Outbox;
    }

    protected override HashSet<string> AllowedFilterFields => new()
    {
        nameof(Product.Name),
        nameof(Product.Price),
        nameof(Product.Stock),
        nameof(Product.IsAvailable),
        nameof(Product.CategoryId),
        nameof(Product.CreatedAt),
        nameof(Product.Status),
        $"{nameof(Product.Manufacturer)}.{nameof(Manufacturer.Country)}",
        $"{nameof(Product.Manufacturer)}.{nameof(Manufacturer.Name)}"
    };

    public override async Task<Product> CreateAsync(Product product)
    {
        await _collection.InsertOneAsync(product);

        await _outbox.InsertOneAsync(new OutboxMessage
        {
            RoutingKey = "product.created",
            Payload = JsonSerializer.Serialize(new ProductCreatedEvent
            {
                Id = product.Id ?? string.Empty,
                Name = product.Name,
                Price = product.Price,
                OccurredAt = DateTime.UtcNow
            })
        });

        return product;
    }

    public async Task<Product> UpdateAsync(string id, Product updated)
    {
        var expectedVersion = updated.Version;

        updated.Id = id;
        updated.Version = expectedVersion + 1;

        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, id),
            Builders<Product>.Filter.Eq(p => p.Version, expectedVersion)
        );

        var result = await _collection.ReplaceOneAsync(filter, updated);

        if (result.MatchedCount == 0)
            throw new NotFoundException("Product", id);

        if (result.ModifiedCount == 0)
            throw new ConflictException(
                $"Product {id} was modified by another request. " +
                $"Expected version: {expectedVersion}, please reload and try again.");

        return updated;
    }

    public override IQueryable<Product> GetQueryable() =>
        _collection.AsQueryable();
}
