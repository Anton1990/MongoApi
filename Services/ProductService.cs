using MongoDB.Bson;
using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Models.Dtos;

namespace MongoApi.Services;

public class ProductService
{
    private readonly IMongoCollection<Product> _products;

    public ProductService(MongoDbContext context)
    {
        _products = context.Products;
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
}
