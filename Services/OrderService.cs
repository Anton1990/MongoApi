using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;

namespace MongoApi.Services;

public class OrderService
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Order> _orders;
    private readonly IMongoCollection<Product> _products;

    public OrderService(MongoDbContext context)
    {
        _client = context.Client;
        _orders = context.Orders;
        _products = context.Products;
    }

    public async Task<List<Order>> GetAllAsync() =>
        await _orders.Find(_ => true).ToListAsync();

    /// <summary>
    /// Создаёт заказ с ACID-транзакцией.
    ///
    /// Атомарно выполняет два действия в разных коллекциях:
    ///   1. orders    → InsertOne   (создать заказ)
    ///   2. products  → UpdateOne   (уменьшить stock)
    ///
    /// Если любая операция падает — обе откатываются (Atomicity).
    /// Другие запросы не видят промежуточного состояния (Isolation).
    ///
    /// ВАЖНО: требует Replica Set. На standalone MongoDB упадёт с ошибкой.
    /// Для локальной разработки запустить: mongod --replSet rs0
    /// </summary>
    public async Task<Order> CreateAsync(string productId, int quantity)
    {
        // Читаем продукт ДО транзакции — проверка что существует
        var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Product", productId);

        if (product.Stock < quantity)
            throw new ValidationException(
                $"Not enough stock. Available: {product.Stock}, requested: {quantity}");

        var order = new Order
        {
            ProductId = productId,
            ProductName = product.Name,    // Snapshot
            ProductPrice = product.Price,  // Snapshot
            Quantity = quantity,
            TotalAmount = product.Price * quantity,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        using var session = await _client.StartSessionAsync();

        // WithTransactionAsync — автоматически retry при TransientTransactionError
        await session.WithTransactionAsync(async (s, ct) =>
        {
            // Операция 1: создать заказ
            await _orders.InsertOneAsync(s, order, cancellationToken: ct);

            // Операция 2: уменьшить сток
            // Повторная проверка stock ВНУТРИ транзакции (Isolation — читаем актуальное)
            var update = Builders<Product>.Update.Inc(p => p.Stock, -quantity);
            var filter = Builders<Product>.Filter.And(
                Builders<Product>.Filter.Eq(p => p.Id, productId),
                Builders<Product>.Filter.Gte(p => p.Stock, quantity) // гарантия что хватит
            );

            var result = await _products.UpdateOneAsync(s, filter, update, cancellationToken: ct);

            if (result.ModifiedCount == 0)
                // Если сток стал недостаточным пока мы работали — откат
                throw new ConflictException("Stock was modified concurrently. Transaction aborted.");

            return order;
        });

        return order;
    }
}
