using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DocumentSeries> DocumentSeries => Set<DocumentSeries>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        Database.BeginTransactionAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Montos con 2 decimales exactos (numeric(12,2) en PostgreSQL), nunca float.
        builder.Properties<decimal>().HavePrecision(12, 2);

        // Enums como texto: la base de datos queda legible para reportes y soporte.
        builder.Properties<Role>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<DocumentType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<IdentityDocType>().HaveConversion<string>().HaveMaxLength(10);
        builder.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<SaleStatus>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<CashSessionStatus>().HaveConversion<string>().HaveMaxLength(20);

        // SQLite (pruebas / modo local) no sabe sumar ni ordenar decimal en SQL. Se mapea a REAL
        // solo para ese proveedor; en PostgreSQL se mantiene numeric exacto.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            builder.Properties<decimal>().HaveConversion<double>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
