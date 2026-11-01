using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Application.Common;
using PuntoVenta.Domain.Common;
using PuntoVenta.Domain.Entities;
using PuntoVenta.Domain.Enums;

namespace PuntoVenta.Application.Features.Users;

public record LoginRequest(string Email, string Password);
public record UserDto(int Id, string Email, string FullName, Role Role, bool IsActive, DateTime CreatedAt);
public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
public record CreateUserRequest(string Email, string FullName, string Password, Role Role);
public record UpdateUserRequest(string FullName, Role Role, bool IsActive);
public record ChangePasswordRequest(string NewPassword);

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public static class PasswordRules
{
    /// <summary>Política mínima: 8+ caracteres con mayúscula, minúscula y dígito.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir un dígito.");
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator() => RuleFor(x => x.NewPassword).StrongPassword();
}

public class UserService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    ICurrentUser currentUser,
    IValidator<LoginRequest> loginValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IValidator<ChangePasswordRequest> passwordValidator)
{
    public static UserDto ToDto(User u) => new(u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt);

    /// <returns>null si las credenciales no son válidas.</returns>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Mismo resultado para "no existe", "inactivo" y "contraseña errada":
        // así no se revela qué correos están registrados (enumeración de usuarios).
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            return null;

        var token = tokens.Create(user);
        return new LoginResponse(token.Token, token.ExpiresAt, ToDto(user));
    }

    public async Task<UserDto> MeAsync(CancellationToken ct)
    {
        var user = await db.Users.FindAsync([currentUser.Id], ct) ?? throw new NotFoundException("Usuario", currentUser.Id);
        return ToDto(user);
    }

    public Task<PagedResult<UserDto>> ListAsync(PageQuery query, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(u => u.Email.Contains(s) || u.FullName.ToLower().Contains(s));
        }
        q = q.ApplySort(query, new Dictionary<string, System.Linq.Expressions.Expression<Func<User, object>>>
        {
            ["fullName"] = u => u.FullName,
            ["email"] = u => u.Email,
            ["role"] = u => u.Role,
            ["createdAt"] = u => u.CreatedAt,
        }, "fullName");
        return q.ToPagedAsync(query, u => new UserDto(u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt), ct);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException($"Ya existe un usuario con el correo {email}.");

        var user = new User(email, request.FullName, hasher.Hash(request.Password), request.Role);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var user = await db.Users.FindAsync([id], ct) ?? throw new NotFoundException("Usuario", id);

        // Evita que el administrador se bloquee a sí mismo y deje el sistema sin admin.
        if (id == currentUser.Id && (request.Role != Role.Admin || !request.IsActive))
            throw new DomainException("No puede quitarse el rol de administrador ni desactivarse a sí mismo.");

        user.Update(request.FullName, request.Role, request.IsActive);
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task ChangePasswordAsync(int id, ChangePasswordRequest request, CancellationToken ct)
    {
        await passwordValidator.ValidateAndThrowAsync(request, ct);
        var user = await db.Users.FindAsync([id], ct) ?? throw new NotFoundException("Usuario", id);
        user.ChangePassword(hasher.Hash(request.NewPassword));
        await db.SaveChangesAsync(ct);
    }
}
