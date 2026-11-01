using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;
using PuntoVenta.Domain.Services;

namespace PuntoVenta.Application.Features.Customers;

public record CustomerDto(
    int Id, IdentityDocType DocType, string DocNumber, string Name,
    string? Email, string? Phone, string? Address, DateTime CreatedAt);

public record CreateCustomerRequest(IdentityDocType DocType, string DocNumber, string Name, string? Email, string? Phone, string? Address);
public record UpdateCustomerRequest(string Name, string? Email, string? Phone, string? Address);

public class CustomerQuery : PageQuery
{
    public IdentityDocType? DocType { get; set; }
}

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.DocType).IsInEnum();
        RuleFor(x => x.DocNumber).NotEmpty()
            .Must((req, doc) => IdentityDocumentValidator.IsValid(req.DocType, doc))
            .WithMessage(req => req.DocType == IdentityDocType.Ruc
                ? "RUC inválido: debe tener 11 dígitos, prefijo 10/15/17/20 y dígito verificador correcto."
                : "DNI inválido: debe tener 8 dígitos.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(250);
    }
}

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(250);
    }
}

public class CustomerService(
    IAppDbContext db,
    IValidator<CreateCustomerRequest> createValidator,
    IValidator<UpdateCustomerRequest> updateValidator)
{
    private static readonly Expression<Func<Customer, CustomerDto>> Projection = c =>
        new CustomerDto(c.Id, c.DocType, c.DocNumber, c.Name, c.Email, c.Phone, c.Address, c.CreatedAt);

    public Task<PagedResult<CustomerDto>> ListAsync(CustomerQuery query, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking();
        if (query.DocType is { } type) q = q.Where(c => c.DocType == type);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s) || c.DocNumber.Contains(s));
        }
        return q.ApplySort(query, new Dictionary<string, Expression<Func<Customer, object>>>
        {
            ["name"] = c => c.Name,
            ["docNumber"] = c => c.DocNumber,
            ["createdAt"] = c => c.CreatedAt,
        }, "name").ToPagedAsync(query, Projection, ct);
    }

    public async Task<CustomerDto> GetAsync(int id, CancellationToken ct) =>
        await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Cliente", id);

    /// <summary>Búsqueda rápida por documento desde la pantalla de venta.</summary>
    public async Task<CustomerDto> GetByDocumentAsync(string docNumber, CancellationToken ct) =>
        await db.Customers.AsNoTracking().Where(c => c.DocNumber == docNumber).Select(Projection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Cliente con documento", docNumber);

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        if (await db.Customers.AnyAsync(c => c.DocNumber == request.DocNumber, ct))
            throw new ConflictException($"Ya existe un cliente con documento {request.DocNumber}.");

        var customer = new Customer(request.DocType, request.DocNumber, request.Name, request.Email, request.Phone, request.Address);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return await GetAsync(customer.Id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(int id, UpdateCustomerRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var customer = await db.Customers.FindAsync([id], ct) ?? throw new NotFoundException("Cliente", id);
        // El documento no se edita: cambiarlo alteraría a quién se emitieron los comprobantes previos.
        customer.Update(request.Name, request.Email, request.Phone, request.Address);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }
}
