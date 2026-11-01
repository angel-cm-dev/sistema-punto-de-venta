using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Application.Abstractions;

/// <summary>
/// Puerto de persistencia. La aplicación depende de esta interfaz (no del DbContext concreto),
/// así Infrastructure decide el proveedor (PostgreSQL en producción, SQLite en pruebas).
/// Ver ADR-0002 para la justificación de exponer DbSet en lugar de repositorios genéricos.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CashSession> CashSessions { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<DocumentSeries> DocumentSeries { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken Create(User user);
}

/// <summary>Usuario autenticado de la petición actual (resuelto desde los claims del JWT).</summary>
public interface ICurrentUser
{
    int Id { get; }
    Role Role { get; }
    bool IsAdmin => Role == Role.Admin;
}
