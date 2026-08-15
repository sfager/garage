using Garage.Application.Abstractions;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Persistence.Repositories;
using Garage.Infrastructure.Services;
using Garage.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// The Identity schema version shapes the model — Version3 adds the passkey tables —
    /// so it is owned here, alongside the migrations, rather than by whoever calls
    /// AddIdentityCore. Design time and runtime must agree on it or EF reports the
    /// model as having pending changes.
    /// </summary>
    public static void ConfigureIdentitySchema(IdentityOptions options) =>
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

    /// <summary>
    /// Wires the SQL Server context, the repository implementations and the ambient
    /// services the Application layer declares but does not implement.
    /// </summary>
    public static IServiceCollection AddGarageInfrastructure(
        this IServiceCollection services,
        string connectionString,
        FileStoreOptions? fileStore = null)
    {
        services.Configure<IdentityOptions>(ConfigureIdentitySchema);

        services.AddDbContext<GarageDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(GarageDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure();
            }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GarageDbContext>());
        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IMileageRepository, MileageRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IServiceRecordRepository, ServiceRecordRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<GarageDbSeeder>();

        services.AddSingleton(fileStore ?? new FileStoreOptions());
        services.AddSingleton<IFileStore, LocalFileStore>();

        // vPIC is a public service, so a slow or missing response must not hold a page
        // open; the lookup reports failure and the user types the details instead.
        services.AddHttpClient<IVehicleLookupService, NhtsaVehicleLookupService>(client =>
        {
            client.BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
