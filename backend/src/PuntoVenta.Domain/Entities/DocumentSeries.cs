using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Domain.Entities;

/// <summary>
/// Serie de comprobantes (p. ej. B001 para boletas, F001 para facturas) y su último
/// correlativo usado. SUNAT exige que la numeración sea consecutiva y sin huecos por serie,
/// por eso el siguiente número se reserva de forma atómica dentro de la transacción de venta.
/// </summary>
public class DocumentSeries
{
    public int Id { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string Code { get; private set; } = null!;
    public int LastNumber { get; private set; }
    public bool IsActive { get; private set; } = true;

    private DocumentSeries() { }

    public DocumentSeries(DocumentType type, string code, int lastNumber = 0)
    {
        DocumentType = type;
        Code = code;
        LastNumber = lastNumber;
    }

    /// <summary>Formato oficial: SERIE-CORRELATIVO con 8 dígitos (B001-00000123).</summary>
    public static string Format(string series, int number) => $"{series}-{number:D8}";
}
