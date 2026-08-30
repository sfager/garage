using Garage.Application;
using Garage.Application.Abstractions;
using Garage.Application.Files;
using Garage.Domain.Repositories;
using Garage.Infrastructure;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Notifications;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Storage;
using Garage.Web.Components;
using Garage.Web.Components.Account;
using Garage.Web.Services.Api;
using Garage.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var fileStoreOptions = new FileStoreOptions
{
    Root = Path.Combine(builder.Environment.ContentRootPath,
        builder.Configuration.GetValue("Garage:FileStorageRoot", "App_Data/files")!),
    RequestPath = "/files"
};

// Story S-5: web push. Keys come from configuration or user-secrets; without them the
// sender reports itself unconfigured and the settings page says so plainly.
var vapidOptions = new VapidOptions
{
    Subject = builder.Configuration.GetValue("Garage:Vapid:Subject", "mailto:garage@example.com")!,
    PublicKey = builder.Configuration["Garage:Vapid:PublicKey"],
    PrivateKey = builder.Configuration["Garage:Vapid:PrivateKey"]
};

var sweepOptions = new NotificationSweepOptions
{
    Enabled = builder.Configuration.GetValue("Garage:Notifications:Enabled", true),
    Interval = TimeSpan.FromMinutes(builder.Configuration.GetValue("Garage:Notifications:IntervalMinutes", 60))
};

// Onion wiring: the Web layer knows both inner layers, and neither knows it.
builder.Services.AddGarageInfrastructure(connectionString, fileStoreOptions, vapidOptions, sweepOptions);
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
builder.Services.AddTransient<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<VehicleApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<MaintenanceApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<DocumentApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<ReportApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<HouseholdApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<NotificationApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<MileageApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
builder.Services.AddHttpClient<FuelApiClient>(ConfigureApiHttpClient)
    .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();

void ConfigureApiHttpClient(IServiceProvider sp, HttpClient client)
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (http is not null)
    {
        client.BaseAddress = new Uri($"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/");
        return;
    }

    var configuredBaseUrl = sp.GetService<IConfiguration>()?["Garage:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
    {
        client.BaseAddress = new Uri(configuredBaseUrl, UriKind.Absolute);
        return;
    }

    throw new InvalidOperationException(
        "Cannot resolve API base address. Either use these clients within an HTTP request or set Garage:BaseUrl.");
}

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

app.MapControllers();

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

        // The type comes from the allowlist, not from sniffing and not from whatever the
        // uploader claimed. Anything not safe to render is sent as a download, and nosniff
        // stops the browser second-guessing either decision.
        var contentType = UploadPolicy.ResolveContentType(storageKey) ?? "application/octet-stream";
        var inline = UploadPolicy.CanRenderInline(contentType);

        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.ContentDisposition = inline ? "inline" : "attachment";
        http.Response.Headers.CacheControl = "private, max-age=0, no-store";

        return Results.File(stream, contentType);
    })
    .RequireAuthorization();

app.Run();
