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
| `src/Garage.Domain` | Entities, ValueObjects, invariants, enums, Repository interfaces/abstractions. No framework references at all. | — |
| `src/Garage.Application` | Service abstractions, application services, DTOs. | Domain |
| `src/Garage.Infrastructure` | EF Core SQL Server context, configurations, repositories, migrations, ASP.NET Identity user. | Application |
| `src/Garage.Web` | Blazor components, layout, and the presentation-owned implementations of `ICurrentUser` and `ISelectedVehicleStore`. | Application, Infrastructure |
| `tests/Garage.Domain.Tests` | NUnit tests for the domain rules. | Domain, Application |

The Web project references Infrastructure only so `Program.cs` can register it. No component
touches `GarageDbContext` directly — they go through the Application abstractions.

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

## Notifications (story S-5) — partially built

The per-reminder switch, the active-reminders list with their triggers, and the in-app
surfacing of due items on Home are all working. What is **not** built is delivery: nothing
reaches the user while the app is closed. That needs a channel decision — web push
(service worker plus VAPID keys), email (an SMTP or API provider), or both — and a
background job to evaluate due points on a schedule. `Reminder.NotificationsEnabled` is
the flag such a job would honour.

The backlog schedules S-5 in phase 7, so this gap is expected at this point.

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
| E6 · Documents | D-1, D-2, D-3 | Not started |
| E7 · Reports | R-1 – R-4 | Not started |
| Phase 7 · Polish | V-3, S-5, G-3, R-3, R-4 | Not started |
