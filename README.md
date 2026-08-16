# Garage

A household vehicle log — maintenance, repairs, mileage, fuel and documents for two or
three cars. Blazor Web App on .NET 10, SQL Server, Onion Architecture.

Built from the `Vehicle Tracker Wireframes` and `Vehicle Tracker Backlog` design documents,
following the backlog's build order one epic at a time.

## Architecture

Dependencies point inward. Nothing in an inner layer knows the layer outside it.

```
Garage.Web  ──────►  Garage.Infrastructure  ──────►  Garage.Application  ──────►  Garage.Domain
    │                                                        ▲
    └────────────────────────────────────────────────────────┘
```

| Project | Contains | Depends on |
| --- | --- | --- |
| `src/Garage.Domain` | Entities, invariants, enums. No framework references at all. | — |
| `src/Garage.Application` | Repository and service abstractions, application services, DTOs. | Domain |
| `src/Garage.Infrastructure` | EF Core SQL Server context, configurations, repositories, migrations, ASP.NET Identity user. | Application |
| `src/Garage.Web` | Blazor components, layout, and the presentation-owned implementations of `ICurrentUser` and `ISelectedVehicleStore`. | Application, Infrastructure |
| `tests/Garage.Domain.Tests` | NUnit tests for the domain rules. | Domain, Application |

The Web project references Infrastructure only so `Program.cs` can register it. No component
touches `GarageDbContext` directly — they go through the Application abstractions.

### Where repository interfaces live

Repository interfaces live in **`Garage.Domain/Repositories`** — the contract sits with the
model it describes. Because the Domain depends on nothing outside itself, an interface
there may only speak in Domain types: `IReportRepository` defines its own `CostLine` and
`OdometerPoint`, and `VehicleDeletionImpact` sits beside the interface that returns it.

```
Garage.Domain/Repositories     IRepository<T>, IHouseholdRepository, IVehicleRepository,
                               IMileageRepository, IReminderRepository,
                               IServiceRecordRepository, IFuelRepository,
                               IDocumentRepository, IReportRepository
Garage.Application/Abstractions  IClock, ICurrentUser, IUnitOfWork, IFileStore,
                                 ISelectedVehicleStore, IServiceDraftStore,
                                 IVehicleLookupService
```

The split is by what the interface describes, not by who calls it. A repository describes
how the model is read and written, so it belongs to the model. The abstractions left in
Application describe application concerns — the clock, the signed-in user, the commit
boundary, where files and drafts are kept, an external lookup service — none of which the
Domain has an opinion about.

## Design decisions taken from the backlog's "Open decisions"

| Decision | Choice |
| --- | --- |
| Maintenance layout | **1c** — grouped list (overdue / due soon / later) with an Upcoming\|History toggle |
| Service logging | **1f** — full-screen three-step wizard, each step a route |
| Fuel screen | **1h** stats strip over the log, with the **1i** trend chart above it |
| Units | Miles and gallons only, no metric option |
| Accounts | Multi-user. Users belong to a `Household`; vehicles belong to a household, so two people can share the same cars |
| Render mode | Blazor Server (`InteractiveServer`) |

## Running it

You need a SQL Server instance. Any of these works:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_password123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

Then point the app at it — the development connection string lives in
`src/Garage.Web/appsettings.Development.json`, or override it without touching the file:

```bash
dotnet user-secrets --project src/Garage.Web set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=Garage-dev;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
```

Run it:

```bash
dotnet run --project src/Garage.Web --launch-profile https
```

In Development the app applies migrations on startup. With `Garage:SeedDemoData` set to
`true` (the default for Development) it also creates a demo account whose household already
holds the two cars from the wireframes:

```
demo@garage.local / Demo123!
```

Set `Garage:SeedDemoData` to `false` to start with an empty garage instead.

Registering a new account creates a household for that user on first use, so a new
sign-up starts with an empty garage. To let a second person share the same cars, point
their `AspNetUsers.HouseholdId` at the existing household.

## VIN lookup

