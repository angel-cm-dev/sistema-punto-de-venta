using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.UnitTests.Domain;

public class SaleTests
{
    private static readonly Customer EmpresaConRuc =
        new(IdentityDocType.Ruc, "20601234565", "Empresa Demo S.A.C.", null, null, null);
    private static readonly Customer PersonaConDni =
        new(IdentityDocType.Dni, "45678912", "Cliente Demo", null, null, null);

    private static List<SaleLineInput> Lines(params (int qty, decimal price)[] lines) =>
        lines.Select((l, i) => new SaleLineInput(i + 1, $"Producto {i + 1}", l.qty, l.price)).ToList();

    private static Sale Create(
        DocumentType type, List<SaleLineInput> lines, List<PaymentInput> payments,
        Customer? customer = null, decimal discount = 0) =>
        Sale.Create(type, customer, cashierId: 1, cashSessionId: 1, lines, discount, payments);

    [Fact]
    public void Calcula_totales_IGV_y_vuelto()
    {
        var sale = Create(DocumentType.Boleta, Lines((2, 3.50m), (1, 24.90m)),
            [new PaymentInput(PaymentMethod.Efectivo, 50m, null)]);

        Assert.Equal(31.90m, sale.GrossAmount);
        Assert.Equal(31.90m, sale.Total);
        Assert.Equal(27.03m, sale.Subtotal);
        Assert.Equal(4.87m, sale.Igv);
        Assert.Equal(18.10m, sale.Change);
        Assert.Equal(31.90m, sale.NetCash); // 50 recibidos − 18.10 de vuelto
    }

    [Fact]
    public void Aplica_descuento_antes_de_extraer_IGV()
    {
        var sale = Create(DocumentType.Boleta, Lines((1, 118m)),
            [new PaymentInput(PaymentMethod.Tarjeta, 100.30m, "VOUCHER-1")], discount: 17.70m);

        Assert.Equal(100.30m, sale.Total);
        Assert.Equal(85.00m, sale.Subtotal);
        Assert.Equal(15.30m, sale.Igv);
    }

    [Fact]
    public void Acepta_pago_mixto_tarjeta_y_efectivo()
    {
        var sale = Create(DocumentType.Boleta, Lines((1, 100m)),
        [
            new PaymentInput(PaymentMethod.Yape, 60m, "OP-123"),
            new PaymentInput(PaymentMethod.Efectivo, 50m, null),
        ]);

        Assert.Equal(10m, sale.Change);
        Assert.Equal(40m, sale.NetCash);
        Assert.Equal(2, sale.Payments.Count);
    }

    [Fact]
    public void Rechaza_pago_insuficiente()
    {
        var ex = Assert.Throws<DomainException>(() => Create(DocumentType.Boleta, Lines((1, 10m)),
            [new PaymentInput(PaymentMethod.Efectivo, 9.99m, null)]));
        Assert.Equal("pago_insuficiente", ex.Code);
    }

    [Fact]
    public void Pagos_electronicos_no_generan_vuelto()
    {
        var ex = Assert.Throws<DomainException>(() => Create(DocumentType.Boleta, Lines((1, 10m)),
            [new PaymentInput(PaymentMethod.Plin, 20m, "OP-1")]));
        Assert.Equal("pago_excedido", ex.Code);
    }

    [Fact]
    public void Factura_exige_cliente_con_RUC()
    {
        var sinCliente = Assert.Throws<DomainException>(() => Create(DocumentType.Factura, Lines((1, 10m)),
            [new PaymentInput(PaymentMethod.Efectivo, 10m, null)]));
        Assert.Equal("factura_sin_ruc", sinCliente.Code);

        Assert.Throws<DomainException>(() => Create(DocumentType.Factura, Lines((1, 10m)),
            [new PaymentInput(PaymentMethod.Efectivo, 10m, null)], PersonaConDni));

        var ok = Create(DocumentType.Factura, Lines((1, 10m)),
            [new PaymentInput(PaymentMethod.Efectivo, 10m, null)], EmpresaConRuc);
        Assert.Equal(DocumentType.Factura, ok.DocumentType);
    }

    [Fact]
    public void Boleta_mayor_a_700_exige_identificar_al_cliente()
    {
        var ex = Assert.Throws<DomainException>(() => Create(DocumentType.Boleta, Lines((1, 700.01m)),
            [new PaymentInput(PaymentMethod.Tarjeta, 700.01m, null)]));
        Assert.Equal("boleta_sin_cliente", ex.Code);

        // Exactamente S/ 700 aún no exige identificación.
        Create(DocumentType.Boleta, Lines((1, 700m)), [new PaymentInput(PaymentMethod.Tarjeta, 700m, null)]);
        Create(DocumentType.Boleta, Lines((1, 900m)), [new PaymentInput(PaymentMethod.Tarjeta, 900m, null)], PersonaConDni);
    }

    [Fact]
    public void Rechaza_venta_vacia_y_descuento_total()
    {
        Assert.Throws<DomainException>(() => Create(DocumentType.Boleta, [], [new PaymentInput(PaymentMethod.Efectivo, 1m, null)]));
        Assert.Throws<DomainException>(() => Create(DocumentType.Boleta, Lines((1, 10m)),
            [new PaymentInput(PaymentMethod.Efectivo, 1m, null)], discount: 10m));
    }

    [Fact]
    public void Anulacion_requiere_motivo_y_no_se_repite()
    {
        var sale = Create(DocumentType.Boleta, Lines((1, 10m)), [new PaymentInput(PaymentMethod.Efectivo, 10m, null)]);
        Assert.Throws<DomainException>(() => sale.Void(1, "x"));

        sale.Void(1, "Cliente desistió de la compra");
        Assert.Equal(SaleStatus.Anulada, sale.Status);
        Assert.NotNull(sale.VoidedAt);

        var ex = Assert.Throws<DomainException>(() => sale.Void(1, "Segundo intento"));
        Assert.Equal("venta_anulada", ex.Code);
    }

    [Fact]
    public void Numero_de_comprobante_tiene_formato_SUNAT()
    {
        var sale = Create(DocumentType.Boleta, Lines((1, 10m)), [new PaymentInput(PaymentMethod.Efectivo, 10m, null)]);
        sale.AssignNumber("B001", 123);
        Assert.Equal("B001-00000123", sale.DocumentNumber);
        Assert.Throws<DomainException>(() => sale.AssignNumber("B001", 124));
    }
}
