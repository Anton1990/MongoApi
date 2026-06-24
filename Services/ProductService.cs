using System.Linq.Expressions;
using System.Text.Json;
using Contracts.Events;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoApi.Filtering;
using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Models.Dtos;

namespace MongoApi.Services;

public class ProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<OutboxMessage> _outbox;

    public ProductService(MongoDbContext context)
    {
        _products = context.Products;
        _outbox = context.Outbox;
    }

    public async Task<List<Product>> GetAllAsync() =>
        await _products.Find(_ => true).ToListAsync();

    public async Task<Product?> GetByIdAsync(string id) =>
        await _products.Find(p => p.Id == id).FirstOrDefaultAsync();

    /// <summary>
    /// Возвращает все продукты с данными категории через $lookup (аналог LEFT JOIN).
    ///
    /// Эквивалент SQL:
    ///   SELECT p.*, c.name, c.slug, c.description
    ///   FROM products p
    ///   LEFT JOIN categories c ON p.categoryId = c._id
    /// </summary>
    public async Task<List<ProductWithCategoryDto>> GetAllWithCategoryAsync()
    {
        // $lookup: "соединяем" products с categories по полю categoryId → _id
        var lookupStage = new BsonDocument("$lookup", new BsonDocument
        {
            { "from", "categories" },
            { "localField", "categoryId" },   // поле в products
            { "foreignField", "_id" },         // поле в categories
            { "as", "category" }               // имя поля с результатом (массив)
        });

        // $unwind: разворачиваем массив "category" в один объект
        // preserveNullAndEmptyArrays: true — аналог LEFT JOIN (не теряем продукты без категории)
        var unwindStage = new BsonDocument("$unwind", new BsonDocument
        {
            { "path", "$category" },
            { "preserveNullAndEmptyArrays", true }
        });

        var pipeline = PipelineDefinition<Product, ProductWithCategoryDto>.Create(
            new[] { lookupStage, unwindStage });

        return await _products.Aggregate(pipeline).ToListAsync();
    }

    /// <summary>
    /// Курсорная пагинация по продуктам.
    ///
    /// Как работает:
    ///   1. Первый запрос: cursor = null → берём первые pageSize продуктов
    ///   2. Следующий запрос: cursor = Id последнего продукта → берём следующие pageSize
    ///
    /// Почему Id а не дата:
    ///   MongoDB ObjectId уже содержит timestamp внутри и лексикографически сортируется
    ///   по времени создания — не нужно отдельное поле.
    /// </summary>
    public async Task<CursorPageResult<Product>> GetPageAsync(string? cursor, int pageSize = 20)
    {
        // Если cursor передан — берём только продукты ПОСЛЕ него
        // Если нет — берём с самого начала
        var filter = cursor is null
            ? Builders<Product>.Filter.Empty
            : Builders<Product>.Filter.Gt(p => p.Id, cursor);

        // Запрашиваем pageSize + 1 чтобы понять — есть ли ещё данные
        var items = await _products
            .Find(filter)
            .Sort(Builders<Product>.Sort.Ascending(p => p.Id))
            .Limit(pageSize + 1)
            .ToListAsync();

        var hasMore = items.Count > pageSize;

        if (hasMore)
            items.RemoveAt(items.Count - 1); // убираем лишний элемент

        var nextCursor = hasMore ? items.Last().Id : null;

        return new CursorPageResult<Product>(items, nextCursor, hasMore);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await _products.InsertOneAsync(product);

        // Сохраняем событие в Outbox — BackgroundService опубликует в RabbitMQ
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

    /// <summary>
    /// Обновляет продукт с проверкой версии (Optimistic Concurrency).
    ///
    /// Клиент передаёт версию которую получил при чтении.
    /// Фильтр: id == X AND version == expectedVersion
    /// Если другой запрос уже обновил документ — version изменилась → ModifiedCount = 0 → исключение.
    /// </summary>
    public async Task<Product> UpdateAsync(string id, Product updated)
    {
        var expectedVersion = updated.Version;

        updated.Id = id;
        updated.Version = expectedVersion + 1; // инкрементируем версию

        // Фильтр включает version — ключевое условие Optimistic Concurrency
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, id),
            Builders<Product>.Filter.Eq(p => p.Version, expectedVersion)
        );

        var result = await _products.ReplaceOneAsync(filter, updated);

        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Product {id} not found");

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException(
                $"Product {id} was modified by another request. " +
                $"Expected version: {expectedVersion}, please reload and try again.");

        return updated;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    // Поля разрешённые для expression-фильтрации
    private static readonly HashSet<string> AllowedFilterFields = new()
    {
        nameof(Product.Name),
        nameof(Product.Price),
        nameof(Product.Stock),
        nameof(Product.IsAvailable),
        nameof(Product.CategoryId),
        nameof(Product.CreatedAt)
    };

    /// <summary>
    /// Поиск через строку-выражение в стиле Expression Tree.
    /// Поддерживает: ==, !=, &lt;, &gt;, &lt;=, &gt;=, Contains, StartsWith, EndsWith
    /// Поддерживает: AND, OR, скобки для группировки
    ///
    /// Примеры:
    ///   Price&gt;100 AND IsAvailable==True
    ///   (Price&gt;=50 AND Price&lt;=500) OR Name Contains laptop
    ///   Name StartsWith Apple AND Stock&gt;0
    /// </summary>
    public async Task<PagedResult<Product>> ExpressionSearchAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDesc = false)
    {
        const int MaxPageSize = 100;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        page = Math.Max(1, page);

        var operations = new GeneralOperationType(AllowedFilterFields);
        var parser = new LogicalParser<Product>(query, operations);
        var predicate = parser.Parse();

        var queryable = _products.AsQueryable().Where(predicate);

        if (sortBy is not null && AllowedFilterFields.Contains(sortBy))
        {
            var parameter = Expression.Parameter(typeof(Product), "p");
            var property = Expression.Property(parameter, sortBy);
            var boxed = Expression.Convert(property, typeof(object));
            var keySelector = Expression.Lambda<Func<Product, object>>(boxed, parameter);

            queryable = sortDesc
                ? queryable.OrderByDescending(keySelector)
                : queryable.OrderBy(keySelector);
        }

        var total = queryable.Count();
        var items = queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<Product>(items, page, pageSize, total);
    }

}
