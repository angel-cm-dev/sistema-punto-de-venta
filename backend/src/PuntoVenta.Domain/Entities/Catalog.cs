using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Enums;
using PuntoVenta.Domain.Services;

namespace PuntoVenta.Domain.Entities;

public class Category
{
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public List<Product> Products { get; private set; } = [];

    private Category() { }

    public Category(string name, string? description)
    {
        Update(name, description);
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre de la categoría es obligatorio.");
        Name = name.Trim();
        Description = description?.Trim();
    }
}

public class Product
{
    public int Id { get; private set; }
    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int CategoryId { get; private set; }
    public Category? Category { get; private set; }

    /// <summary>Precio de venta unitario CON IGV incluido (precio de góndola).</summary>
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public int MinStock { get; private set; }

    /// <summary>
    /// Baja lógica: un producto con ventas históricas no puede borrarse físicamente
    /// porque los comprobantes emitidos lo referencian.
    /// </summary>
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Product() { }

    public Product(string sku, string name, int categoryId, decimal price, int stock, int minStock)
    {
        if (stock < 0) throw new DomainException("El stock inicial no puede ser negativo.");
        Sku = sku.Trim().ToUpperInvariant();
        Stock = stock;
        Update(name, categoryId, price, minStock);
    }

    public bool IsLowStock => Stock <= MinStock;

    public void Update(string name, int categoryId, decimal price, int minStock)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del producto es obligatorio.");
        if (price <= 0) throw new DomainException("El precio debe ser mayor a cero.");
        if (minStock < 0) throw new DomainException("El stock mínimo no puede ser negativo.");
        Name = name.Trim();
        CategoryId = categoryId;
        Price = Money.Round(price);
        MinStock = minStock;
    }

    /// <summary>Ajuste manual de inventario (conteo físico, merma, ingreso de mercadería).</summary>
    public void AdjustStock(int delta)
    {
        if (Stock + delta < 0) throw new DomainException($"El ajuste dejaría el stock de '{Name}' en negativo.");
        Stock += delta;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

public class Customer
{
    public int Id { get; private set; }
    public IdentityDocType DocType { get; private set; }
    public string DocNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Customer() { }

    public Customer(IdentityDocType docType, string docNumber, string name, string? email, string? phone, string? address)
    {
        if (!IdentityDocumentValidator.IsValid(docType, docNumber))
            throw new DomainException(docType == IdentityDocType.Ruc
                ? "El RUC no es válido (11 dígitos y dígito verificador correcto)."
                : "El DNI debe tener exactamente 8 dígitos.", "documento_invalido");
        DocType = docType;
        DocNumber = docNumber;
        Update(name, email, phone, address);
    }

    public void Update(string name, string? email, string? phone, string? address)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre o razón social es obligatorio.");
        Name = name.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        Address = address?.Trim();
    }
}

public class User
{
    public int Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private User() { }

    public User(string email, string fullName, string passwordHash, Role role)
    {
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        Role = role;
    }

    public void Update(string fullName, Role role, bool isActive)
    {
        FullName = fullName.Trim();
        Role = role;
        IsActive = isActive;
    }

    public void ChangePassword(string newHash) => PasswordHash = newHash;
}
