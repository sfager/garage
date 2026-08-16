using Garage.Application.Abstractions;
using Garage.Domain.Repositories;
using Garage.Infrastructure.Notifications;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Persistence.Repositories;
using Garage.Infrastructure.Services;
using Garage.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        FileStoreOptions? fileStore = null,
        VapidOptions? vapid = null,
        NotificationSweepOptions? sweep = null)
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
        services.AddScoped<IFuelRepository, FuelRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<ISentNotificationRepository, SentNotificationRepository>();
        services.AddScoped<INotificationScanRepository, NotificationScanRepository>();
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

        // Story S-5: web push. Without VAPID keys the sender reports itself
        // unconfigured and the sweep stands down rather than failing.
        services.AddSingleton(vapid ?? new VapidOptions());
        services.AddSingleton<IPushSender, WebPushSender>();

        var sweepOptions = sweep ?? new NotificationSweepOptions();
        services.AddSingleton(sweepOptions);

        if (sweepOptions.Enabled)
        {
            services.AddHostedService<NotificationSweepService>();
        }

        return services;
    }
}
