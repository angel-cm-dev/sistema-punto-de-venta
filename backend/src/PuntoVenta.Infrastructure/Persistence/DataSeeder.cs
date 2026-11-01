using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Infrastructure.Persistence;

/// <summary>
/// Datos de demostración 100 % FICTICIOS (nombres, RUC y DNI inventados, con dígito
/// verificador válido para pasar las reglas). Es idempotente: si ya hay usuarios, no hace nada.
/// </summary>
public class DataSeeder(AppDbContext db, IPasswordHasher hasher, ILogger<DataSeeder> logger)
{
    public const string DemoPassword = "Demo123!";

    public async Task SeedAsync(bool includeDemoData, CancellationToken ct = default)
    {
        if (!await db.DocumentSeries.AnyAsync(ct))
        {
            // Series base necesarias para operar (no son datos de demo).
            db.DocumentSeries.AddRange(
                new DocumentSeries(DocumentType.Boleta, "B001"),
                new DocumentSeries(DocumentType.Factura, "F001"));
            await db.SaveChangesAsync(ct);
        }

        if (!includeDemoData || await db.Users.AnyAsync(ct)) return;

        logger.LogInformation("Sembrando datos de demostración");
        var hash = hasher.Hash(DemoPassword);
        var admin = new User("admin@demo.pe", "Administrador Demo", hash, Role.Admin);
        var cajera = new User("cajero@demo.pe", "Lucía Quispe (demo)", hash, Role.Cajero);
        var cajero2 = new User("cajero2@demo.pe", "Jorge Huamán (demo)", hash, Role.Cajero);
        db.Users.AddRange(admin, cajera, cajero2);

        var bebidas = new Category("Bebidas", "Gaseosas, aguas y jugos");
        var abarrotes = new Category("Abarrotes", "Productos de despensa");
        var limpieza = new Category("Limpieza", "Limpieza del hogar");
        var snacks = new Category("Snacks", "Galletas y piqueos");
        db.Categories.AddRange(bebidas, abarrotes, limpieza, snacks);
        await db.SaveChangesAsync(ct);

        var products = new List<Product>
        {
            new("BEB-001", "Gaseosa Inca 500 ml", bebidas.Id, 3.50m, 120, 24),
            new("BEB-002", "Agua mineral 625 ml", bebidas.Id, 2.00m, 200, 36),
            new("BEB-003", "Jugo de naranja 1 L", bebidas.Id, 6.90m, 40, 10),
            new("BEB-004", "Gaseosa cola 1.5 L", bebidas.Id, 7.50m, 8, 12),
            new("ABA-001", "Arroz extra 5 kg", abarrotes.Id, 24.90m, 60, 15),
            new("ABA-002", "Aceite vegetal 1 L", abarrotes.Id, 11.50m, 45, 12),
            new("ABA-003", "Azúcar rubia 1 kg", abarrotes.Id, 4.20m, 90, 20),
            new("ABA-004", "Fideos spaghetti 500 g", abarrotes.Id, 3.80m, 5, 20),
            new("ABA-005", "Leche evaporada 400 g", abarrotes.Id, 4.10m, 150, 48),
            new("LIM-001", "Detergente 2 kg", limpieza.Id, 21.90m, 30, 8),
            new("LIM-002", "Lejía 1 L", limpieza.Id, 3.90m, 55, 12),
            new("LIM-003", "Papel higiénico x4", limpieza.Id, 6.50m, 70, 15),
            new("SNK-001", "Galleta de soda x6", snacks.Id, 4.50m, 80, 20),
            new("SNK-002", "Papas fritas 150 g", snacks.Id, 5.90m, 3, 10),
            new("SNK-003", "Chocolate 30 g", snacks.Id, 2.50m, 110, 30),
        };
        db.Products.AddRange(products);

        var customers = new List<Customer>
        {
            new(IdentityDocType.Ruc, "20601234565", "Bodega El Ejemplo S.A.C.", "compras@ejemplo.pe", "014445566", "Av. Ficticia 123, Lima"),
            new(IdentityDocType.Ruc, "20509876542", "Restaurante Demo E.I.R.L.", "admin@restaurante-demo.pe", null, "Jr. Inventado 456, Miraflores"),
            new(IdentityDocType.Ruc, "10456789124", "Pérez Rojas María (demo)", null, "987654321", null),
            new(IdentityDocType.Dni, "45678912", "Carlos Mendoza (demo)", "carlos@correo-demo.pe", "912345678", null),
            new(IdentityDocType.Dni, "71234567", "Ana Torres (demo)", null, null, null),
        };
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);

