using Microsoft.AspNetCore.Mvc;
using MongoApi.Models;
using MongoApi.Services;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Customer>>> GetAll() =>
        Ok(await _customerService.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Customer>> GetById(string id)
    {
        var customer = await _customerService.GetByIdAsync(id);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> Create(Customer customer)
    {
        var created = await _customerService.CreateAsync(customer);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Customer customer)
    {
        var updated = await _customerService.UpdateAsync(id, customer);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _customerService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
