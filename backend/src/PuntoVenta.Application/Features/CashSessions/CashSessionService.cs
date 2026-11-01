using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Application.Features.CashSessions;

public record OpenCashSessionRequest(decimal OpeningAmount);
public record CloseCashSessionRequest(decimal CountedCash, string? Notes);
public record PaymentMethodTotal(PaymentMethod Method, decimal Amount);

public record CashSessionDto(
    int Id, int CashierId, string CashierName, CashSessionStatus Status,
    DateTime OpenedAt, DateTime? ClosedAt, decimal OpeningAmount,
    int SalesCount, int VoidedCount, decimal SalesTotal,
    IReadOnlyList<PaymentMethodTotal> PaymentsByMethod,
    decimal ExpectedCash, decimal? CountedCash, decimal? Difference, string? Notes);

public record CashSessionListItem(
    int Id, string CashierName, CashSessionStatus Status, DateTime OpenedAt, DateTime? ClosedAt,
    decimal OpeningAmount, decimal? ExpectedCash, decimal? CountedCash, decimal? Difference);

public class CashSessionQuery : PageQuery
{
    public CashSessionStatus? Status { get; set; }
    public int? CashierId { get; set; }
}

public class OpenCashSessionRequestValidator : AbstractValidator<OpenCashSessionRequest>
{
    public OpenCashSessionRequestValidator() =>
        RuleFor(x => x.OpeningAmount).GreaterThanOrEqualTo(0).LessThan(100_000).PrecisionScale(12, 2, true);
}

public class CloseCashSessionRequestValidator : AbstractValidator<CloseCashSessionRequest>
{
    public CloseCashSessionRequestValidator()
    {
        RuleFor(x => x.CountedCash).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, true);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class CashSessionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<OpenCashSessionRequest> openValidator,
    IValidator<CloseCashSessionRequest> closeValidator)
{
    public async Task<CashSessionDto> OpenAsync(OpenCashSessionRequest request, CancellationToken ct)
    {
        await openValidator.ValidateAndThrowAsync(request, ct);
        // Un cajero solo puede tener un turno abierto: así cada venta se imputa a un único arqueo.
        if (await db.CashSessions.AnyAsync(s => s.CashierId == currentUser.Id && s.Status == CashSessionStatus.Abierta, ct))
            throw new ConflictException("Ya tiene una caja abierta. Ciérrela antes de abrir otra.");

        var session = new CashSession(currentUser.Id, request.OpeningAmount);
        db.CashSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return await GetAsync(session.Id, ct);
    }

    public async Task<CashSessionDto?> GetCurrentAsync(CancellationToken ct)
    {
        var id = await db.CashSessions
            .Where(s => s.CashierId == currentUser.Id && s.Status == CashSessionStatus.Abierta)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
        return id is null ? null : await GetAsync(id.Value, ct);
    }

    public async Task<CashSessionDto> CloseAsync(int id, CloseCashSessionRequest request, CancellationToken ct)
    {
        await closeValidator.ValidateAndThrowAsync(request, ct);
        var session = await db.CashSessions.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Caja", id);
        EnsureCanAccess(session.CashierId);

        var sales = await LoadIssuedSales(id, ct);
        session.Close(request.CountedCash, sales.Sum(s => s.NetCash), request.Notes);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<CashSessionDto> GetAsync(int id, CancellationToken ct)
    {
        var session = await db.CashSessions.AsNoTracking().Include(s => s.Cashier)
            .FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Caja", id);
        EnsureCanAccess(session.CashierId);

        var issued = await LoadIssuedSales(id, ct);
        var voided = await db.Sales.CountAsync(s => s.CashSessionId == id && s.Status == SaleStatus.Anulada, ct);

        // El efectivo se reporta neto de vuelto: es lo que físicamente debe quedar en el cajón.
        var byMethod = Enum.GetValues<PaymentMethod>()
            .Select(m => new PaymentMethodTotal(m, m == PaymentMethod.Efectivo
                ? issued.Sum(s => s.NetCash)
                : issued.SelectMany(s => s.Payments).Where(p => p.Method == m).Sum(p => p.Amount)))
            .ToList();

        var expected = session.ExpectedCash ?? Money.Round(session.OpeningAmount + issued.Sum(s => s.NetCash));

        return new CashSessionDto(
            session.Id, session.CashierId, session.Cashier!.FullName, session.Status,
            session.OpenedAt, session.ClosedAt, session.OpeningAmount,
            issued.Count, voided, issued.Sum(s => s.Total), byMethod,
            expected, session.CountedCash, session.Difference, session.Notes);
    }

    public Task<PagedResult<CashSessionListItem>> ListAsync(CashSessionQuery query, CancellationToken ct)
    {
        var q = db.CashSessions.AsNoTracking();
        // El cajero solo ve sus propios turnos; el admin ve todos y puede filtrar por cajero.
        if (!currentUser.IsAdmin) q = q.Where(s => s.CashierId == currentUser.Id);
        else if (query.CashierId is { } cashierId) q = q.Where(s => s.CashierId == cashierId);
        if (query.Status is { } status) q = q.Where(s => s.Status == status);

        return q.ApplySort(query, new Dictionary<string, Expression<Func<CashSession, object>>>
        {
            ["openedAt"] = s => s.OpenedAt,
            ["closedAt"] = s => s.ClosedAt!,
        }, "openedAt").ToPagedAsync(query, s => new CashSessionListItem(
            s.Id, s.Cashier!.FullName, s.Status, s.OpenedAt, s.ClosedAt,
            s.OpeningAmount, s.ExpectedCash, s.CountedCash, s.Difference), ct);
    }

    private Task<List<Sale>> LoadIssuedSales(int sessionId, CancellationToken ct) =>
        db.Sales.AsNoTracking().Include(s => s.Payments)
            .Where(s => s.CashSessionId == sessionId && s.Status == SaleStatus.Emitida)
            .ToListAsync(ct);

    private void EnsureCanAccess(int cashierId)
    {
        if (!currentUser.IsAdmin && cashierId != currentUser.Id)
            throw new ForbiddenException("No puede operar la caja de otro cajero.");
    }
}
