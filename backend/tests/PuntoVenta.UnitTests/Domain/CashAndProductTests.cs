using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.UnitTests.Domain;

public class CashSessionTests
{
    [Fact]
    public void Cierre_calcula_esperado_y_diferencia()
    {
        var session = new CashSession(cashierId: 7, openingAmount: 200m);
        session.Close(countedCash: 345.50m, netCashSales: 150m, notes: "Faltan 4.50");

        Assert.Equal(CashSessionStatus.Cerrada, session.Status);
        Assert.Equal(350m, session.ExpectedCash);
        Assert.Equal(-4.50m, session.Difference); // faltante
        Assert.NotNull(session.ClosedAt);
    }

    [Fact]
    public void No_se_puede_cerrar_dos_veces()
    {
        var session = new CashSession(1, 0m);
        session.Close(0m, 0m, null);
        var ex = Assert.Throws<DomainException>(() => session.Close(0m, 0m, null));
        Assert.Equal("caja_cerrada", ex.Code);
    }

    [Fact]
    public void Fondo_negativo_es_invalido() =>
        Assert.Throws<DomainException>(() => new CashSession(1, -10m));
}

public class ProductTests
{
    [Fact]
    public void Ajuste_no_puede_dejar_stock_negativo()
    {
        var product = new Product("abc-1", "Producto", 1, 10m, stock: 5, minStock: 2);
        Assert.Equal("ABC-1", product.Sku); // SKU normalizado a mayúsculas
        product.AdjustStock(-5);
        Assert.Equal(0, product.Stock);
        Assert.Throws<DomainException>(() => product.AdjustStock(-1));
    }

    [Fact]
    public void Detecta_stock_bajo_en_el_umbral()
    {
        var product = new Product("P-1", "Producto", 1, 10m, stock: 3, minStock: 3);
        Assert.True(product.IsLowStock);
        product.AdjustStock(1);
        Assert.False(product.IsLowStock);
    }

    [Fact]
    public void Precio_debe_ser_positivo() =>
        Assert.Throws<DomainException>(() => new Product("P-1", "Producto", 1, 0m, 1, 0));
}

public class LimaTimeAndPagingTests
{
    [Fact]
    public void Dia_en_Lima_es_UTC_menos_5()
    {
        // 03:00 UTC del 2 de enero = 22:00 del 1 de enero en Lima.
        var utc = new DateTime(2026, 1, 2, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 1, 1), LimaTime.ToLimaDate(utc));
        Assert.Equal(new DateTime(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc), LimaTime.StartOfDayUtc(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void PageQuery_acota_valores_fuera_de_rango()
    {
        var q = new PageQuery { Page = -3, PageSize = 5000 };
        Assert.Equal(1, q.Page);
        Assert.Equal(PageQuery.MaxPageSize, q.PageSize);
        Assert.Equal(3, new PagedResult<int>([], 1, 10, 21).TotalPages);
    }
}
