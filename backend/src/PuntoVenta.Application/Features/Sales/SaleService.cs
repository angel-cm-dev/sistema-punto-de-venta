using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Application.Features.Sales;

public record SaleItemRequest(int ProductId, int Quantity);
public record PaymentRequest(PaymentMethod Method, decimal Amount, string? Reference);
public record CreateSaleRequest(
    DocumentType DocumentType,
    int? CustomerId,
    List<SaleItemRequest> Items,
    List<PaymentRequest> Payments,
    decimal Discount = 0);
public record VoidSaleRequest(string Reason);

public record SaleItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public record PaymentDto(PaymentMethod Method, decimal Amount, string? Reference);
public record SaleDto(
    int Id, DocumentType DocumentType, string DocumentNumber, SaleStatus Status, DateTime CreatedAt,
    int? CustomerId, string? CustomerName, string? CustomerDocNumber,
    int CashierId, string CashierName, int CashSessionId,
    decimal GrossAmount, decimal Discount, decimal Subtotal, decimal Igv, decimal Total,
    decimal AmountPaid, decimal Change,
    IReadOnlyList<SaleItemDto> Items, IReadOnlyList<PaymentDto> Payments,
    DateTime? VoidedAt, string? VoidReason);

public record SaleListItem(
    int Id, DocumentType DocumentType, string Series, int Number, SaleStatus Status, DateTime CreatedAt,
    string? CustomerName, string CashierName, decimal Total)
{
    public string DocumentNumber => DocumentSeries.Format(Series, Number);
}

public class SaleQuery : PageQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public SaleStatus? Status { get; set; }
    public DocumentType? DocumentType { get; set; }
    public int? CashierId { get; set; }
}

public class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.Items).NotEmpty().WithMessage("La venta debe tener al menos un ítem.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ProductId).GreaterThan(0);
            i.RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(10_000);
        });
        RuleFor(x => x.Payments).NotEmpty().WithMessage("Debe registrar al menos un medio de pago.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Method).IsInEnum();
            p.RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(12, 2, true);
            p.RuleFor(x => x.Reference).MaximumLength(60);
        });
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, true);
    }
}

public class VoidSaleRequestValidator : AbstractValidator<VoidSaleRequest>
{
    public VoidSaleRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(250);
}

