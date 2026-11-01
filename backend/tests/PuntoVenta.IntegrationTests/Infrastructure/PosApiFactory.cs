using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace PuntoVenta.IntegrationTests.Infrastructure;

/// <summary>
/// Levanta la API real en memoria con SQLite en memoria (sin Docker ni PostgreSQL).
/// La base compartida "Mode=Memory;Cache=Shared" vive mientras haya una conexión abierta,
/// por eso la fábrica mantiene una conexión "ancla" durante toda la sesión de pruebas.
/// </summary>
public class PosApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@demo.pe";
    public const string Password = "Demo123!";

    private readonly string _connectionString = $"Data Source=pos-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _anchor;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public PosApiFactory()
    {
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Jwt:Key", "clave-exclusiva-de-pruebas-de-integracion-0123456789");
        builder.UseSetting("RateLimiting:LoginPermitPerMinute", "1000");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateAuthenticatedClientAsync(AdminEmail);

    /// <summary>
    /// Crea un cajero nuevo y aislado para la prueba (evita que el estado de caja de una prueba
    /// afecte a otra) y devuelve un cliente autenticado como él.
    /// </summary>
    public async Task<HttpClient> CreateFreshCashierAsync(HttpClient admin, decimal? openCashWith = null)
    {
        var email = $"cajero-{Guid.NewGuid():N}@demo.pe";
        var created = await admin.PostAsJsonAsync("/api/users",
            new { email, fullName = "Cajero de prueba", password = Password, role = "Cajero" }, Json);
        created.EnsureSuccessStatusCode();

        var client = await CreateAuthenticatedClientAsync(email);
        if (openCashWith is { } amount)
            (await client.PostAsJsonAsync("/api/cash-sessions/open", new { openingAmount = amount })).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Crea un producto con stock controlado para que la prueba conozca el punto de partida.</summary>
    public static async Task<JsonElement> CreateProductAsync(HttpClient admin, decimal price, int stock, int minStock = 0)
    {
        var categories = await admin.GetFromJsonAsync<JsonElement>("/api/categories", Json);
        var categoryId = categories[0].GetProperty("id").GetInt32();
        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            sku = $"T-{Guid.NewGuid().ToString("N")[..10]}",
            name = $"Producto prueba {Guid.NewGuid().ToString("N")[..6]}",
            categoryId,
            price,
            stock,
            minStock,
        }, Json);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _anchor.Dispose();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<PosApiFactory>
{
    public const string Name = "api";
}
