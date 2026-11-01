using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PuntoVenta.Api.Infrastructure;
using PuntoVenta.Application;
using PuntoVenta.Application.Abstractions;
using PuntoVenta.Infrastructure;
using PuntoVenta.Infrastructure.Persistence;
using PuntoVenta.Infrastructure.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging estructurado ----------
// Serilog escribe JSON compacto en contenedores (fácil de indexar en Loki/ELK) y texto legible en desarrollo.
builder.Services.AddSerilog((_, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("app", "punto-de-venta-api"));

// ---------- Capas (inyección de dependencias) ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Autenticación JWT ----------
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres (configúrelo con la variable Jwt__Key).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Se conservan los nombres de claim cortos ("sub", "role") tal como se emiten.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = JwtTokenService.RoleClaim,
            NameClaimType = JwtTokenService.SubjectClaim,
        };
    });
builder.Services.AddAuthorization();

// ---------- Limitador de intentos de login (mitiga fuerza bruta) ----------
var loginPermits = builder.Configuration.GetValue("RateLimiting:LoginPermitPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromMinutes(1) }));
});

// ---------- CORS (solo necesario si el frontend corre en otro origen, p. ej. Vite en :5173) ----------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---------- Salud y documentación ----------
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Punto de Venta API",
        Version = "v1",
        Description = "API REST del sistema POS: catálogo, caja, ventas con IGV y reportes.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Pegue el token obtenido en POST /api/auth/login",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = [],
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "PuntoVenta.Api.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Punto de Venta API v1");
        c.DocumentTitle = "Punto de Venta API";
    });
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await app.Services.InitializeDatabaseAsync(app.Configuration);
await app.RunAsync();

/// <summary>Expuesto para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
