using MongoApi.Models;
using MongoApi.Services;

namespace MongoApi.GraphQL;

public class ProductMutation
{
    /// <summary>
    /// Создаёт новый продукт.
    ///
    /// mutation {
    ///   createProduct(input: { name: "Laptop", price: 999.99, stock: 10, isAvailable: true, categoryId: "..." }) {
    ///     id name price
    ///   }
    /// }
    /// </summary>
    public async Task<Product> CreateProduct(Product input, [Service] ProductService service) =>
        await service.CreateAsync(input);

    /// <summary>
    /// Удаляет продукт по ID. Возвращает true если удалён.
    ///
    /// mutation { deleteProduct(id: "abc123") }
    /// </summary>
    public async Task<bool> DeleteProduct(string id, [Service] ProductService service) =>
        await service.DeleteAsync(id);
}
