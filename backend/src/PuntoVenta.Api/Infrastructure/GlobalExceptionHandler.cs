using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuntoVenta.Domain.Common;

namespace PuntoVenta.Api.Infrastructure;

/// <summary>
/// Punto único que traduce excepciones a respuestas RFC 9457 (ProblemDetails).
/// Los controladores quedan sin try/catch y el cliente siempre recibe el mismo formato.
/// </summary>
public class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException ve => new ValidationProblemDetails(
                ve.Errors.GroupBy(e => ToCamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Datos de entrada inválidos",
            },
            DomainException de => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Regla de negocio incumplida",
                Detail = de.Message,
                Extensions = { ["code"] = de.Code },
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Recurso no encontrado",
                Detail = exception.Message,
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto",
                Detail = exception.Message,
            },
            // Carrera entre dos peticiones que pasan la validación previa y chocan en un índice único.
            DbUpdateException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto",
                Detail = "La operación entra en conflicto con datos existentes. Intente nuevamente.",
            },
            ForbiddenException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acceso denegado",
                Detail = exception.Message,
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No autenticado",
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno",
                // Nunca se expone el detalle técnico al cliente; queda en el log con el traceId.
                Detail = "Ocurrió un error inesperado. Si persiste, contacte a soporte con el traceId.",
            },
        };

        if (problem.Status >= 500)
            logger.LogError(exception, "Error no controlado en {Method} {Path}", http.Request.Method, http.Request.Path);
        else
            logger.LogInformation("Petición rechazada ({Status}): {Message}", problem.Status, exception.Message);

        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static string ToCamelCase(string name) =>
        string.Join('.', name.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
