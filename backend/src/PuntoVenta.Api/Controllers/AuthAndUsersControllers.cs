using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PuntoVenta.Application.Common;
using PuntoVenta.Application.Features.Users;

namespace PuntoVenta.Api.Controllers;

public static class Roles
{
    public const string Admin = nameof(Domain.Enums.Role.Admin);
    public const string Cajero = nameof(Domain.Enums.Role.Cajero);
}

[ApiController]
[Route("api/auth")]
[Tags("Autenticación")]
public class AuthController(UserService users) : ControllerBase
{
    /// <summary>Inicia sesión y devuelve un JWT (Bearer) válido por un turno de trabajo.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await users.LoginAsync(request, ct);
        return result is null
            ? Problem(statusCode: 401, title: "Credenciales inválidas", detail: "Correo o contraseña incorrectos.")
            : Ok(result);
    }

    /// <summary>Datos del usuario autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    public Task<UserDto> Me(CancellationToken ct) => users.MeAsync(ct);
}

[ApiController]
[Route("api/users")]
[Tags("Usuarios")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List([FromQuery] PageQuery query, CancellationToken ct) => users.ListAsync(query, ct);

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return Created($"/api/users/{user.Id}", user);
    }

    [HttpPut("{id:int}")]
    public Task<UserDto> Update(int id, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request, ct);

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, ChangePasswordRequest request, CancellationToken ct)
    {
        await users.ChangePasswordAsync(id, request, ct);
        return NoContent();
    }
}
