using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PuntoVenta.Application.Features.CashSessions;
using PuntoVenta.Application.Features.Catalog;
using PuntoVenta.Application.Features.Customers;
using PuntoVenta.Application.Features.Reports;
using PuntoVenta.Application.Features.Sales;
using PuntoVenta.Application.Features.Users;

namespace PuntoVenta.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registra todos los validadores FluentValidation del ensamblado.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<UserService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<CashSessionService>();
        services.AddScoped<SaleService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
