using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuntoVenta.Domain.Entities;

namespace PuntoVenta.Infrastructure.Persistence;

// Mapeo explícito (Fluent API) en lugar de atributos: el dominio queda libre de detalles de persistencia.

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Description).HasMaxLength(250);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            // Defensa en profundidad: aunque la aplicación ya lo valida, la BD nunca acepta stock negativo.
            t.HasCheckConstraint("ck_products_stock_non_negative", "\"Stock\" >= 0");
            t.HasCheckConstraint("ck_products_price_positive", "\"Price\" > 0");
        });
        b.Property(x => x.Sku).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Sku).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Name);
        b.Ignore(x => x.IsLowStock);
        b.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.Property(x => x.DocNumber).HasMaxLength(11).IsRequired();
        b.HasIndex(x => x.DocNumber).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(250);
    }
}

public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> b)
    {
        b.ToTable("cash_sessions");
        b.Ignore(x => x.IsOpen);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CashierId, x.Status });
    }
}

public class DocumentSeriesConfiguration : IEntityTypeConfiguration<DocumentSeries>
{
    public void Configure(EntityTypeBuilder<DocumentSeries> b)
    {
        b.ToTable("document_series");
        b.Property(x => x.Code).HasMaxLength(4).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("sales");
        b.Ignore(x => x.DocumentNumber);
        b.Ignore(x => x.NetCash);
        b.Property(x => x.Series).HasMaxLength(4).IsRequired();
        // Garantía a nivel de BD de que un comprobante (serie + número) es único.
        b.HasIndex(x => new { x.Series, x.Number }).IsUnique();
        b.HasIndex(x => x.CreatedAt);
        b.Property(x => x.VoidReason).HasMaxLength(250);

        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CashSession).WithMany(s => s.Sales).HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.VoidedById).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.ToTable("sale_items");
        b.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.Property(x => x.Reference).HasMaxLength(60);
    }
}
