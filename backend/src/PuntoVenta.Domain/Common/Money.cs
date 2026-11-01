namespace PuntoVenta.Domain.Common;

/// <summary>
/// Utilidades monetarias. Todo importe se redondea a 2 decimales con
/// MidpointRounding.AwayFromZero (redondeo comercial: 0.005 → 0.01), que es
/// el criterio que esperan contadores y SUNAT, y no el "bancario" por defecto de .NET.
/// </summary>
public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