public class SaleService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<CreateSaleRequest> createValidator,
    IValidator<VoidSaleRequest> voidValidator)
{
    /// <summary>
    /// Tope de descuento para el cajero (sobre el importe bruto). Descuentos mayores requieren
    /// a un administrador: es un control antifraude típico de retail.
    /// </summary>
    public const decimal CashierMaxDiscountRate = 0.10m;

    public async Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var session = await db.CashSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CashierId == currentUser.Id && s.Status == CashSessionStatus.Abierta, ct)
            ?? throw new DomainException("Debe abrir caja antes de registrar ventas.", "caja_no_abierta");

        // Si el mismo producto viene en varias líneas se consolida en una sola.
        var requested = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();
        var ids = requested.Select(r => r.ProductId).ToList();

        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id, ct);
        var missing = ids.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Productos inexistentes o inactivos: {string.Join(", ", missing)}.", "producto_invalido");

        Customer? customer = null;
        if (request.CustomerId is { } customerId)
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new DomainException($"El cliente {customerId} no existe.", "cliente_invalido");

        // El precio se toma SIEMPRE de la base de datos, nunca del cliente HTTP:
        // de lo contrario cualquiera podría manipular el precio desde el navegador.
        var lines = requested
            .Select(r => new SaleLineInput(r.ProductId, products[r.ProductId].Name, r.Quantity, products[r.ProductId].Price))
            .ToList();

        var gross = lines.Sum(l => Money.Round(l.UnitPrice * l.Quantity));
        if (!currentUser.IsAdmin && request.Discount > Money.Round(gross * CashierMaxDiscountRate))
            throw new DomainException(
                $"El cajero puede aplicar como máximo {CashierMaxDiscountRate:P0} de descuento.", "descuento_excedido");

        var sale = Sale.Create(
            request.DocumentType, customer, currentUser.Id, session.Id, lines, request.Discount,
            request.Payments.Select(p => new PaymentInput(p.Method, p.Amount, p.Reference)).ToList());

        // ---- Sección transaccional: stock + correlativo + comprobante se confirman juntos o nada ----
        await using var tx = await db.BeginTransactionAsync(ct);

        foreach (var line in lines)
        {
            // UPDATE condicional atómico: "resta solo si alcanza". Evita la carrera clásica de
            // leer stock → validar → escribir, donde dos cajas venden la última unidad a la vez.
            var affected = await db.Products
                .Where(p => p.Id == line.ProductId && p.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - line.Quantity), ct);
            if (affected == 0)
                throw new DomainException(
                    $"Stock insuficiente para '{line.ProductName}' (disponible: {products[line.ProductId].Stock}).",
                    "stock_insuficiente");
        }

        var series = await db.DocumentSeries.AsNoTracking()
            .Where(s => s.DocumentType == request.DocumentType && s.IsActive)
            .OrderBy(s => s.Code)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException($"No hay serie activa para {request.DocumentType}.", "serie_inexistente");

        // El UPDATE bloquea la fila de la serie hasta el COMMIT: las ventas concurrentes de la
        // misma serie se serializan y el correlativo nunca se repite ni deja huecos.
        await db.DocumentSeries.Where(s => s.Id == series.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastNumber, x => x.LastNumber + 1), ct);
        var number = await db.DocumentSeries.Where(s => s.Id == series.Id).Select(s => s.LastNumber).SingleAsync(ct);

        sale.AssignNumber(series.Code, number);
        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(sale.Id, ct);
    }

    public async Task<SaleDto> VoidAsync(int id, VoidSaleRequest request, CancellationToken ct)
    {
        await voidValidator.ValidateAndThrowAsync(request, ct);
        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Venta", id);

        sale.Void(currentUser.Id, request.Reason);

        await using var tx = await db.BeginTransactionAsync(ct);
        // La anulación devuelve al almacén lo vendido; el comprobante se conserva (no se borra)
        // porque la numeración SUNAT no puede tener huecos: queda con estado "Anulada".
        foreach (var item in sale.Items)
        {
            await db.Products.Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity), ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<SaleDto> GetAsync(int id, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Items).Include(s => s.Payments)
            .Include(s => s.Customer).Include(s => s.Cashier)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Venta", id);

        if (!currentUser.IsAdmin && sale.CashierId != currentUser.Id)
            throw new ForbiddenException("Solo puede consultar sus propias ventas.");

        return new SaleDto(
            sale.Id, sale.DocumentType, sale.DocumentNumber, sale.Status, sale.CreatedAt,
            sale.CustomerId, sale.Customer?.Name, sale.Customer?.DocNumber,
            sale.CashierId, sale.Cashier!.FullName, sale.CashSessionId,
            sale.GrossAmount, sale.Discount, sale.Subtotal, sale.Igv, sale.Total, sale.AmountPaid, sale.Change,
            sale.Items.OrderBy(i => i.Id).Select(i => new SaleItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.LineTotal)).ToList(),
            sale.Payments.OrderBy(p => p.Id).Select(p => new PaymentDto(p.Method, p.Amount, p.Reference)).ToList(),
            sale.VoidedAt, sale.VoidReason);
    }

    public Task<PagedResult<SaleListItem>> ListAsync(SaleQuery query, CancellationToken ct)
    {
        var q = db.Sales.AsNoTracking();
        if (!currentUser.IsAdmin) q = q.Where(s => s.CashierId == currentUser.Id);
        else if (query.CashierId is { } cashierId) q = q.Where(s => s.CashierId == cashierId);

        if (query.From is { } from) { var f = LimaTime.StartOfDayUtc(from); q = q.Where(s => s.CreatedAt >= f); }
        if (query.To is { } to) { var t = LimaTime.StartOfDayUtc(to.AddDays(1)); q = q.Where(s => s.CreatedAt < t); }
        if (query.Status is { } status) q = q.Where(s => s.Status == status);
        if (query.DocumentType is { } type) q = q.Where(s => s.DocumentType == type);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x => x.Series.ToLower().Contains(s)
                             || (x.Customer != null && (x.Customer.Name.ToLower().Contains(s) || x.Customer.DocNumber.Contains(s))));
        }

        return q.ApplySort(query, new Dictionary<string, Expression<Func<Sale, object>>>
        {
            ["createdAt"] = s => s.CreatedAt,
            ["total"] = s => s.Total,
            ["number"] = s => s.Number,
        }, "createdAt").ToPagedAsync(query, s => new SaleListItem(
            s.Id, s.DocumentType, s.Series, s.Number, s.Status, s.CreatedAt,
            s.Customer != null ? s.Customer.Name : null, s.Cashier!.FullName, s.Total), ct);
    }
}
