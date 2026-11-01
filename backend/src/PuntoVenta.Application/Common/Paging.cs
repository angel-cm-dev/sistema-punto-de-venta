using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace PuntoVenta.Application.Common;

/// <summary>Parámetros comunes de listados: ?page=1&amp;pageSize=20&amp;search=...&amp;sortBy=name&amp;sortDir=asc</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 20;

    public int Page { get => _page; set => _page = value < 1 ? 1 : value; }
    // Se acota el tamaño de página para que un cliente no pueda pedir toda la tabla de golpe.
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, MaxPageSize); }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }

    public bool Descending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public static class QueryableExtensions
{
    /// <summary>
    /// Ordena según una lista blanca de columnas. Nunca se construye el ORDER BY con el texto
    /// del cliente: un campo desconocido cae al orden por defecto.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        PageQuery page,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> allowed,
        string defaultKey)
    {
        var key = page.SortBy is not null && allowed.ContainsKey(page.SortBy) ? page.SortBy : defaultKey;
        var selector = allowed[key];
        return page.Descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
    }

    public static async Task<PagedResult<TOut>> ToPagedAsync<T, TOut>(
        this IQueryable<T> query, PageQuery page, Expression<Func<T, TOut>> projection, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(projection)
            .ToListAsync(ct);
        return new PagedResult<TOut>(items, page.Page, page.PageSize, total);
    }
}
