using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;

namespace PuntoVenta.Application.Features.Catalog;

public record CategoryDto(int Id, string Name, string? Description, int ProductCount);
public record SaveCategoryRequest(string Name, string? Description);

public record ProductDto(
    int Id, string Sku, string Name, int CategoryId, string CategoryName,
    decimal Price, int Stock, int MinStock, bool IsLowStock, bool IsActive);

public record CreateProductRequest(string Sku, string Name, int CategoryId, decimal Price, int Stock, int MinStock);
public record UpdateProductRequest(string Name, int CategoryId, decimal Price, int MinStock, bool IsActive);
public record AdjustStockRequest(int Delta, string Reason);

public class ProductQuery : PageQuery
{
    public int? CategoryId { get; set; }
    public bool? LowStock { get; set; }
    /// <summary>Por defecto solo activos; el admin puede pedir includeInactive=true.</summary>
    public bool IncludeInactive { get; set; }
}

public class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("El SKU solo admite letras, números y guiones.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0).LessThan(1_000_000).PrecisionScale(12, 2, true);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0).LessThan(1_000_000).PrecisionScale(12, 2, true);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
    }
}

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.Delta).NotEqual(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}

public class CatalogService(
    IAppDbContext db,
    IValidator<SaveCategoryRequest> categoryValidator,
    IValidator<CreateProductRequest> createProductValidator,
    IValidator<UpdateProductRequest> updateProductValidator,
    IValidator<AdjustStockRequest> adjustValidator)
{
    // ---------- Categorías ----------

    public Task<List<CategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count(p => p.IsActive)))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        await EnsureUniqueCategoryName(request.Name, null, ct);
        var category = new Category(request.Name, request.Description);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Description, 0);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken ct)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        var category = await db.Categories.FindAsync([id], ct) ?? throw new NotFoundException("Categoría", id);
        await EnsureUniqueCategoryName(request.Name, id, ct);
        category.Update(request.Name, request.Description);
        await db.SaveChangesAsync(ct);
        var count = await db.Products.CountAsync(p => p.CategoryId == id && p.IsActive, ct);
        return new CategoryDto(category.Id, category.Name, category.Description, count);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct) ?? throw new NotFoundException("Categoría", id);
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new ConflictException("No se puede eliminar una categoría que tiene productos asociados.");
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueCategoryName(string name, int? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == normalized && c.Id != excludeId, ct))
            throw new ConflictException($"Ya existe la categoría '{name.Trim()}'.");
    }

    // ---------- Productos ----------

    private static readonly Dictionary<string, Expression<Func<Product, object>>> ProductSorts = new()
    {
        ["name"] = p => p.Name,
        ["sku"] = p => p.Sku,
        ["price"] = p => p.Price,
        ["stock"] = p => p.Stock,
        ["createdAt"] = p => p.CreatedAt,
    };

    private static readonly Expression<Func<Product, ProductDto>> ProductProjection = p => new ProductDto(
        p.Id, p.Sku, p.Name, p.CategoryId, p.Category!.Name, p.Price, p.Stock, p.MinStock,
        p.Stock <= p.MinStock, p.IsActive);

    public Task<PagedResult<ProductDto>> ListProductsAsync(ProductQuery query, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking();
        if (!query.IncludeInactive) q = q.Where(p => p.IsActive);
        if (query.CategoryId is { } categoryId) q = q.Where(p => p.CategoryId == categoryId);
        if (query.LowStock == true) q = q.Where(p => p.Stock <= p.MinStock);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(s) || p.Sku.ToLower().Contains(s));
        }
        return q.ApplySort(query, ProductSorts, "name").ToPagedAsync(query, ProductProjection, ct);
    }

    public async Task<ProductDto> GetProductAsync(int id, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id).Select(ProductProjection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Producto", id);

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct)
    {
        await createProductValidator.ValidateAndThrowAsync(request, ct);
        await EnsureCategoryExists(request.CategoryId, ct);
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"El SKU {sku} ya está registrado.");

        var product = new Product(sku, request.Name, request.CategoryId, request.Price, request.Stock, request.MinStock);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken ct)
    {
        await updateProductValidator.ValidateAndThrowAsync(request, ct);
        var product = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Producto", id);
        await EnsureCategoryExists(request.CategoryId, ct);
        product.Update(request.Name, request.CategoryId, request.Price, request.MinStock);
        if (request.IsActive) product.Activate(); else product.Deactivate();
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(id, ct);
    }

    public async Task DeactivateProductAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Producto", id);
        product.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductDto> AdjustStockAsync(int id, AdjustStockRequest request, CancellationToken ct)
    {
        await adjustValidator.ValidateAndThrowAsync(request, ct);
        var product = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Producto", id);
        product.AdjustStock(request.Delta);
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(id, ct);
    }

    private async Task EnsureCategoryExists(int categoryId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
            throw new DomainException($"La categoría {categoryId} no existe.", "categoria_inexistente");
    }
}
