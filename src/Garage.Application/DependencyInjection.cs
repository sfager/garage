using Garage.Application.Vehicles;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application services. Everything here depends only on the
    /// abstractions in <c>Garage.Application.Abstractions</c>, which Infrastructure supplies.
    /// </summary>
    public static IServiceCollection AddGarageApplication(this IServiceCollection services)
    {
        services.AddScoped<VehicleContext>();
        return services;
    }
}
