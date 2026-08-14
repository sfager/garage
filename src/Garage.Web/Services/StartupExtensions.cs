using Garage.Domain.Entities;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Garage.Web.Services;

public static class StartupExtensions
{
    private const string DemoEmail = "demo@garage.local";
    private const string DemoPassword = "Demo123!";

    /// <summary>
    /// Development convenience: bring the database up to the latest migration and,
    /// when <c>Garage:SeedDemoData</c> is on, create a demo account whose household
    /// already contains the two cars from the wireframes.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<WebApplication>>();

        var context = services.GetRequiredService<GarageDbContext>();
        await context.Database.MigrateAsync();

        if (!app.Configuration.GetValue("Garage:SeedDemoData", false))
        {
            return;
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(DemoEmail);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DemoEmail,
                Email = DemoEmail,
                EmailConfirmed = true,
                DisplayName = "Demo"
            };

            var created = await users.CreateAsync(user, DemoPassword);
            if (!created.Succeeded)
            {
                logger.LogWarning("Could not create the demo user: {Errors}",
                    string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Demo sign-in: {Email} / {Password}", DemoEmail, DemoPassword);
        }

        if (user.HouseholdId == Guid.Empty)
        {
            var household = new Household("Demo garage");
            context.Households.Add(household);
            await context.SaveChangesAsync();

            user.HouseholdId = household.Id;
            await users.UpdateAsync(user);
        }

        var seeder = services.GetRequiredService<GarageDbSeeder>();
        await seeder.SeedHouseholdAsync(user.HouseholdId);
    }
}