        await SeedHistoricalSalesAsync(cajera, cajero2, products, customers, ct);
    }

    /// <summary>
    /// Genera una semana de ventas en turnos ya cerrados para que el dashboard y los
    /// reportes tengan datos desde el primer arranque. Usa un generador con semilla fija
    /// para que la demo sea reproducible.
    /// </summary>
    private async Task SeedHistoricalSalesAsync(User c1, User c2, List<Product> products, List<Customer> customers, CancellationToken ct)
    {
        var random = new Random(2026);
        var boleta = await db.DocumentSeries.FirstAsync(s => s.DocumentType == DocumentType.Boleta, ct);
        var factura = await db.DocumentSeries.FirstAsync(s => s.DocumentType == DocumentType.Factura, ct);
        int boletaNumber = 0, facturaNumber = 0;

        for (var daysAgo = 6; daysAgo >= 1; daysAgo--)
        {
            var cashier = daysAgo % 2 == 0 ? c1 : c2;
            var openedAt = DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(13); // 08:00 hora Lima
            var session = new CashSession(cashier.Id, 200m);
            db.CashSessions.Add(session);
            db.Entry(session).Property(s => s.OpenedAt).CurrentValue = openedAt;
            await db.SaveChangesAsync(ct);

            var sales = new List<Sale>();
            var count = random.Next(4, 9);
            for (var n = 0; n < count; n++)
            {
                var picks = products.Where(p => p.Stock > 20).OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
                var lines = picks.Select(p => new SaleLineInput(p.Id, p.Name, random.Next(1, 4), p.Price)).ToList();
                var isFactura = n % 4 == 0;
                var customer = isFactura ? customers[random.Next(0, 3)] : (n % 3 == 0 ? customers[3] : null);
                var gross = lines.Sum(l => l.UnitPrice * l.Quantity);
                var method = (PaymentMethod)random.Next(1, 5);
                var paid = method == PaymentMethod.Efectivo ? Math.Ceiling(gross / 10) * 10 : gross;

                var sale = Sale.Create(isFactura ? DocumentType.Factura : DocumentType.Boleta, customer, cashier.Id, session.Id,
                    lines, 0, [new PaymentInput(method, paid, method == PaymentMethod.Efectivo ? null : $"OP-{random.Next(100000, 999999)}")]);
                sale.AssignNumber(isFactura ? factura.Code : boleta.Code, isFactura ? ++facturaNumber : ++boletaNumber);
                foreach (var l in lines) products.First(p => p.Id == l.ProductId).AdjustStock(-l.Quantity);

                db.Sales.Add(sale);
                db.Entry(sale).Property(s => s.CreatedAt).CurrentValue = openedAt.AddMinutes(30 + n * 47);
                sales.Add(sale);
            }

            var netCash = sales.Sum(s => s.NetCash);
            // Un pequeño faltante ocasional para que el reporte de arqueo muestre diferencias reales.
            session.Close(200m + netCash - (daysAgo == 3 ? 5m : 0m), netCash, daysAgo == 3 ? "Faltante de S/ 5.00 (demo)" : null);
            db.Entry(session).Property(s => s.ClosedAt).CurrentValue = openedAt.AddHours(9);
            await db.SaveChangesAsync(ct);
        }

        await db.DocumentSeries.Where(s => s.Id == boleta.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastNumber, boletaNumber), ct);
        await db.DocumentSeries.Where(s => s.Id == factura.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastNumber, facturaNumber), ct);
    }
}
