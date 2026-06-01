using Microsoft.AspNetCore.Mvc;
using MongoApi.Infrastructure;
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
        catch (ConcurrencyException ex)
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
