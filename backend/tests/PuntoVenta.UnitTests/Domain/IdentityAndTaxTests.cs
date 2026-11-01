using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Enums;
using PuntoVenta.Domain.Services;

namespace PuntoVenta.UnitTests.Domain;

public class IdentityDocumentValidatorTests
{
    [Theory]
    [InlineData("20601234565")] // persona jurídica (ficticio, dígito verificador válido)
    [InlineData("10456789124")] // persona natural con negocio
    [InlineData("20487654320")] // caso en que el dígito "se pliega" a 0
    public void Ruc_valido_pasa(string ruc) => Assert.True(IdentityDocumentValidator.IsValidRuc(ruc));

    [Theory]
    [InlineData("20601234566")] // dígito verificador incorrecto
    [InlineData("30601234565")] // prefijo inexistente
    [InlineData("2060123456")]  // 10 dígitos
    [InlineData("2060123456A")] // no numérico
    [InlineData("")]
    [InlineData(null)]
    public void Ruc_invalido_falla(string? ruc) => Assert.False(IdentityDocumentValidator.IsValidRuc(ruc));

    [Theory]
    [InlineData("45678912", true)]
    [InlineData("4567891", false)]
    [InlineData("456789123", false)]
    [InlineData("4567891X", false)]
    public void Dni_requiere_8_digitos(string dni, bool expected) =>
        Assert.Equal(expected, IdentityDocumentValidator.IsValid(IdentityDocType.Dni, dni));
}

public class TaxCalculatorTests
{
    [Fact]
    public void Extrae_IGV_de_un_precio_que_ya_lo_incluye()
    {
        var tax = TaxCalculator.FromGrossTotal(118m);
        Assert.Equal(100m, tax.Subtotal);
        Assert.Equal(18m, tax.Igv);
        Assert.Equal(118m, tax.Total);
    }

    [Theory]
    [InlineData(3.50)]
    [InlineData(0.01)]
    [InlineData(24.90)]
    [InlineData(1234.57)]
    public void Subtotal_mas_IGV_siempre_cuadra_al_centimo(decimal total)
    {
        var tax = TaxCalculator.FromGrossTotal(total);
        Assert.Equal(tax.Total, tax.Subtotal + tax.Igv);
        Assert.Equal(Money.Round(total), tax.Total);
    }

    [Fact]
    public void Redondeo_comercial_no_bancario()
    {
        // 2.665 → 2.67 (AwayFromZero); el redondeo bancario daría 2.66.
        Assert.Equal(2.67m, Money.Round(2.665m));
    }

    [Fact]
    public void Total_negativo_es_rechazado() =>
        Assert.Throws<DomainException>(() => TaxCalculator.FromGrossTotal(-1m));
}
