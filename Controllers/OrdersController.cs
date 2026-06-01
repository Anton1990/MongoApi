using Microsoft.AspNetCore.Mvc;
using MongoApi.Models;
using MongoApi.Services;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetAll() =>
        Ok(await _orderService.GetAllAsync());

    /// <summary>
    /// Создаёт заказ с ACID-транзакцией.
    /// Атомарно: создаёт заказ + уменьшает stock продукта.
    /// Если stock недостаточен — 400 Bad Request, ничего не изменится.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Order>> Create(
        [FromQuery] string productId,
        [FromQuery] int quantity)
    {
        try
        {
            var order = await _orderService.CreateAsync(productId, quantity);
            return CreatedAtAction(nameof(GetAll), new { id = order.Id }, order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
