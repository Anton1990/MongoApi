using Microsoft.AspNetCore.Mvc;
using MongoApi.Models;
using MongoApi.Models.Dtos;
using MongoApi.Services;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll() =>
        Ok(await _productService.GetAllAsync());

    /// <summary>
    /// Поиск через строку-выражение (Expression Tree) с пагинацией, сортировкой и проекцией полей.
    /// Поля: Name, Price, Stock, IsAvailable, CategoryId, CreatedAt
    /// Операторы: ==, !=, &lt;, &gt;, &lt;=, &gt;=, Contains, StartsWith, EndsWith
    /// Логика: AND, OR, скобки ()
    ///
    /// GET /api/products/query?q=Price>100&amp;page=1&amp;pageSize=20
    /// GET /api/products/query?q=Price>100&amp;sortBy=Price&amp;sortDesc=true
    /// GET /api/products/query?q=(Price>=50 AND Price&lt;=500) OR Name Contains laptop&amp;page=2&amp;pageSize=50
    /// GET /api/products/query?q=Price>100&amp;fields=Name,Price,Stock
    /// </summary>
    [HttpGet("query")]
    public async Task<IActionResult> Query(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _productService.ExpressionSearchAsync(q, page, pageSize, sortBy, sortDesc);

            if (fields is null)
                return Ok(result);

            // Проекция: возвращаем только запрошенные поля
            var fieldSet = fields
                .Split(',')
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var projected = result.Items
                .Select(p => ProjectFields(p, fieldSet))
                .ToList();

            return Ok(new { items = projected, page = result.Page, pageSize = result.PageSize, total = result.Total });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Возвращает только запрошенные поля продукта в виде словаря.
    /// Неизвестные поля игнорируются.
    /// </summary>
    private static Dictionary<string, object?> ProjectFields(Product product, HashSet<string> fields)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in typeof(Product).GetProperties())
        {
            if (fields.Contains(prop.Name))
                dict[prop.Name] = prop.GetValue(product);
        }
        return dict;
    }

    /// <summary>
    /// Курсорная пагинация.
    /// Первый запрос:            GET /api/products/paged?pageSize=20
    /// Следующие страницы:       GET /api/products/paged?cursor=&lt;nextCursor&gt;&amp;pageSize=20
    /// Когда hasMore = false — данные закончились.
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<CursorPageResult<Product>>> GetPaged(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20) =>
        Ok(await _productService.GetPageAsync(cursor, pageSize));

    /// <summary>
    /// Возвращает продукты с данными категории ($lookup).
    /// Аналог: SELECT * FROM products LEFT JOIN categories ON categoryId = _id
    /// </summary>
    [HttpGet("with-category")]
    public async Task<ActionResult<List<ProductWithCategoryDto>>> GetAllWithCategory() =>
        Ok(await _productService.GetAllWithCategoryAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetById(string id)
    {
        var product = await _productService.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(Product product)
    {
        var created = await _productService.CreateAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Product product)
    {
        try
        {
            await _productService.UpdateAsync(id, product);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Infrastructure.ConcurrencyException ex)
        {
            // 409 Conflict — документ изменён другим запросом
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _productService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
