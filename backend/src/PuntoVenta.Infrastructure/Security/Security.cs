using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Domain.Entities;

namespace PuntoVenta.Infrastructure.Security;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "punto-de-venta";
    public string Audience { get; set; } = "punto-de-venta-web";
    /// <summary>Clave HMAC; mínimo 32 caracteres. En producción se inyecta por variable de entorno.</summary>
    public string Key { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 480;
}

/// <summary>
/// BCrypt con factor de trabajo 12: lento a propósito (≈250 ms) para encarecer ataques de
/// fuerza bruta si la tabla de usuarios se filtrara. Incluye sal aleatoria por contraseña.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;
    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
}

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    /// <summary>Claims con nombres cortos (estándar JWT) en vez de las URIs largas de .NET.</summary>
    public const string RoleClaim = "role";
    public const string SubjectClaim = JwtRegisteredClaimNames.Sub;

    private readonly JwtOptions _options = options.Value;

    public AccessToken Create(User user)
    {
        // Duración de un turno de trabajo (8 h) para no forzar re-login a mitad de jornada.
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                new Claim(RoleClaim, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256),
        };
        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