Story V-1 decodes VINs through [NHTSA vPIC](https://vpic.nhtsa.dot.gov/api/), which is
public, free and needs no API key. It only covers vehicles sold in the United States,
which matches the miles-and-gallons decision. The call has a 10-second timeout and never
throws: any failure — unknown VIN, service down, no network — returns a failed result and
the UI falls back to manual entry with whatever was already typed.

Plate lookup has no free equivalent; turning a plate into a VIN needs a commercial data
provider. Until one is configured, choosing "Plate" says so and goes to manual entry.
To add one, implement `IVehicleLookupService` and swap the registration in
`AddGarageInfrastructure`.

## The service wizard's draft (story L-4)

The three steps at `/log`, `/log/cost` and `/log/notes` share one `ServiceDraft`, held by
the scoped `ServiceLogWizard` and persisted to protected local storage on the device. That
is what makes Back lossless and lets an abandoned entry be resumed from Home — including
after a restart. A draft is scratch work rather than a record, so it stays out of the
database until the user saves.

Receipts are an exception: the file is written to the store as soon as it is picked, so it
survives the same restart, and only the `Document` row waits for the save. Discarding a
draft deletes those files.

## Reminders have two shapes

Most reminders are **interval-based**: every 5,000 miles, every 6 months, or whichever
comes first. Story D-2 added a second shape — a **one-shot reminder that fires on a given
day**, which is what a registration expiring on 3 September actually is. `Reminder.OnDate`
creates these; they carry a `FixedDueDate`, never repeat after service, and project through
the same `ReminderProjector` so they band and sort alongside everything else.

## Uploaded files

Vehicle photos — and later receipts and documents — are written under
`Garage:FileStorageRoot` with generated storage keys, never the client's filename.
They are served from `/files/{key}` by an authorized endpoint that checks the file
belongs to a vehicle in the caller's household, so they are not readable by another
household even if the key leaks. A key the caller does not own returns 404 rather than
403, so the endpoint does not confirm that a file exists.

## How efficiency and cost per mile are worked out

**MPG is tank-to-tank.** A full fill returns the tank to a known level, so the fuel burned
since the last full fill is everything added in between. Partial fills are therefore
counted towards the gallons but never given an MPG of their own — attributing one to a
half-tank top-up would invent a number. The first full fill only establishes a baseline.
The average aggregates total miles over total gallons rather than averaging the per-fill
figures, so a short tank does not weigh as much as a long one.

**Cost per mile counts fuel and service together**, matching the reports screen [1m]. It is
"what this car costs to run", not "what its fuel costs" — the epic is *fuel and running
costs*. The trend chart's `$ / mi` and `Spend` metrics use the same definition, so the
chart and the stats strip above it never disagree about what a label means. `MPG` is
fuel-only by nature.

Where a figure cannot be computed, the screen states why instead of showing a zero — a
zero here reads as "this car does nought to the gallon" rather than "we cannot tell yet".

## Notifications (story S-5)

Delivery is **web push**: a service worker receives the notification whether or not the app
is open. An hourly background sweep (`NotificationSweepService`) looks for reminders whose
trigger has arrived and documents inside the 30-day expiry window, and pushes them to every
browser the household has subscribed.

A due point stays due until the work is done, so each notification is recorded against the
due point it described (`SentNotifications`). That keeps a standing item quiet while still
speaking up when the due point moves — after a service, or once a snooze runs out. A
delivery that fails is deliberately *not* recorded, so a transient outage retries rather
than swallowing the notification.

### Setting it up

Push needs a VAPID key pair. Without one the sender reports itself unconfigured, the sweep
stands down, and the settings page says so rather than failing quietly. Generate a pair:

```bash
dotnet run --project tools/GenerateVapidKeys
```

Then store them — never in a committed settings file:

```bash
dotnet user-secrets --project src/Garage.Web set "Garage:Vapid:PublicKey" "<public>"
```

Other settings: `Garage:Vapid:Subject` (a `mailto:` or `https:` URL identifying the app),
`Garage:Notifications:Enabled`, `Garage:Notifications:IntervalMinutes`.

Each browser subscribes separately at `/settings/notifications` — turning it on at a desk
does not cover a phone. Individual reminders can still be silenced one at a time on the
reminders screen [1k]. iOS only delivers push to a site added to the home screen.

## VIN scanning (story V-3)

The scanner reads the Code 39 / Code 128 barcode on the door jamb using the browser's
`BarcodeDetector`. There is deliberately **no OCR fallback**: a VIN read wrongly but
confidently is worse than no VIN, and the manual field is always beside it. A scan that
cannot be read offers a retry and never takes manual entry away.

A scanned VIN is checked before it is trusted — length, the excluded letters I/O/Q, and the
check digit in position nine, which catches most single-character misreads. A failed check
digit *warns* rather than blocks: it is mandatory in North America but not in Europe, so a
genuine European VIN can fail it.

## Working with the schema

Migrations live in `src/Garage.Infrastructure/Persistence/Migrations`.

```bash
dotnet ef migrations add <Name> --project src/Garage.Infrastructure --startup-project src/Garage.Infrastructure --output-dir Persistence/Migrations
```

`GarageDbContextFactory` supplies a design-time connection string, overridable with the
`GARAGE_CONNECTION` environment variable. It is only used for scaffolding, never at runtime.

## Tests

```bash
dotnet test
```

## Build status against the backlog

| Epic | Stories | State |
| --- | --- | --- |
| E0 · Foundations | F-1, F-2, F-3 | Done — verified against SQL Server 2022 |
| E1 · Garage and onboarding | V-1, V-2, V-4 | Done — verified against SQL Server 2022 |
| E2 · Mileage | M-1, M-2, M-3 | Done — verified against SQL Server 2022 |
| E3 · Maintenance and reminders | S-1 – S-6 | Done, except notification delivery — see below |
| E4 · Logging a service | L-1 – L-4 | Done — verified against SQL Server 2022 |
| E5 · Fuel and running costs | G-1, G-2, G-3 | Done — verified against SQL Server 2022 |
| E6 · Documents | D-1, D-2, D-3 | Done — verified against SQL Server 2022 |
| E7 · Reports | R-1 – R-4 | Done — verified against SQL Server 2022 |
| Phase 7 · Polish | V-3, S-5 | Done — G-3, R-3 and R-4 shipped earlier, in E5 and E7 |
