using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without starting the web host. The connection
/// string here is only ever used for scaffolding migrations, never at runtime.
/// </summary>
public class GarageDbContextFactory : IDesignTimeDbContextFactory<GarageDbContext>
{
    public GarageDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GARAGE_CONNECTION")
                               ?? "Server=(localdb)\\mssqllocaldb;Database=MyApps;Integrated Security=True;Trusted_Connection=True;MultipleActiveResultSets=true";

        // IdentityDbContext reads the schema version off the application service
        // provider while building the model. Without this the scaffolded migration
        // would describe the Version1 Identity schema while the running app expects
        // Version3, and every startup would fail with "the model has pending changes".
        var applicationServices = new ServiceCollection()
            .Configure<IdentityOptions>(DependencyInjection.ConfigureIdentitySchema)
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<GarageDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(GarageDbContext).Assembly.FullName))
            .UseApplicationServiceProvider(applicationServices)
            .Options;

        return new GarageDbContext(options);
    }
}
