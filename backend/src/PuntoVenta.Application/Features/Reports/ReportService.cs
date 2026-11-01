using Microsoft.EntityFrameworkCore;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Application.Features.Reports;

public record DailySales(DateOnly Date, int Tickets, decimal Subtotal, decimal Igv, decimal Total);
public record ProductSales(int ProductId, string ProductName, int Quantity, decimal Amount);
public record CashierSales(int CashierId, string CashierName, int Tickets, decimal Total, decimal AverageTicket);
public record DashboardDto(
    decimal SalesToday, int TicketsToday, decimal AverageTicketToday,
    decimal SalesMonth, int LowStockProducts, int OpenCashSessions,
    IReadOnlyList<DailySales> Last7Days, IReadOnlyList<ProductSales> TopProducts);

public class DateRangeQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class ReportService(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>Límite del rango para que un reporte no recorra años de ventas en una sola petición.</summary>
    public const int MaxRangeDays = 366;

    private static (DateTime FromUtc, DateTime ToUtc) Resolve(DateRangeQuery range)
    {
        var to = range.To ?? LimaTime.Today();
        var from = range.From ?? to.AddDays(-29);
        if (from > to) throw new DomainException("La fecha inicial no puede ser posterior a la final.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            throw new DomainException($"El rango máximo de un reporte es {MaxRangeDays} días.");
        return (LimaTime.StartOfDayUtc(from), LimaTime.StartOfDayUtc(to.AddDays(1)));
    }

    /// <summary>Solo cuentan comprobantes emitidos: los anulados no son ingreso.</summary>
    private IQueryable<Domain.Entities.Sale> IssuedSales(DateTime fromUtc, DateTime toUtc, bool onlyMine)
    {
        var q = db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Emitida && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc);
        return onlyMine ? q.Where(s => s.CashierId == currentUser.Id) : q;
    }

    public async Task<IReadOnlyList<DailySales>> SalesByDayAsync(DateRangeQuery range, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Resolve(range);
        return await SalesByDayInternal(fromUtc, toUtc, onlyMine: false, ct);
    }

    private async Task<IReadOnlyList<DailySales>> SalesByDayInternal(DateTime fromUtc, DateTime toUtc, bool onlyMine, CancellationToken ct)
    {
        // Se agrupa en memoria porque el "día" depende de la hora de Lima (UTC−5) y la
        // función de fecha difiere entre PostgreSQL y SQLite. El rango está acotado (≤ 366 días)
        // y solo se traen 4 columnas, así que el volumen es razonable.
        var rows = await IssuedSales(fromUtc, toUtc, onlyMine)
            .Select(s => new { s.CreatedAt, s.Subtotal, s.Igv, s.Total })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => LimaTime.ToLimaDate(r.CreatedAt))
            .Select(g => new DailySales(g.Key, g.Count(), g.Sum(x => x.Subtotal), g.Sum(x => x.Igv), g.Sum(x => x.Total)))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<ProductSales>> SalesByProductAsync(DateRangeQuery range, int top, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Resolve(range);
        return await SalesByProductInternal(fromUtc, toUtc, Math.Clamp(top, 1, 100), false, ct);
    }

    private async Task<IReadOnlyList<ProductSales>> SalesByProductInternal(DateTime fromUtc, DateTime toUtc, int top, bool onlyMine, CancellationToken ct)
    {
        var saleIds = IssuedSales(fromUtc, toUtc, onlyMine).Select(s => s.Id);
        // Importe por producto = suma de líneas (precio con IGV). No prorratea el descuento global
        // del ticket: es la vista comercial de "cuánto se vendió de cada producto".
        var rows = await db.SaleItems.AsNoTracking()
            .Where(i => saleIds.Contains(i.SaleId))
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new { g.Key.ProductId, g.Key.ProductName, Quantity = g.Sum(x => x.Quantity), Amount = g.Sum(x => x.LineTotal) })
            .ToListAsync(ct);

        // Un producto renombrado aparece con dos nombres en el histórico: se consolida por Id.
        return rows.GroupBy(r => r.ProductId)
            .Select(g => new ProductSales(g.Key, g.Last().ProductName, g.Sum(x => x.Quantity), Money.Round(g.Sum(x => x.Amount))))
            .OrderByDescending(p => p.Amount)
            .Take(top)
            .ToList();
    }

    public async Task<IReadOnlyList<CashierSales>> SalesByCashierAsync(DateRangeQuery range, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Resolve(range);
        var rows = await IssuedSales(fromUtc, toUtc, false)
            .GroupBy(s => new { s.CashierId, s.Cashier!.FullName })
            .Select(g => new { g.Key.CashierId, g.Key.FullName, Tickets = g.Count(), Total = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        return rows
            .Select(r => new CashierSales(r.CashierId, r.FullName, r.Tickets, Money.Round(r.Total),
                r.Tickets == 0 ? 0 : Money.Round(r.Total / r.Tickets)))
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    /// <summary>KPIs de inicio. El cajero ve solo sus cifras; el admin, las de toda la tienda.</summary>
    public async Task<DashboardDto> DashboardAsync(CancellationToken ct)
    {
        var onlyMine = !currentUser.IsAdmin;
        var today = LimaTime.Today();
        var todayStart = LimaTime.StartOfDayUtc(today);
        var tomorrow = LimaTime.StartOfDayUtc(today.AddDays(1));
        var monthStart = LimaTime.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1));

        var last7 = await SalesByDayInternal(LimaTime.StartOfDayUtc(today.AddDays(-6)), tomorrow, onlyMine, ct);
        var todayRow = last7.FirstOrDefault(d => d.Date == today);
        var monthTotals = await IssuedSales(monthStart, tomorrow, onlyMine).Select(s => s.Total).ToListAsync(ct);

        // Se rellenan los días sin ventas con cero para que el gráfico tenga siempre 7 barras.
        var series = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(i - 6))
            .Select(d => last7.FirstOrDefault(x => x.Date == d) ?? new DailySales(d, 0, 0, 0, 0))
            .ToList();

        var tickets = todayRow?.Tickets ?? 0;
        var salesToday = todayRow?.Total ?? 0;

        return new DashboardDto(
            salesToday, tickets, tickets == 0 ? 0 : Money.Round(salesToday / tickets),
            monthTotals.Sum(),
            await db.Products.CountAsync(p => p.IsActive && p.Stock <= p.MinStock, ct),
            await db.CashSessions.CountAsync(s => s.Status == CashSessionStatus.Abierta, ct),
            series,
            await SalesByProductInternal(LimaTime.StartOfDayUtc(today.AddDays(-29)), tomorrow, 5, onlyMine, ct));
    }
}
