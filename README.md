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

## Database Setup

Before running the application, you must have a SQL Server instance running and the database
initialized with migrations. Choose the instructions for your operating system.

### Windows: SQL Server LocalDB

**LocalDB** is a lightweight, file-based SQL Server instance included with Visual Studio and
the .NET SDK. It's ideal for local development.

#### Option 1: Use LocalDB (recommended for Windows)

If you have Visual Studio or the .NET SDK installed, LocalDB is likely already available.
Verify it by running:

```bash
sqllocaldb info mssqllocaldb
```

If `mssqllocaldb` is listed, skip to "Configure and Initialize the Database" below.

#### Option 2: Install LocalDB Separately (if needed)

If LocalDB is not available, download and install **SQL Server Express with LocalDB**:
1. Visit [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
2. Download the **Express** edition
3. Run the installer and choose **Local DB** during setup
4. Verify installation with `sqllocaldb info mssqllocaldb`

#### Configure and Initialize the Database

By default, the app uses the LocalDB connection string `(localdb)\mssqllocaldb`. No
additional configuration is needed — just run the application.

However, if you prefer to initialize the database before starting the app, or if you've
configured a different connection string, apply the migrations manually:

```bash
dotnet ef database update --project src/Garage.Infrastructure
```

Then start the app:

```bash
dotnet run --project src/Garage.Web --launch-profile https
```

### macOS & Linux: Docker-based SQL Server

SQL Server is available as a Docker container, which is the standard approach on macOS
and Linux.

#### Start the SQL Server Container

Run the following command to start a SQL Server 2022 container:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_password123" -p 1433:1433 --name myapps-mssql -d mcr.microsoft.com/mssql/server:2022-latest
```

Replace `Your_password123` with a strong password. The container will be accessible at
`localhost:1433`.

#### Configure the Connection String

Store the connection string in user secrets (so it doesn't appear in version control):

```bash
dotnet user-secrets --project src/Garage.Web set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=MyApps;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True"
```

Replace `YOUR_PASSWORD` with the password you used above.

#### Initialize the Database

Apply migrations to create the database schema:

```bash
dotnet ef database update --project src/Garage.Infrastructure
```

### Verify the Database Was Created

After running `dotnet ef database update`, verify the database was created by connecting
to SQL Server:

**Windows (LocalDB)**:
```bash
sqlcmd -S "(localdb)\mssqllocaldb" -d MyApps -Q "SELECT @@VERSION"
```

**macOS/Linux (Docker)**:
```bash
docker exec myapps-mssql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YOUR_PASSWORD_HERE" -Q "SELECT @@VERSION"
```

If you see version information, the database connection is working.

## Running the Application

Once the database is initialized, start the application:

```bash
dotnet run --project src/Garage.Web --launch-profile https
```

The app applies migrations on startup in Development mode, so the database will be
automatically updated if any pending migrations exist.

### Demo Data

By default in Development, the app seeds a demo account with sample data:

```
demo@garage.local / Demo123!
```

This demo account includes the two cars from the wireframes. To start with an empty garage
instead, set:

```bash
dotnet user-secrets --project src/Garage.Web set "Garage:SeedDemoData" "false"
```

To re-enable seeding later:

```bash
dotnet user-secrets --project src/Garage.Web set "Garage:SeedDemoData" "true"
```

New user accounts created through registration start with an empty garage and can invite
others to share their household.

## Sharing a garage with someone

Accounts are per person; a **household** is what makes two people see the same cars. At
`/settings/household` you can invite someone, see who is already there, withdraw an
invitation, and leave.

An invitation is a single-use code that expires after seven days. Only a SHA-256 hash of
it is stored — the code itself is shown once, when it is created, and a leaked database
should not hand anybody the keys to a garage. Codes avoid I, O, 0 and 1 so one read aloud
stays the same code.

Two cases are handled explicitly rather than left to chance:

- **The joiner already has cars.** They move into the shared garage along with all their
  history, and the join screen says so *before* the decision, naming the count. Their old,
  now-empty household is removed.
- **The last person tries to leave.** Refused while cars remain, because it would leave
  them unreachable. Leaving otherwise gives that person a fresh, empty garage; the shared
  cars stay with whoever is left, which the confirmation states.

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

## Uploaded files and storage configuration

Vehicle photos — and later receipts and documents — are written under the configured file
storage provider with generated storage keys, never the client's filename. They are served
from `/files/{key}` by an authorized endpoint that checks the file belongs to a vehicle in
the caller's household, so they are not readable by another household even if the key leaks.
A key the caller does not own returns 404 rather than 403, so the endpoint does not confirm
that a file exists.

### File Storage Configuration

Garage supports two file storage providers:

#### Local File System (Default)

Stores files on the local disk. Suitable for single-server deployments and local development.

```json
{
  "Garage": {
    "FileStorage": {
      "Provider": "Local",
      "LocalRoot": "App_Data/files"
    }
  }
}
```

The `LocalRoot` can be an absolute path or relative to the application's content root.
If not specified, defaults to `App_Data/files`.

**Legacy configuration** (still supported):
```json
{
  "Garage": {
    "FileStorageRoot": "App_Data/files"
  }
}
```

#### Azure Blob Storage

Stores files in Azure Blob Storage. **Recommended for cloud deployments** and multi-instance
Azure App Services, where local file system storage is ephemeral and cannot be shared
across instances.

**Production (Azure App Service with Managed Identity):**

```json
{
  "Garage": {
    "FileStorage": {
      "Provider": "AzureBlob",
      "AzureBlob": {
        "ContainerName": "garage-files",
        "ServiceUrl": "https://yourstorageaccount.blob.core.windows.net"
      }
    }
  }
}
```

Or configure via environment variables in Azure App Service:
```
Garage__FileStorage__Provider=AzureBlob
Garage__FileStorage__AzureBlob__ContainerName=garage-files
Garage__FileStorage__AzureBlob__ServiceUrl=https://yourstorageaccount.blob.core.windows.net
```

**Authentication:** Uses `DefaultAzureCredential` which automatically detects:
- **Managed Identity** in Azure App Service (production)
- **Azure CLI credentials** (`az login`) for local development
- **Visual Studio credentials** when signed in to Azure
- **Environment variables** for service principals

**Azure App Service setup:**
1. Create an Azure Storage Account (Standard_LRS or better)
2. Create a blob container named `garage-files` (or your chosen name)
3. Enable **System-Assigned Managed Identity** on your App Service
4. Grant the Managed Identity **Storage Blob Data Contributor** role on the storage account
   (IAM → Add role assignment)
5. Configure the App Service settings (Provider, ContainerName, ServiceUrl)

The blob container is created automatically on first use if it doesn't exist.

**Local Development with Azurite:**

[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) is a
free, open-source Azure Storage emulator perfect for local development.

1. Install Azurite:
   ```bash
   npm install -g azurite
   ```

2. Start Azurite:
   ```bash
   azurite --silent --location c:\azurite
   ```

   Or use Visual Studio's integrated Azurite:
   **View** → **Other Windows** → **Azure Storage Emulator (Azurite)**

3. Configure `appsettings.Development.json`:
   ```json
   {
     "Garage": {
       "FileStorage": {
         "Provider": "AzureBlob",
         "AzureBlob": {
           "ContainerName": "garage-files",
           "ServiceUrl": "http://127.0.0.1:10000/devstoreaccount1"
         }
       }
     }
   }
   ```

4. Run the application — the container is created automatically.

**Migrating from Local to Azure Blob Storage:**

Existing files in `App_Data/files/` are not automatically migrated. To move them:

1. Use Azure Storage Explorer or the Azure CLI:
   ```bash
   az storage blob upload-batch \
     --account-name yourstorageaccount \
     --destination garage-files \
     --source App_Data/files \
     --auth-mode login
   ```

2. Preserve the folder structure — storage keys like `vehicles/abc123.jpg` must remain
   unchanged as they're stored in the database.

3. Update configuration to `"Provider": "AzureBlob"` and restart the application.

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

## Security

A review was run over the whole codebase. What it found, and what was done:

| Finding | Resolution |
| --- | --- |
| **Stored XSS via uploaded files** — an uploaded `.html` was served from the app's own origin as `text/html`, so opening a "document" ran script as the signed-in user. In a shared household, one member could have attacked another. | Uploads are now checked against a server-side allowlist (`UploadPolicy`), the content type is derived from that allowlist rather than from what the browser claimed, anything not safe to render is sent as `Content-Disposition: attachment`, and every file is served with `X-Content-Type-Options: nosniff`. Files uploaded before the fix are neutralised by the serving rules. |
| **Unsubscribe had no ownership check** — `PushSubscriptionService.UnsubscribeAsync` deleted by endpoint alone, so anyone holding another household's endpoint could silence its notifications. | The subscription must belong to the caller's household. |
| **Unlimited password guessing** — the scaffolded sign-in passed `lockoutOnFailure: false`. | Failures now count towards lockout. |
| **An unscoped public helper** — `MaintenanceService.GetMilesPerDayAsync` did not scope to the household. Not reachable from outside, but a future caller could have passed an untrusted id. | Made private, with a comment saying why. |

Checked and found sound: every routable page carries `[Authorize]` (only the error and not-found pages do not); every repository lookup by id is scoped to the caller's household, and the file endpoint verifies ownership and returns 404 rather than 403; invitation codes are 160-bit, stored only as a SHA-256 hash, single-use and expiring; storage keys are generated, never taken from the client, and path traversal is refused; no raw SQL and no raw HTML rendering anywhere; antiforgery, HTTPS redirection and HSTS are all in place.

## Known gaps

Every story in the backlog is implemented. These are the places where an acceptance
criterion is met only partly, or where verification stopped short of proof.

| Gap | Detail |
| --- | --- |
| **Plate lookup (V-1)** | No provider is wired — plate-to-VIN needs a commercial service. Choosing "Plate" says so and goes to manual entry. |
| **Fuel rows in the history table (R-2)** | "Rows open the underlying record" holds for service records; a fuel row opens the Fuel screen rather than that entry, because fill-ups have no detail page. |
| **Push delivery (S-5) unproven** | The sweep, dedup and subscription handling are unit-tested, but no notification has been delivered to a real browser: the development browser blocks notifications and service workers. The last hop needs confirming on a real device. |
| **Barcode scanning (V-3) unproven** | The failure paths are verified; reading an actual VIN barcode needs a camera and a plate to point it at. |
| **Test shape** | The Domain is covered by 107 unit tests. Application services — maintenance, reporting, fuel, documents, the wizard — were verified by exercising the running app, not by unit tests. There are no integration tests against SQL Server and no component tests. |
| **Two accepted risks from the security review** | Email addresses are not verified at sign-up (`RequireConfirmedAccount = false`), because no email sender is configured — re-enable it once one is. And `appsettings.json` ships a default SQL Server connection string containing an `sa` password; production must override it, and it should be emptied before this is deployed anywhere real. |




