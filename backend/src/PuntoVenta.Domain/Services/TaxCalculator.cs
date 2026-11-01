using PuntoVenta.Domain.Common;

namespace PuntoVenta.Domain.Services;

public readonly record struct TaxBreakdown(decimal Subtotal, decimal Igv, decimal Total);

/// <summary>
/// Cálculo del IGV (Impuesto General a las Ventas) del Perú: 18% (16% IGV + 2% IPM).
/// </summary>
public static class TaxCalculator
{
    public const decimal IgvRate = 0.18m;

    /// <summary>
    /// En retail los precios de góndola ya incluyen IGV, así que el impuesto se
    /// "extrae" del total: base = total / 1.18 y IGV = total - base.
    /// Se calcula el IGV como diferencia (y no base * 0.18) para garantizar que
    /// subtotal + IGV == total al céntimo, sin descuadres por redondeo.
    /// </summary>
    public static TaxBreakdown FromGrossTotal(decimal grossTotal)
    {
        if (grossTotal < 0) throw new DomainException("El total no puede ser negativo.");
        var total = Money.Round(grossTotal);
        var subtotal = Money.Round(total / (1 + IgvRate));
        return new TaxBreakdown(subtotal, total - subtotal, total);
    }
}
