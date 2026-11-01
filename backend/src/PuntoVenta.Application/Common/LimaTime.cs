namespace PuntoVenta.Application.Common;

/// <summary>
/// Las fechas se guardan en UTC, pero "las ventas del día" se entienden en hora de Lima.
/// Perú no aplica horario de verano, por lo que el desfase UTC−5 es fijo y no hace falta
/// depender de la base de zonas horarias del sistema operativo (que difiere entre Windows y Linux).
/// </summary>
public static class LimaTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    public static DateOnly Today(DateTime? utcNow = null) => ToLimaDate(utcNow ?? DateTime.UtcNow);

    public static DateOnly ToLimaDate(DateTime utc) => DateOnly.FromDateTime(utc + Offset);

    /// <summary>Instante UTC en que empieza el día indicado en Lima.</summary>
    public static DateTime StartOfDayUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);
}
