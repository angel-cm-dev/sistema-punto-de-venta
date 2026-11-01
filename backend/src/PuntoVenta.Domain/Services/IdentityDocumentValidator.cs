using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Domain.Services;

/// <summary>
/// Validación de documentos de identidad peruanos.
/// </summary>
public static class IdentityDocumentValidator
{
    // Pesos oficiales del algoritmo módulo 11 que SUNAT usa para el dígito verificador del RUC.
    private static readonly int[] RucWeights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    // Prefijos válidos: 10 persona natural, 15/17 casos especiales, 20 persona jurídica.
    private static readonly string[] RucPrefixes = ["10", "15", "17", "20"];

    public static bool IsValidDni(string? value) =>
        value is { Length: 8 } && value.All(char.IsAsciiDigit);

    public static bool IsValidRuc(string? value)
    {
        if (value is not { Length: 11 } || !value.All(char.IsAsciiDigit)) return false;
        if (!RucPrefixes.Contains(value[..2])) return false;

        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (value[i] - '0') * RucWeights[i];

        var check = 11 - (sum % 11);
        // Si el resultado es 10 u 11 el dígito se "pliega" a 0 u 1 respectivamente.
        if (check == 10) check = 0;
        else if (check == 11) check = 1;

        return check == value[10] - '0';
    }

    public static bool IsValid(IdentityDocType type, string? value) => type switch
    {
        IdentityDocType.Dni => IsValidDni(value),
        IdentityDocType.Ruc => IsValidRuc(value),
        _ => false
    };
}
