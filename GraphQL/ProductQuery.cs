using HotChocolate.Data;
using MongoApi.Models;
using MongoApi.Services;

namespace MongoApi.GraphQL;

public class ProductQuery
{
    /// <summary>
    /// Возвращает список продуктов с фильтрацией, сортировкой и пагинацией.
    ///
    /// Пример запроса в Banana Cake Pop (/graphql):
    ///   query {
    ///     products(
    ///       where: { price: { gt: 100 }, isAvailable: { eq: true } }
    ///       order: { price: ASC }
    ///       first: 20
    ///     ) {
    ///       nodes { id name price stock }
    ///       pageInfo { hasNextPage endCursor }
    ///       totalCount
    ///     }
    ///   }
    /// </summary>
    [UseOffsetPaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Product> GetProducts([Service] ProductService service) =>
        service.GetQueryable();

    /// <summary>
    /// Возвращает один продукт по ID.
    ///
    /// query { product(id: "abc123") { name price } }
    /// </summary>
    public async Task<Product?> GetProduct(string id, [Service] ProductService service) =>
        await service.GetByIdAsync(id);
}
