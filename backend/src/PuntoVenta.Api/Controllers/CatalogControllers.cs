using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PuntoVenta.Application.Common;
using PuntoVenta.Application.Features.Catalog;
using PuntoVenta.Application.Features.Customers;

namespace PuntoVenta.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Tags("Categorías")]
[Authorize]
public class CategoriesController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<List<CategoryDto>> List(CancellationToken ct) => catalog.ListCategoriesAsync(ct);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveCategoryRequest request, CancellationToken ct)
    {
        var category = await catalog.CreateCategoryAsync(request, ct);
        return Created($"/api/categories/{category.Id}", category);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<CategoryDto> Update(int id, SaveCategoryRequest request, CancellationToken ct) =>
        catalog.UpdateCategoryAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteCategoryAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/products")]
[Tags("Productos")]
[Authorize]
public class ProductsController(CatalogService catalog) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: search, categoryId, lowStock, includeInactive. Orden: name, sku, price, stock, createdAt.</summary>
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] ProductQuery query, CancellationToken ct) =>
        catalog.ListProductsAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<ProductDto> Get(int id, CancellationToken ct) => catalog.GetProductAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await catalog.CreateProductAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<ProductDto> Update(int id, UpdateProductRequest request, CancellationToken ct) =>
        catalog.UpdateProductAsync(id, request, ct);

    /// <summary>Baja lógica (el producto queda inactivo, no se borra).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await catalog.DeactivateProductAsync(id, ct);
        return NoContent();
    }

    /// <summary>Ajuste manual de stock (+ ingreso / − merma).</summary>
    [HttpPost("{id:int}/stock-adjustments")]
    [Authorize(Roles = Roles.Admin)]
    public Task<ProductDto> AdjustStock(int id, AdjustStockRequest request, CancellationToken ct) =>
        catalog.AdjustStockAsync(id, request, ct);
}

[ApiController]
[Route("api/customers")]
[Tags("Clientes")]
[Authorize]
public class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerDto>> List([FromQuery] CustomerQuery query, CancellationToken ct) =>
        customers.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<CustomerDto> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);

    [HttpGet("by-document/{docNumber}")]
    public Task<CustomerDto> GetByDocument(string docNumber, CancellationToken ct) =>
        customers.GetByDocumentAsync(docNumber, ct);

    /// <summary>El cajero puede registrar clientes al vuelo durante una venta.</summary>
    [HttpPost]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    public Task<CustomerDto> Update(int id, UpdateCustomerRequest request, CancellationToken ct) =>
        customers.UpdateAsync(id, request, ct);
}
