using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PuntoVenta.IntegrationTests.Infrastructure;

namespace PuntoVenta.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthAndCatalogTests(PosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = PosApiFactory.Json;

    [Fact]
    public async Task Health_responde_ok()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Documento_OpenAPI_se_genera_con_esquema_Bearer()
    {
        var doc = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Json);
        Assert.True(doc.GetProperty("paths").TryGetProperty("/api/sales", out _));
        Assert.True(doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Login_con_credenciales_demo_devuelve_token_y_rol()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = PosApiFactory.AdminEmail, password = PosApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Admin", body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_invalido_devuelve_401_en_formato_ProblemDetails()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = PosApiFactory.AdminEmail, password = "incorrecta" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Endpoint_protegido_sin_token_devuelve_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cajero_no_puede_crear_productos()
    {
        var cajero = await factory.CreateAuthenticatedClientAsync("cajero@demo.pe");
        var response = await cajero.PostAsJsonAsync("/api/products",
            new { sku = "X-1", name = "X", categoryId = 1, price = 1, stock = 1, minStock = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Crear_producto_valida_entrada_y_SKU_unico()
    {
        var admin = await factory.CreateAdminClientAsync();

        var invalid = await admin.PostAsJsonAsync("/api/products",
            new { sku = "con espacios", name = "", categoryId = 1, price = -5, stock = 1, minStock = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("sku", out _));
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("price", out _));

        var product = await PosApiFactory.CreateProductAsync(admin, 9.90m, 10);
        var duplicate = await admin.PostAsJsonAsync("/api/products", new
        {
            sku = product.GetProperty("sku").GetString(),
            name = "Otro",
            categoryId = product.GetProperty("categoryId").GetInt32(),
            price = 1,
            stock = 1,
            minStock = 0,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Listado_de_productos_pagina_filtra_y_ordena()
    {
        var admin = await factory.CreateAdminClientAsync();

        var page = await admin.GetFromJsonAsync<JsonElement>("/api/products?page=1&pageSize=5&sortBy=price&sortDir=desc", Json);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(5, items.Count);
        Assert.True(page.GetProperty("totalItems").GetInt32() >= 15);
        var prices = items.Select(i => i.GetProperty("price").GetDecimal()).ToList();
        Assert.Equal(prices.OrderByDescending(p => p), prices);

        var search = await admin.GetFromJsonAsync<JsonElement>("/api/products?search=arroz", Json);
        Assert.Contains(search.GetProperty("items").EnumerateArray(), i => i.GetProperty("sku").GetString() == "ABA-001");

        var low = await admin.GetFromJsonAsync<JsonElement>("/api/products?lowStock=true&pageSize=100", Json);
        Assert.All(low.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("isLowStock").GetBoolean()));
    }

    [Fact]
    public async Task Cliente_con_RUC_invalido_es_rechazado_y_valido_se_registra()
    {
        var cajero = await factory.CreateAuthenticatedClientAsync("cajero@demo.pe");

        var bad = await cajero.PostAsJsonAsync("/api/customers",
            new { docType = "Ruc", docNumber = "20601234566", name = "Mala S.A." }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var dni = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var ok = await cajero.PostAsJsonAsync("/api/customers",
            new { docType = "Dni", docNumber = dni, name = "Cliente Nuevo" }, Json);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        var found = await cajero.GetAsync($"/api/customers/by-document/{dni}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
    }

    [Fact]
    public async Task Admin_no_puede_quitarse_su_propio_rol()
    {
        var admin = await factory.CreateAdminClientAsync();
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", Json);
        var response = await admin.PutAsJsonAsync($"/api/users/{me.GetProperty("id").GetInt32()}",
            new { fullName = "Admin", role = "Cajero", isActive = true }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
