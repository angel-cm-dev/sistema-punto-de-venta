using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PuntoVenta.IntegrationTests.Infrastructure;

namespace PuntoVenta.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SalesFlowTests(PosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = PosApiFactory.Json;

    private static object CashSale(int productId, int quantity, decimal cash) => new
    {
        documentType = "Boleta",
        items = new[] { new { productId, quantity } },
        payments = new[] { new { method = "Efectivo", amount = cash } },
    };

    private static async Task<int> StockOf(HttpClient client, int productId) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/products/{productId}", Json)).GetProperty("stock").GetInt32();

    private static async Task<string> ErrorCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString()!;

    [Fact]
    public async Task Venta_sin_caja_abierta_es_rechazada()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin);
        var product = await PosApiFactory.CreateProductAsync(admin, 5m, 10);

        var response = await cajero.PostAsJsonAsync("/api/sales", CashSale(product.GetProperty("id").GetInt32(), 1, 5m), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("caja_no_abierta", await ErrorCode(response));
    }

    [Fact]
    public async Task Venta_completa_descuenta_stock_numera_y_calcula_IGV()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 100m);
        var product = await PosApiFactory.CreateProductAsync(admin, 11.80m, 10);
        var id = product.GetProperty("id").GetInt32();

        var first = await cajero.PostAsJsonAsync("/api/sales", CashSale(id, 3, 50m), Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var sale = await first.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(35.40m, sale.GetProperty("total").GetDecimal());
        Assert.Equal(30.00m, sale.GetProperty("subtotal").GetDecimal());
        Assert.Equal(5.40m, sale.GetProperty("igv").GetDecimal());
        Assert.Equal(14.60m, sale.GetProperty("change").GetDecimal());
        Assert.Matches("^B001-\\d{8}$", sale.GetProperty("documentNumber").GetString());
        Assert.Equal(7, await StockOf(admin, id));

        // El correlativo de la siguiente boleta es exactamente +1.
        var second = await (await cajero.PostAsJsonAsync("/api/sales", CashSale(id, 1, 11.80m), Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var n1 = int.Parse(sale.GetProperty("documentNumber").GetString()![5..]);
        var n2 = int.Parse(second.GetProperty("documentNumber").GetString()![5..]);
        Assert.Equal(n1 + 1, n2);
    }

    [Fact]
    public async Task Stock_insuficiente_revierte_toda_la_transaccion()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 0m);
        var a = (await PosApiFactory.CreateProductAsync(admin, 2m, 10)).GetProperty("id").GetInt32();
        var b = (await PosApiFactory.CreateProductAsync(admin, 2m, 1)).GetProperty("id").GetInt32();

        var response = await cajero.PostAsJsonAsync("/api/sales", new
        {
            documentType = "Boleta",
            items = new[] { new { productId = a, quantity = 4 }, new { productId = b, quantity = 2 } },
            payments = new[] { new { method = "Efectivo", amount = 20m } },
        }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("stock_insuficiente", await ErrorCode(response));
        // El producto A ya se había descontado dentro de la transacción: el ROLLBACK debe devolverlo.
        Assert.Equal(10, await StockOf(admin, a));
        Assert.Equal(1, await StockOf(admin, b));
    }

    [Fact]
    public async Task Factura_sin_RUC_es_rechazada_y_con_RUC_usa_serie_F()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 0m);
        var id = (await PosApiFactory.CreateProductAsync(admin, 10m, 10)).GetProperty("id").GetInt32();
        object Factura(int? customerId) => new
        {
            documentType = "Factura",
            customerId,
            items = new[] { new { productId = id, quantity = 1 } },
            payments = new[] { new { method = "Tarjeta", amount = 10m, reference = "VOUCHER-9" } },
        };

        var rejected = await cajero.PostAsJsonAsync("/api/sales", Factura(null), Json);
        Assert.Equal("factura_sin_ruc", await ErrorCode(rejected));

        var empresa = await cajero.GetFromJsonAsync<JsonElement>("/api/customers/by-document/20601234565", Json);
        var ok = await cajero.PostAsJsonAsync("/api/sales", Factura(empresa.GetProperty("id").GetInt32()), Json);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var sale = await ok.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.StartsWith("F001-", sale.GetProperty("documentNumber").GetString());
    }

    [Fact]
    public async Task Pago_electronico_mayor_al_total_y_descuento_excesivo_son_rechazados()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 0m);
        var id = (await PosApiFactory.CreateProductAsync(admin, 100m, 10)).GetProperty("id").GetInt32();

        var yape = await cajero.PostAsJsonAsync("/api/sales", new
        {
            documentType = "Boleta",
            items = new[] { new { productId = id, quantity = 1 } },
            payments = new[] { new { method = "Yape", amount = 120m, reference = "OP-1" } },
        }, Json);
        Assert.Equal("pago_excedido", await ErrorCode(yape));

        var discount = await cajero.PostAsJsonAsync("/api/sales", new
        {
            documentType = "Boleta",
            discount = 15m,
            items = new[] { new { productId = id, quantity = 1 } },
            payments = new[] { new { method = "Efectivo", amount = 85m } },
        }, Json);
        Assert.Equal("descuento_excedido", await ErrorCode(discount));
    }

    [Fact]
    public async Task Anulacion_solo_admin_repone_stock_y_no_se_repite()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 0m);
        var id = (await PosApiFactory.CreateProductAsync(admin, 4m, 5)).GetProperty("id").GetInt32();
        var sale = await (await cajero.PostAsJsonAsync("/api/sales", CashSale(id, 2, 8m), Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var saleId = sale.GetProperty("id").GetInt32();
        Assert.Equal(3, await StockOf(admin, id));

        var byCashier = await cajero.PostAsJsonAsync($"/api/sales/{saleId}/void", new { reason = "Error de digitación" });
        Assert.Equal(HttpStatusCode.Forbidden, byCashier.StatusCode);

        var voided = await admin.PostAsJsonAsync($"/api/sales/{saleId}/void", new { reason = "Error de digitación" });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal("Anulada", (await voided.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString());
        Assert.Equal(5, await StockOf(admin, id));

        var again = await admin.PostAsJsonAsync($"/api/sales/{saleId}/void", new { reason = "Segundo intento" });
        Assert.Equal("venta_anulada", await ErrorCode(again));
        Assert.Equal(5, await StockOf(admin, id));
    }

    [Fact]
    public async Task Cierre_de_caja_hace_arqueo_excluyendo_ventas_anuladas()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 150m);
        var id = (await PosApiFactory.CreateProductAsync(admin, 10m, 50)).GetProperty("id").GetInt32();

        await cajero.PostAsJsonAsync("/api/sales", CashSale(id, 2, 50m), Json);                // +20 efectivo neto
        await cajero.PostAsJsonAsync("/api/sales", new                                          // pago mixto
        {
            documentType = "Boleta",
            items = new[] { new { productId = id, quantity = 3 } },
            payments = new[]
            {
                new { method = "Plin", amount = 25m, reference = (string?)"OP-7" },
                new { method = "Efectivo", amount = 10m, reference = (string?)null },
            },
        }, Json);                                                                                // +5 efectivo neto
        var toVoid = await (await cajero.PostAsJsonAsync("/api/sales", CashSale(id, 1, 10m), Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        await admin.PostAsJsonAsync($"/api/sales/{toVoid.GetProperty("id").GetInt32()}/void", new { reason = "Prueba de arqueo" });

        var current = await cajero.GetFromJsonAsync<JsonElement>("/api/cash-sessions/current", Json);
        var sessionId = current.GetProperty("id").GetInt32();
        Assert.Equal(175m, current.GetProperty("expectedCash").GetDecimal());

        var closed = await cajero.PostAsJsonAsync($"/api/cash-sessions/{sessionId}/close", new { countedCash = 170m, notes = "Faltante" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var summary = await closed.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Cerrada", summary.GetProperty("status").GetString());
        Assert.Equal(2, summary.GetProperty("salesCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("voidedCount").GetInt32());
        Assert.Equal(50m, summary.GetProperty("salesTotal").GetDecimal());
        Assert.Equal(-5m, summary.GetProperty("difference").GetDecimal());

        var noSession = await cajero.GetAsync("/api/cash-sessions/current");
        Assert.Equal(HttpStatusCode.NoContent, noSession.StatusCode);
    }

    [Fact]
    public async Task No_se_puede_abrir_dos_cajas_a_la_vez()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateFreshCashierAsync(admin, openCashWith: 10m);
        var second = await cajero.PostAsJsonAsync("/api/cash-sessions/open", new { openingAmount = 10m });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Cajero_solo_ve_sus_ventas()
    {
        var admin = await factory.CreateAdminClientAsync();
        var c1 = await factory.CreateFreshCashierAsync(admin, openCashWith: 0m);
        var c2 = await factory.CreateFreshCashierAsync(admin, openCashWith: 0m);
        var id = (await PosApiFactory.CreateProductAsync(admin, 3m, 10)).GetProperty("id").GetInt32();
        var sale = await (await c1.PostAsJsonAsync("/api/sales", CashSale(id, 1, 3m), Json)).Content.ReadFromJsonAsync<JsonElement>(Json);

        var list = await c2.GetFromJsonAsync<JsonElement>("/api/sales", Json);
        Assert.Equal(0, list.GetProperty("totalItems").GetInt32());

        var detail = await c2.GetAsync($"/api/sales/{sale.GetProperty("id").GetInt32()}");
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
    }

    [Fact]
    public async Task Reportes_solo_para_admin_y_con_datos_del_seed()
    {
        var admin = await factory.CreateAdminClientAsync();
        var cajero = await factory.CreateAuthenticatedClientAsync("cajero@demo.pe");

        Assert.Equal(HttpStatusCode.Forbidden, (await cajero.GetAsync("/api/reports/sales-by-day")).StatusCode);

        var byDay = await admin.GetFromJsonAsync<JsonElement>("/api/reports/sales-by-day", Json);
        Assert.True(byDay.GetArrayLength() >= 6);
        foreach (var day in byDay.EnumerateArray())
            Assert.Equal(day.GetProperty("total").GetDecimal(),
                day.GetProperty("subtotal").GetDecimal() + day.GetProperty("igv").GetDecimal());

        var byProduct = await admin.GetFromJsonAsync<JsonElement>("/api/reports/sales-by-product?top=3", Json);
        Assert.InRange(byProduct.GetArrayLength(), 1, 3);

        var byCashier = await admin.GetFromJsonAsync<JsonElement>("/api/reports/sales-by-cashier", Json);
        Assert.True(byCashier.GetArrayLength() >= 2);

        var badRange = await admin.GetAsync("/api/reports/sales-by-day?from=2026-02-01&to=2026-01-01");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badRange.StatusCode);

        var dashboard = await cajero.GetFromJsonAsync<JsonElement>("/api/reports/dashboard", Json);
        Assert.Equal(7, dashboard.GetProperty("last7Days").GetArrayLength());
    }
}
