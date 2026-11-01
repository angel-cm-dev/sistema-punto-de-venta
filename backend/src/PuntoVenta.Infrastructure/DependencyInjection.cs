using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Infrastructure.Persistence;
using PuntoVenta.Infrastructure.Security;

namespace PuntoVenta.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Database:Provider = "Postgres" (producción/Docker) o "Sqlite" (ejecución local sin Docker).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "Postgres";
        var connection = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connection);
            else
                options.UseNpgsql(connection, npgsql => npgsql.EnableRetryOnFailure(3));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DataSeeder>();
        return services;
    }

    /// <summary>
    /// Aplica el esquema y siembra datos. PostgreSQL usa migraciones versionadas; SQLite,
    /// que solo se usa en local/pruebas, crea el esquema directamente desde el modelo.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

        if (db.Database.IsNpgsql())
        {
            logger.LogInformation("Aplicando migraciones de PostgreSQL");
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        var seedDemo = config.GetValue("Database:SeedDemoData", true);
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(seedDemo);
    }
}

/// <summary>Usado solo por "dotnet ef migrations add" para construir el contexto en tiempo de diseño.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=pos;Username=pos;Password=pos")
            .Options;
        return new AppDbContext(options);
    }
}
