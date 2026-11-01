namespace PuntoVenta.Domain.Common;

/// <summary>
/// Violación de una regla de negocio. La capa API la traduce a HTTP 422
/// (la petición es sintácticamente válida pero el negocio la rechaza).
/// </summary>
public class DomainException(string message, string code = "regla_negocio") : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Recurso inexistente → HTTP 404.</summary>
public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' no existe.");

/// <summary>Conflicto con el estado actual (duplicados, concurrencia) → HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>El usuario está autenticado pero no puede operar sobre el recurso → HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);
