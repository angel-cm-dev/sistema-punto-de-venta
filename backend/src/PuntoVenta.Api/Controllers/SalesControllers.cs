using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PuntoVenta.Application.Common;
using PuntoVenta.Application.Features.CashSessions;
using PuntoVenta.Application.Features.Reports;
using PuntoVenta.Application.Features.Sales;

namespace PuntoVenta.Api.Controllers;

[ApiController]
[Route("api/cash-sessions")]
[Tags("Caja")]
[Authorize]
public class CashSessionsController(CashSessionService cash) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CashSessionListItem>> List([FromQuery] CashSessionQuery query, CancellationToken ct) =>
        cash.ListAsync(query, ct);

    /// <summary>Caja abierta del usuario actual (204 si no tiene ninguna).</summary>
    [HttpGet("current")]
    [ProducesResponseType<CashSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var session = await cash.GetCurrentAsync(ct);
        return session is null ? NoContent() : Ok(session);
    }

    [HttpGet("{id:int}")]
    public Task<CashSessionDto> Get(int id, CancellationToken ct) => cash.GetAsync(id, ct);

    [HttpPost("open")]
    [ProducesResponseType<CashSessionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Open(OpenCashSessionRequest request, CancellationToken ct)
    {
        var session = await cash.OpenAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    /// <summary>Cierre con arqueo: compara efectivo contado vs. esperado.</summary>
    [HttpPost("{id:int}/close")]
    public Task<CashSessionDto> Close(int id, CloseCashSessionRequest request, CancellationToken ct) =>
        cash.CloseAsync(id, request, ct);
}

[ApiController]
[Route("api/sales")]
[Tags("Ventas")]
[Authorize]
public class SalesController(SaleService sales) : ControllerBase
{
    /// <summary>Lista paginada. El cajero solo ve sus ventas. Filtros: from, to (yyyy-MM-dd, hora Lima), status, documentType, cashierId, search.</summary>
    [HttpGet]
    public Task<PagedResult<SaleListItem>> List([FromQuery] SaleQuery query, CancellationToken ct) =>
        sales.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<SaleDto> Get(int id, CancellationToken ct) => sales.GetAsync(id, ct);

    /// <summary>Registra una venta: descuenta stock, asigna serie-correlativo y calcula IGV en una transacción.</summary>
    [HttpPost]
    [ProducesResponseType<SaleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateSaleRequest request, CancellationToken ct)
    {
        var sale = await sales.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = sale.Id }, sale);
    }

    /// <summary>Anula el comprobante y repone el stock. Solo administradores.</summary>
    [HttpPost("{id:int}/void")]
    [Authorize(Roles = Roles.Admin)]
    public Task<SaleDto> Void(int id, VoidSaleRequest request, CancellationToken ct) => sales.VoidAsync(id, request, ct);
}

[ApiController]
[Route("api/reports")]
[Tags("Reportes")]
[Authorize]
public class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>KPIs del inicio (el cajero ve solo sus cifras).</summary>
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => reports.DashboardAsync(ct);

    [HttpGet("sales-by-day")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<DailySales>> ByDay([FromQuery] DateRangeQuery range, CancellationToken ct) =>
        reports.SalesByDayAsync(range, ct);

    [HttpGet("sales-by-product")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<ProductSales>> ByProduct([FromQuery] DateRangeQuery range, CancellationToken ct, [FromQuery] int top = 10) =>
        reports.SalesByProductAsync(range, top, ct);

    [HttpGet("sales-by-cashier")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<CashierSales>> ByCashier([FromQuery] DateRangeQuery range, CancellationToken ct) =>
        reports.SalesByCashierAsync(range, ct);
}
