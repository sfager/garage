# Copilot Instructions for Garage

## Build & Test Commands

```bash
# Run the application
dotnet run --project src/Garage.Web --launch-profile https

# Run all tests (NUnit)
dotnet test

# Run a single test class
dotnet test --filter ClassName=Garage.Domain.Tests.VehicleTests

# Run a single test method
dotnet test --filter Name=TestMethodName_Condition_ExpectedOutcome

# Create a new database migration
dotnet ef migrations add MigrationName --project src/Garage.Infrastructure --startup-project src/Garage.Infrastructure --output-dir Persistence/Migrations

# Generate VAPID keys for push notifications
dotnet run --project tools/GenerateVapidKeys
```

## Architecture Overview

**Onion Architecture with four layers** where dependencies point inward only:

```
Garage.Web  ──────►  Garage.Infrastructure  ──────►  Garage.Application  ──────►  Garage.Domain
    │                                                        ▲
    └────────────────────────────────────────────────────────┘
```

| Project | Role | Constraints |
|---------|------|-----------|
| **Garage.Domain** | Entities, invariants, enums, domain services | No framework references. Own the repository interface contracts. |
| **Garage.Application** | Application services, DTOs, abstractions | Depends only on Domain. Abstractions for clock, current user, file store, notifications, lookups. |
| **Garage.Infrastructure** | EF Core, SQL Server, repositories, Identity | Implements Application abstractions. Persistence layer. |
| **Garage.Web** | Blazor components, layout, presentation logic | Implements only `ICurrentUser` and `ISelectedVehicleStore`. References Infrastructure only in `Program.cs`. |

### Repository Interfaces Live in Domain

Repository interfaces (e.g. `IVehicleRepository`, `IReportRepository`) belong to `Garage.Domain/Repositories` — the contract sits with the model it describes. Domain-owned types like `CostLine` and `OdometerPoint` are defined in the interface file itself.

## Key Conventions

### Domain Entities

