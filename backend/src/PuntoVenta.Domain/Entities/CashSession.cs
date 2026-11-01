using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Domain.Entities;

/// <summary>
/// Turno de caja de un cajero. Toda venta pertenece a una sesión abierta; al cerrar
/// se hace el arqueo: se compara el efectivo contado físicamente con el esperado.
/// </summary>
public class CashSession
{
    public int Id { get; private set; }
    public int CashierId { get; private set; }
    public User? Cashier { get; private set; }
    public CashSessionStatus Status { get; private set; } = CashSessionStatus.Abierta;
    public DateTime OpenedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; private set; }

    /// <summary>Fondo de caja (sencillo para dar vuelto) con el que se abre el turno.</summary>
    public decimal OpeningAmount { get; private set; }

    /// <summary>Efectivo que el sistema espera encontrar al cierre (fondo + ventas en efectivo).</summary>
    public decimal? ExpectedCash { get; private set; }

    /// <summary>Efectivo contado por el cajero al cierre.</summary>
    public decimal? CountedCash { get; private set; }

    /// <summary>Contado − esperado. Positivo = sobrante, negativo = faltante.</summary>
    public decimal? Difference { get; private set; }
    public string? Notes { get; private set; }

    public List<Sale> Sales { get; private set; } = [];

    private CashSession() { }

    public CashSession(int cashierId, decimal openingAmount)
    {
        if (openingAmount < 0) throw new DomainException("El fondo de caja no puede ser negativo.");
        CashierId = cashierId;
        OpeningAmount = Money.Round(openingAmount);
    }

    public bool IsOpen => Status == CashSessionStatus.Abierta;

    /// <param name="netCashSales">Efectivo neto cobrado en ventas emitidas (ya descontado el vuelto).</param>
    public void Close(decimal countedCash, decimal netCashSales, string? notes)
    {
        if (!IsOpen) throw new DomainException("La caja ya está cerrada.", "caja_cerrada");
        if (countedCash < 0) throw new DomainException("El efectivo contado no puede ser negativo.");

        Status = CashSessionStatus.Cerrada;
        ClosedAt = DateTime.UtcNow;
        CountedCash = Money.Round(countedCash);
        ExpectedCash = Money.Round(OpeningAmount + netCashSales);
        Difference = CountedCash - ExpectedCash;
        Notes = notes?.Trim();
    }
}
