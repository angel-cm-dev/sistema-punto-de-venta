using PuntoVenta.Application.Abstractions;
using PuntoVenta.Domain.Enums;
using PuntoVenta.Infrastructure.Security;

namespace PuntoVenta.Api.Infrastructure;

/// <summary>Adaptador que expone a la capa de aplicación el usuario del JWT de la petición.</summary>
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No hay una petición HTTP activa.");

    public int Id => int.TryParse(Principal.FindFirst(JwtTokenService.SubjectClaim)?.Value, out var id)
        ? id
        : throw new UnauthorizedAccessException();

    public Role Role => Enum.TryParse<Role>(Principal.FindFirst(JwtTokenService.RoleClaim)?.Value, out var role)
        ? role
        : throw new UnauthorizedAccessException();
}
