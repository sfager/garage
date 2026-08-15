using Garage.Application;
using Garage.Application.Abstractions;
using Garage.Infrastructure;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Storage;
using Garage.Web.Components;
using Garage.Web.Components.Account;
using Garage.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var fileStoreOptions = new FileStoreOptions
{
    Root = Path.Combine(builder.Environment.ContentRootPath,
        builder.Configuration.GetValue("Garage:FileStorageRoot", "App_Data/files")!),
    RequestPath = "/files"
};

// Onion wiring: the Web layer knows both inner layers, and neither knows it.
builder.Services.AddGarageInfrastructure(connectionString, fileStoreOptions);
builder.Services.AddGarageApplication();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Presentation-owned implementations of Application abstractions.
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ISelectedVehicleStore, SelectedVehicleStore>();
builder.Services.AddScoped<IServiceDraftStore, ServiceDraftStore>();

// The Identity schema version is set by AddGarageInfrastructure, which owns the
// migrations that have to match it.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<GarageDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    await app.MigrateAndSeedAsync();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

Directory.CreateDirectory(fileStoreOptions.Root);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

// Uploaded photos, receipts and documents. Not served as plain static files: every
// request is checked against the caller's household, so an unguessed storage key is
// not the only thing keeping one household out of another's paperwork.
app.MapGet($"{fileStoreOptions.RequestPath}/{{**storageKey}}", async (
        string storageKey,
        HttpContext http,
        UserManager<ApplicationUser> users,
        IVehicleRepository vehicles,
        IFileStore files,
        IContentTypeProvider contentTypes,
        CancellationToken cancellationToken) =>
    {
        // This is a plain HTTP request rather than a Blazor circuit, so the household
        // comes from the signed-in principal directly, not from ICurrentUser.
        var user = await users.GetUserAsync(http.User);
        if (user is null || user.HouseholdId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var householdId = user.HouseholdId;

        if (!await vehicles.OwnsStoredFileAsync(storageKey, householdId, cancellationToken))
        {
            // Deliberately a 404, not a 403: a household should not learn that a file
            // it cannot see exists at all.
            return Results.NotFound();
        }

        var stream = await files.OpenAsync(storageKey, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound();
        }

        if (!contentTypes.TryGetContentType(storageKey, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return Results.File(stream, contentType);
    })
    .RequireAuthorization();

app.Run();
