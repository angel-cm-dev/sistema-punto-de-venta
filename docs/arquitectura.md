# Arquitectura

## Capas

```mermaid
flowchart TB
    API["PuntoVenta.Api<br/>Controllers · JWT · ProblemDetails · Swagger · Serilog"]
    APP["PuntoVenta.Application<br/>Servicios de caso de uso · DTOs · FluentValidation · Paginación"]
    DOM["PuntoVenta.Domain<br/>Entidades · Reglas (IGV, RUC/DNI, arqueo) · Excepciones"]
    INF["PuntoVenta.Infrastructure<br/>EF Core (Npgsql/SQLite) · Migraciones · BCrypt · JWT · Seed"]
    API --> APP --> DOM
    INF --> APP
    API -. composición DI .-> INF
```

| Capa | Responsabilidad | Depende de |
|---|---|---|
| **Domain** | Entidades con setters privados y métodos que validan invariantes (`Sale.Create`, `CashSession.Close`, `Product.AdjustStock`). Servicios puros: `TaxCalculator`, `IdentityDocumentValidator`. | Nada |
| **Application** | Casos de uso (`SaleService`, `CashSessionService`, `ReportService`…): orquestan dominio y persistencia, aplican autorización a nivel de datos y validan la entrada. Define los puertos `IAppDbContext`, `ICurrentUser`, `IPasswordHasher`, `ITokenService`. | Domain, abstracciones de EF Core |
| **Infrastructure** | Implementa los puertos: `AppDbContext` con configuraciones Fluent API, migraciones de PostgreSQL, `BCryptPasswordHasher`, `JwtTokenService`, `DataSeeder`. | Application |
| **Api** | HTTP: rutas, autenticación/autorización por rol, rate limiting, manejo global de errores, health checks, OpenAPI. Adaptador `HttpCurrentUser`. | Application, Infrastructure (solo para registrar DI) |

## Flujo de una petición: `POST /api/sales`

```mermaid
sequenceDiagram
    autonumber
    participant UI as React (PosPage)
    participant MW as Middleware (JWT, RateLimiter, ExceptionHandler)
    participant C as SalesController
    participant S as SaleService
    participant D as Sale (dominio)
    participant DB as PostgreSQL

    UI->>MW: POST /api/sales + Bearer token
    MW->>C: Usuario autenticado (claims sub, role)
    C->>S: CreateAsync(request)
    S->>S: FluentValidation (400 si falla)
    S->>DB: ¿Caja abierta del cajero? productos activos, cliente
    S->>D: Sale.Create(líneas con precio de BD, pagos, descuento)
    D-->>S: Totales, IGV, vuelto (422 si viola una regla)
    S->>DB: BEGIN
    loop por cada ítem
        S->>DB: UPDATE products SET stock = stock - q WHERE id = ? AND stock >= q
        DB-->>S: filas afectadas (0 ⇒ 422 stock_insuficiente ⇒ ROLLBACK)
    end
    S->>DB: UPDATE document_series SET last_number = last_number + 1 (bloquea la fila)
    S->>DB: INSERT sale, items, payments
    S->>DB: COMMIT
    S-->>C: SaleDto
    C-->>UI: 201 Created + comprobante B001-00000027
```

Si cualquier paso lanza una excepción, `GlobalExceptionHandler` la traduce a ProblemDetails (`400/403/404/409/422/500`) y, como la transacción nunca llegó al `COMMIT`, el `await using` la revierte.

## Decisiones clave

- **El dominio protege sus invariantes**: no hay forma de construir una `Sale` con totales inconsistentes o una factura sin RUC; las reglas se prueban sin base de datos (38 pruebas unitarias).
- **Precios desde la base de datos**: el cliente HTTP solo envía `productId` y `cantidad`; nunca precios.
- **Stock atómico** (ver [ADR-0001](decisiones/0001-descuento-de-stock-atomico.md)).
- **`IAppDbContext` en lugar de repositorios genéricos** (ver [ADR-0002](decisiones/0002-dbcontext-como-puerto.md)).
- **SQLite para pruebas y desarrollo local** (ver [ADR-0003](decisiones/0003-sqlite-para-pruebas.md)).
- **Fechas en UTC, días en hora de Lima**: los reportes agrupan con desfase fijo UTC−5 (Perú no usa horario de verano).
- **Autorización en dos niveles**: por rol en el endpoint (`[Authorize(Roles = "Admin")]`) y por propietario en el servicio (un cajero solo ve sus ventas y cajas).
- **Errores homogéneos**: todos los errores siguen RFC 9457 e incluyen `traceId` para correlacionar con los logs.

## Seguridad

- Contraseñas con BCrypt (factor 12) y política de complejidad.
- JWT HS256 con emisor, audiencia y expiración validados; la clave se inyecta por variable de entorno y la API se niega a arrancar con una clave corta.
- Mensaje de login genérico (no revela si el correo existe) y rate limiting de 10 intentos/minuto por IP.
- Contenedores ejecutándose con usuarios sin privilegios; nginx agrega cabeceras de seguridad básicas.
- Restricciones `CHECK` en base de datos (stock ≥ 0, precio > 0) y unicidad de `(serie, número)` como defensa en profundidad.
