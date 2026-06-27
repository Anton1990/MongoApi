using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _orgService;
    private readonly IProductService      _productService;
    private readonly ICurrentUserService  _currentUser;

    public OrganizationsController(
        IOrganizationService orgService,
        IProductService productService,
        ICurrentUserService currentUser)
    {
        _orgService     = orgService;
        _productService = productService;
        _currentUser    = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] QueryRequest request)
    {
        var userId = _currentUser.GetUserId();
        return Ok(await _orgService.GetForUserAsync(userId, request));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest request)
    {
        var userId = _currentUser.GetUserId();

        var org = new Organization
        {
            Name        = request.Name,
            Description = request.Description
        };

        var created = await _orgService.CreateAsync(org, userId, request.AdminRoleId);
        return CreatedAtAction(nameof(GetById), new { orgId = created.Id }, created);
    }

    [HttpGet("{orgId}")]
    [AuthorizeRole(ResourceType.Organization, "orgId", Roles.Member)]
    public async Task<IActionResult> GetById(string orgId)
    {
        return Ok(await _orgService.GetByIdAsync(orgId));
    }

    [HttpGet("{orgId}/members")]
    [AuthorizeRole(ResourceType.Organization, "orgId", Roles.Member)]
    public async Task<IActionResult> GetMembers(string orgId)
    {
        return Ok(await _orgService.GetMembersAsync(orgId));
    }

    [HttpPost("{orgId}/members")]
    [AuthorizeRole(ResourceType.Organization, "orgId", Roles.Admin)]
    public async Task<IActionResult> AddMember(string orgId, AddMemberRequest request)
    {
        await _orgService.AddMemberAsync(orgId, request.UserId, request.RoleId);
        return NoContent();
    }

    [HttpDelete("{orgId}/members/{userId}")]
    [AuthorizeRole(ResourceType.Organization, "orgId", Roles.Admin)]
    public async Task<IActionResult> RemoveMember(string orgId, string userId)
    {
        await _orgService.RemoveMemberAsync(orgId, userId);
        return NoContent();
    }

    /// <summary>Добавить продукт в организацию — только Admin.</summary>
    [HttpPost("{orgId}/products")]
    [AuthorizeRole(ResourceType.Organization, "orgId", Roles.Admin)]
    public async Task<IActionResult> AddProduct(string orgId, AddOrgProductRequest request)
    {
        var product = new Product
        {
            Name           = request.Name,
            Price          = request.Price,
            Stock          = request.Stock,
            CategoryId     = request.CategoryId,
            OrganizationId = orgId,
            Manufacturer   = request.ManufacturerName is not null
                ? new Manufacturer
                {
                    Name    = request.ManufacturerName,
                    Country = request.ManufacturerCountry ?? string.Empty
                }
                : null
        };

        var created = await _productService.CreateAsync(product);
        return CreatedAtAction(
            nameof(ProductsController.GetById),
            "Products",
            new { id = created.Id },
            created);
    }

    /// <summary>Список продуктов организации — только участники.</summary>
    [HttpGet("{orgId}/products")]
    [AuthorizeRole(ResourceType.Organization, "orgId", Roles.Member)]
    public async Task<IActionResult> GetProducts(string orgId, [FromQuery] QueryRequest request)
    {
        var result = await _productService.SearchAsync(request);
        var orgProducts = result.Items.Where(p => p.OrganizationId == orgId).ToList();
        return Ok(orgProducts);
    }

    /// <summary>
    /// Обновить продукт организации.
    /// Разрешено: Admin организации ИЛИ Admin самого продукта.
    /// </summary>
    [HttpPut("{orgId}/products/{productId}")]
    [AuthorizeAnyRole(
        ResourceType.Organization, "orgId",     Roles.Admin,
        ResourceType.Product,      "productId", Roles.Admin)]
    public async Task<IActionResult> UpdateProduct(
        string orgId,
        string productId,
        UpdateOrgProductRequest request)
    {
        var existing = await _productService.GetByIdAsync(productId)
            ?? throw new NotFoundException("Product", productId);

        if (existing.OrganizationId != orgId)
            return NotFound();

        var updated = new Product
        {
            Id             = existing.Id,
            OrganizationId = existing.OrganizationId,
            CreatedAt      = existing.CreatedAt,
            IsAvailable    = existing.IsAvailable,
            Version        = request.Version,
            Name           = request.Name,
            Price          = request.Price,
            Stock          = request.Stock,
            CategoryId     = request.CategoryId,
            Status         = request.Status,
            Manufacturer   = request.ManufacturerName is not null
                ? new Manufacturer
                {
                    Name    = request.ManufacturerName,
                    Country = request.ManufacturerCountry ?? string.Empty
                }
                : null
        };

        await _productService.UpdateAsync(productId, updated);
        return NoContent();
    }
}
