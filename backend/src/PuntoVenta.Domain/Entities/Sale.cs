using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Enums;
using PuntoVenta.Domain.Services;

namespace PuntoVenta.Domain.Entities;

public record SaleLineInput(int ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record PaymentInput(PaymentMethod Method, decimal Amount, string? Reference);

/// <summary>
/// Agregado raíz de la venta. Concentra todas las invariantes del comprobante:
/// totales, IGV, medios de pago, vuelto y reglas SUNAT de identificación del cliente.
/// La capa de aplicación solo orquesta (stock, correlativo, persistencia).
/// </summary>
public class Sale
{
    /// <summary>
    /// SUNAT obliga a identificar al adquirente en boletas cuyo importe supera S/ 700.
    /// </summary>
    public const decimal BoletaIdentificationThreshold = 700m;

    public int Id { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string Series { get; private set; } = null!;
    public int Number { get; private set; }
    public string DocumentNumber => DocumentSeries.Format(Series, Number);

    public int? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public int CashierId { get; private set; }
    public User? Cashier { get; private set; }
    public int CashSessionId { get; private set; }
    public CashSession? CashSession { get; private set; }

    /// <summary>Suma de líneas antes del descuento (con IGV).</summary>
    public decimal GrossAmount { get; private set; }
    public decimal Discount { get; private set; }
    /// <summary>Valor de venta (base imponible, sin IGV).</summary>
    public decimal Subtotal { get; private set; }
    public decimal Igv { get; private set; }
    /// <summary>Importe total a pagar (con IGV, después del descuento).</summary>
    public decimal Total { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Change { get; private set; }

    public SaleStatus Status { get; private set; } = SaleStatus.Emitida;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? VoidedAt { get; private set; }
    public int? VoidedById { get; private set; }
    public string? VoidReason { get; private set; }

    public List<SaleItem> Items { get; private set; } = [];
    public List<Payment> Payments { get; private set; } = [];

    private Sale() { }

    public static Sale Create(
        DocumentType documentType,
        Customer? customer,
        int cashierId,
        int cashSessionId,
        IReadOnlyCollection<SaleLineInput> lines,
        decimal discount,
        IReadOnlyCollection<PaymentInput> payments)
    {
        if (lines.Count == 0) throw new DomainException("La venta debe tener al menos un ítem.", "venta_vacia");
        if (lines.Any(l => l.Quantity <= 0)) throw new DomainException("Las cantidades deben ser mayores a cero.");
        if (discount < 0) throw new DomainException("El descuento no puede ser negativo.");

        var sale = new Sale
        {
            DocumentType = documentType,
            Customer = customer,
            CustomerId = customer?.Id,
            CashierId = cashierId,
            CashSessionId = cashSessionId,
        };

        foreach (var line in lines)
            sale.Items.Add(new SaleItem(line.ProductId, line.ProductName, line.Quantity, line.UnitPrice));

        sale.GrossAmount = sale.Items.Sum(i => i.LineTotal);
        sale.Discount = Money.Round(discount);
        if (sale.Discount >= sale.GrossAmount)
            throw new DomainException("El descuento no puede ser igual o mayor al importe de la venta.");

        var tax = TaxCalculator.FromGrossTotal(sale.GrossAmount - sale.Discount);
        sale.Subtotal = tax.Subtotal;
        sale.Igv = tax.Igv;
        sale.Total = tax.Total;

        sale.ValidateCustomer(customer);
        sale.ApplyPayments(payments);
        return sale;
    }

    private void ValidateCustomer(Customer? customer)
    {
        if (DocumentType == DocumentType.Factura)
        {
            // La factura da derecho a crédito fiscal: solo se emite a contribuyentes con RUC.
            if (customer is null || customer.DocType != IdentityDocType.Ruc)
                throw new DomainException("La factura requiere un cliente con RUC.", "factura_sin_ruc");
        }
        else if (Total > BoletaIdentificationThreshold && customer is null)
        {
            throw new DomainException(
                $"Las boletas mayores a S/ {BoletaIdentificationThreshold:0.00} deben identificar al cliente (DNI o RUC).",
                "boleta_sin_cliente");
        }
    }

    private void ApplyPayments(IReadOnlyCollection<PaymentInput> payments)
    {
        if (payments.Count == 0) throw new DomainException("Debe registrar al menos un medio de pago.", "sin_pago");
        if (payments.Any(p => p.Amount <= 0)) throw new DomainException("Los montos de pago deben ser mayores a cero.");

        var nonCash = Money.Round(payments.Where(p => p.Method != PaymentMethod.Efectivo).Sum(p => p.Amount));
        // Tarjeta, Yape y Plin se cobran por el monto exacto: no existe "vuelto" electrónico.
        if (nonCash > Total)
            throw new DomainException("Los pagos con tarjeta/Yape/Plin no pueden superar el total.", "pago_excedido");

        AmountPaid = Money.Round(payments.Sum(p => p.Amount));
        if (AmountPaid < Total)
            throw new DomainException($"Pago insuficiente: faltan S/ {Total - AmountPaid:0.00}.", "pago_insuficiente");

        // Como lo no-efectivo nunca supera el total, el vuelto siempre sale del efectivo entregado.
        Change = AmountPaid - Total;
        foreach (var p in payments) Payments.Add(new Payment(p.Method, Money.Round(p.Amount), p.Reference));
    }

    /// <summary>Asigna serie y correlativo reservados atómicamente en la base de datos.</summary>
    public void AssignNumber(string series, int number)
    {
        if (Number != 0) throw new DomainException("El comprobante ya tiene número asignado.");
        Series = series;
        Number = number;
    }

    /// <summary>Efectivo que realmente queda en caja por esta venta (efectivo recibido − vuelto).</summary>
    public decimal NetCash =>
        Payments.Where(p => p.Method == PaymentMethod.Efectivo).Sum(p => p.Amount) - Change;

    public void Void(int userId, string reason)
    {
        if (Status == SaleStatus.Anulada) throw new DomainException("La venta ya está anulada.", "venta_anulada");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new DomainException("Indique el motivo de la anulación (mínimo 5 caracteres).");
        Status = SaleStatus.Anulada;
        VoidedAt = DateTime.UtcNow;
        VoidedById = userId;
        VoidReason = reason.Trim();
    }
}

public class SaleItem
{
    public int Id { get; private set; }
    public int SaleId { get; private set; }
    public int ProductId { get; private set; }
    public Product? Product { get; private set; }

    /// <summary>
    /// Copia del nombre y precio al momento de la venta: si el producto cambia de precio
    /// después, el comprobante histórico debe seguir mostrando lo que se cobró.
    /// </summary>
    public string ProductName { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    private SaleItem() { }

    public SaleItem(int productId, string productName, int quantity, decimal unitPrice)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = Money.Round(unitPrice);
        LineTotal = Money.Round(UnitPrice * quantity);
    }
}

public class Payment
{
    public int Id { get; private set; }
    public int SaleId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    /// <summary>Nº de operación de Yape/Plin o voucher de tarjeta.</summary>
    public string? Reference { get; private set; }

    private Payment() { }

    public Payment(PaymentMethod method, decimal amount, string? reference)
    {
        Method = method;
        Amount = amount;
        Reference = reference?.Trim();
    }
}