- Inherit from `Entity` (in `Garage.Domain/Common`).
- Use **private fields** backing **public properties** for collections and mutable state.
- Validate invariants in the constructor and public methods; throw `DomainException` on violation.
- All writes to related entities are transaction-like: the entity method updates both sides (e.g., `Vehicle.RecordTrip` updates the vehicle's odometer and adds to its trip collection).

```csharp
public class Vehicle : Entity
{
    private readonly List<Reminder> _reminders = [];
    public IReadOnlyList<Reminder> Reminders => _reminders.AsReadOnly();
    
    public void UpdateReminder(Reminder reminder)
    {
        if (reminder.VehicleId != Id)
            throw new DomainException("Reminder does not belong to this vehicle.");
        
        _reminders.Remove(reminder);
        _reminders.Add(reminder);
    }
}
```

### Nullable Reference Types & Implicit Usings

- All projects have `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- Use `?` to mark nullable properties; initialize non-nullable properties or make them required.
- `using` statements are implicit at the namespace level; do not repeat common namespaces.

### Testing: NUnit Patterns

- **Framework**: NUnit (not xUnit).
- **Scope**: 107 unit tests for Domain rules only. Application services are tested manually via the running app; there are no integration or component tests.
- **Naming**: `Test{MethodName}_{Condition}_{ExpectedOutcome}`. Example: `TestRecordReading_WhenReadingIsBelowTheLastOne_ThrowsDomainException`.
- **Structure**: Arrange / Act / Assert sections with comments labeling each.
- **Test data**: Use realistic values (e.g., `88_000` miles, `new DateOnly(2026, 8, 1)`). Household IDs are static `Guid`s within a test class; vehicle IDs are generated.

```csharp
[TestFixture]
public class VehicleTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private Vehicle _vehicle = default!;

    [SetUp]
    public void Setup()
    {
        _vehicle = new Vehicle(HouseholdId, "Outback", 88_000, new DateOnly(2026, 8, 1));
    }

    [Test]
    public void TestRecordReading_WhenReadingMovesForward_UpdatesCurrentOdometer()
    {
        // Arrange
        var newOdometer = 88_412;

        // Act
        _vehicle.RecordReading(new DateOnly(2026, 8, 13), newOdometer);

        // Assert
        Assert.That(_vehicle.CurrentOdometer, Is.EqualTo(newOdometer));
    }
}
```

## Domain Concepts

### Household & Multi-User Sharing

- A **Household** is a shared garage. Multiple users belong to one household; vehicles belong to a household, so two people can see the same cars.
- **Invitations**: Single-use, 160-bit codes (no I, O, 0, 1), expired after 7 days, stored only as SHA-256 hash.
- When joining with existing cars, they move to the shared garage along with full history.
- Last person cannot leave while cars remain; leaving otherwise gives that person a fresh garage.

### Vehicle & Odometer

- The vehicle owns the single running odometer value. It **only moves forward** — attempts to go backward throw `DomainException` and name both values in the error message.
- Each odometer change is recorded as an `OdometerReading` with a source enum: `VehicleSetup`, `Trip`, `ServiceRecord`, `FuelEntry`, `ManualReading`.

### Reminders: Two Shapes

- **Interval-based**: Every 5,000 miles, every 6 months, or whichever comes first.
- **One-shot (fixed-date)**: A reminder that fires on a specific day (e.g., registration expiry). Created via `Reminder.OnDate`, carries a `FixedDueDate`, never repeats after service.
- Both project through the same `ReminderProjector` and sort alongside each other.

### Fuel: Tank-to-Tank MPG

- MPG is **full fill to full fill**. A full fill establishes a tank baseline; partial fills count toward gallons but never get their own MPG.
- **Cost per mile** = (total fuel cost + total service cost) / total miles. It answers "what does this car cost to run?" — both fuel and maintenance together.
- Where a metric cannot be computed (not enough data), display why instead of showing zero.

### Service Records: Three-Step Wizard

- The wizard lives at `/log` (step 1), `/log/cost` (step 2), `/log/notes` (step 3).
- State is held by a scoped `ServiceLogWizard` and persisted to protected local storage so Back is lossless.
- Receipts are written to the file store immediately on upload (they survive restart); the `Document` row waits for save.
- Discarding a draft deletes those uploaded files.

### Documents & File Upload

- Vehicle photos, receipts, and documents are written to `Garage:FileStorageRoot` with **generated storage keys** (never the client filename).
- Served from `/files/{key}` by an authorized endpoint that checks the file belongs to the caller's household.
- Returns 404 for unowned files (not 403) so the endpoint doesn't confirm file existence.
- Uploads are checked against `UploadPolicy` (allowlist); content type is derived from that allowlist, not from the client claim.
- Non-safe-to-render files are sent as `Content-Disposition: attachment`; all files get `X-Content-Type-Options: nosniff`.

### Push Notifications

- Delivery is **web push**: a service worker receives it whether or not the app is open.
- Hourly `NotificationSweepService` looks for due reminders and documents inside the 30-day expiry window.
- Each sent notification is recorded against the due point to dedupe standing items, but retries on delivery failure (so transient outages don't swallow the message).
- Requires a VAPID key pair (generate with `dotnet run --project tools/GenerateVapidKeys`). Each browser subscribes separately; iOS needs the site added to home screen.

### VIN Validation

- Checks: length, excluded letters (I, O, Q), and check digit at position 9.
- A failed check digit **warns** rather than blocks (mandatory in North America, not in Europe).
- VIN lookup uses the public [NHTSA vPIC API](https://vpic.nhtsa.dot.gov/api/) with a 10-second timeout. Any failure (unknown VIN, service down, no network) returns a failed result; the UI falls back to manual entry. No OCR fallback.

### Barcode Scanning

- Uses the browser's `BarcodeDetector` for Code 39 / Code 128. **No OCR fallback** — a wrongly-read but confident VIN is worse than no VIN.
- A scan that fails offers retry; manual entry is always available alongside it.

## Commonly Edited Code Locations

| Feature | File Path | What to Know |
|---------|-----------|--------------|
| Domain entities | `src/Garage.Domain/Entities/*.cs` | Inherit from `Entity`; throw `DomainException`; use private fields. |
| Repository contracts | `src/Garage.Domain/Repositories/I*.cs` | Define types you need alongside the interface. |
| Application services | `src/Garage.Application/{Feature}/*.cs` | Depend on Application & Domain; implement `IApplicationService` if needed. |
| Infrastructure repositories | `src/Garage.Infrastructure/Persistence/*.cs` | Implement Domain repository interfaces. |
| Blazor pages | `src/Garage.Web/Components/Pages/*.razor` | Mark as `[Authorize]`; avoid touching `GarageDbContext` directly. |
| Database migrations | `src/Garage.Infrastructure/Persistence/Migrations/*.cs` | Generated; do not hand-edit. |
| Domain tests | `tests/Garage.Domain.Tests/*.cs` | NUnit; Arrange/Act/Assert; test invariants, not implementation. |
| Entity Framework config | `src/Garage.Infrastructure/Persistence/Configurations/*.cs` | Override `OnModelCreating`; map Domain entities to tables. |

## Database & SQL Server

- **Target**: SQL Server 2022+.
- **Connection**: Development string in `appsettings.Development.json`; override without editing via user-secrets.
- **Migrations**: Applied on startup in Development; `GarageDbContextFactory` provides design-time connection string via `GARAGE_CONNECTION` env var.
- **Default user-secrets** (Development only):
  - `ConnectionStrings:DefaultConnection` — SQL Server connection.
  - `Garage:Vapid:PublicKey`, `Garage:Vapid:Subject` — push notification VAPID.
  - `Garage:SeedDemoData` — seed demo account (default `true`).

## Security Notes

A security review was completed. Key findings and mitigations:

| Issue | Mitigation |
|-------|-----------|
| Stored XSS via uploaded `.html` files | Uploads checked against allowlist; content-type derived from allowlist, not client claim; non-safe files sent as attachment with `X-Content-Type-Options: nosniff`. |
| Unsubscribe without ownership check | Subscriptions now verified to belong to caller's household. |
| Unlimited password guessing | `lockoutOnFailure: true` enabled. |
| Unscoped public helper | Made private with explanatory comment. |
| **Verified sound**: All routable pages have `[Authorize]`; repository lookups scoped to household; file endpoint verifies ownership and returns 404; invitation codes are 160-bit, hash-stored, single-use, expiring; no raw SQL or raw HTML rendering; antiforgery, HTTPS redirection, HSTS all in place. |

### Known Gaps

- **Plate lookup (V-1)**: No commercial provider configured; manual entry fallback.
- **Fuel rows in history (R-2)**: Fuel row opens Fuel screen instead of detail page (fuel entries have no detail page).
- **Push delivery (S-5)**: Unit-tested; not proven on a real browser (dev browser blocks notifications).
- **Barcode scanning (V-3)**: Failure paths tested; reading actual barcode unverified.
- **Test shape**: Domain has 107 unit tests; Application services tested via running app, not via unit tests.
- **Unverified production setup**: Default `appsettings.json` contains demo `sa` password; override before production.

## .NET Version & Project Settings

- **.NET**: 10.0 target framework.
- **Language features**: `LangVersion = latest`, `Nullable = enable`, `ImplicitUsings = enable`.
- **Starting point**: `src/Garage.Web/Program.cs` registers Infrastructure in DI.
- **No analyzers** configured beyond NUnit.Analyzers (for test projects). No StyleCop, FxCop, or similar.

## When Copilot Suggests Code

- Verify it follows the Onion Architecture: right layer, right dependencies.
- Check entity suggestions preserve the private-field pattern.
- Test suggestions must follow NUnit conventions (not xUnit).
- Avoid suggesting integration tests or component tests; the codebase has none.
- Push notification code is complex; read `Garage.Infrastructure/Notifications/` thoroughly before suggesting changes.
- File upload paths must respect the allowlist and key generation; avoid suggesting raw filename use.
