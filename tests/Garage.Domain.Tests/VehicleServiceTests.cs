using Garage.Application.Abstractions;
using Garage.Application.Vehicles;
using Garage.Domain.Common;
using Garage.Domain.Entities;
using Garage.Domain.Repositories;
using NUnit.Framework;

namespace Garage.Domain.Tests;

[TestFixture]
public class VehicleServiceTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 13);

    private FakeVehicleRepository _repository = default!;
    private VehicleService _service = default!;

    [SetUp]
    public void Setup()
    {
        _repository = new FakeVehicleRepository();
        _service = new VehicleService(
            _repository,
            new FakeCurrentUser(HouseholdId),
            new FakeUnitOfWork(),
            new FakeClock(Today),
            new FakeFileStore());
    }

    [Test]
    public async Task TestAddAsync_WhenTheRequestIsComplete_CreatesTheVehicleWithItsStartingOdometer()
    {
        // Arrange
        var request = new AddVehicleRequest
        {
            Nickname = "Outback",
            Odometer = 88_412,
            Year = 2019,
            Make = "Subaru",
            Model = "Outback",
            Vin = "4s4bsanc1k3311204"
        };

        // Act
        var vehicle = await _service.AddAsync(request);

        // Assert
        Assert.That(vehicle.CurrentOdometer, Is.EqualTo(88_412));
        Assert.That(vehicle.CurrentOdometerDate, Is.EqualTo(Today));
        Assert.That(vehicle.HouseholdId, Is.EqualTo(HouseholdId));
    }

    [Test]
    public async Task TestAddAsync_WhenAVinIsGiven_StoresItUppercased()
    {
        // Arrange
        var request = new AddVehicleRequest
        {
            Nickname = "Outback",
            Odometer = 100,
            Vin = "4s4bsanc1k3311204"
        };

        // Act
        var vehicle = await _service.AddAsync(request);

        // Assert
        Assert.That(vehicle.Vin, Is.EqualTo("4S4BSANC1K3311204"));
    }

    [Test]
    public void TestAddAsync_WhenTheOdometerIsMissing_ThrowsDomainException()
    {
        // Arrange — story V-2 makes the odometer required to finish the add flow.
        var request = new AddVehicleRequest { Nickname = "Outback", Odometer = null };

        // Act & Assert
        Assert.ThrowsAsync<DomainException>(() => _service.AddAsync(request));
    }

    [Test]
    public async Task TestAddAsync_WhenTheVinIsAlreadyInTheGarage_ThrowsDomainException()
    {
        // Arrange
        await _service.AddAsync(new AddVehicleRequest { Nickname = "Outback", Odometer = 100, Vin = "4S4BSANC1K3311204" });
        var duplicate = new AddVehicleRequest { Nickname = "Copy", Odometer = 200, Vin = "4s4bsanc1k3311204" };

        // Act & Assert
        Assert.ThrowsAsync<DomainException>(() => _service.AddAsync(duplicate));
    }

    [Test]
    public async Task TestArchiveAsync_WhenCalled_HidesTheVehicleButKeepsItInTheFullList()
    {
        // Arrange — story V-4: archived vehicles leave the switcher, not the reports.
        var vehicle = await _service.AddAsync(new AddVehicleRequest { Nickname = "Fit", Odometer = 1_000 });

        // Act
        await _service.ArchiveAsync(vehicle.Id);

        // Assert
        var active = await _repository.ListActiveAsync(HouseholdId);
        var all = await _repository.ListAllAsync(HouseholdId);
        Assert.That(active, Is.Empty);
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TestRestoreAsync_WhenCalledOnAnArchivedVehicle_ReturnsItToTheSwitcher()
    {
        // Arrange
        var vehicle = await _service.AddAsync(new AddVehicleRequest { Nickname = "Fit", Odometer = 1_000 });
        await _service.ArchiveAsync(vehicle.Id);

        // Act
        await _service.RestoreAsync(vehicle.Id);

        // Assert
        var active = await _repository.ListActiveAsync(HouseholdId);
        Assert.That(active, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TestUpdateAsync_WhenTheNicknameChanges_SavesTheNewName()
    {
        // Arrange
        var vehicle = await _service.AddAsync(new AddVehicleRequest { Nickname = "Fit", Odometer = 1_000 });
        var request = new EditVehicleRequest { Id = vehicle.Id, Nickname = "The Fit", Year = 2018, Make = "Honda" };

        // Act
        await _service.UpdateAsync(request);

        // Assert
        Assert.That(vehicle.Nickname, Is.EqualTo("The Fit"));
        Assert.That(vehicle.Year, Is.EqualTo(2018));
    }

    [Test]
    public void TestUpdateAsync_WhenTheVehicleBelongsToAnotherHousehold_ThrowsDomainException()
    {
        // Arrange — the repository only returns vehicles for the caller's household.
        var request = new EditVehicleRequest { Id = Guid.NewGuid(), Nickname = "Someone else's car" };

        // Act & Assert
        Assert.ThrowsAsync<DomainException>(() => _service.UpdateAsync(request));
    }

    [Test]
    public async Task TestDeleteAsync_WhenCalled_RemovesTheVehicleEntirely()
    {
        // Arrange
        var vehicle = await _service.AddAsync(new AddVehicleRequest { Nickname = "Fit", Odometer = 1_000 });

        // Act
        await _service.DeleteAsync(vehicle.Id);

        // Assert
        var all = await _repository.ListAllAsync(HouseholdId);
        Assert.That(all, Is.Empty);
    }

    // ---- test doubles -------------------------------------------------------

    private sealed class FakeVehicleRepository : IVehicleRepository
    {
        private readonly List<Vehicle> _vehicles = [];

        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_vehicles.FirstOrDefault(v => v.Id == id));

        public Task AddAsync(Vehicle entity, CancellationToken cancellationToken = default)
        {
            _vehicles.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(Vehicle entity) => _vehicles.Remove(entity);

        public Task<IReadOnlyList<Vehicle>> ListActiveAsync(Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Vehicle>>(
                [.. _vehicles.Where(v => v.HouseholdId == householdId && !v.IsArchived)]);

        public Task<IReadOnlyList<Vehicle>> ListAllAsync(Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Vehicle>>([.. _vehicles.Where(v => v.HouseholdId == householdId)]);

        public Task<Vehicle?> GetForHouseholdAsync(Guid vehicleId, Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_vehicles.FirstOrDefault(v => v.Id == vehicleId && v.HouseholdId == householdId));

        public Task<bool> VinExistsAsync(string vin, Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_vehicles.Any(v => v.HouseholdId == householdId && v.Vin == vin));

        public Task<bool> OwnsStoredFileAsync(string storageKey, Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_vehicles.Any(v => v.HouseholdId == householdId
                && (v.PhotoPath == storageKey || v.Documents.Any(d => d.StoragePath == storageKey))));

        public Task<VehicleDeletionImpact?> GetDeletionImpactAsync(Guid vehicleId, Guid householdId, CancellationToken cancellationToken = default)
        {
            var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId && v.HouseholdId == householdId);
            return Task.FromResult(vehicle is null
                ? null
                : new VehicleDeletionImpact(vehicle.Nickname, vehicle.ServiceRecords.Count, vehicle.FuelEntries.Count,
                    vehicle.OdometerReadings.Count, vehicle.Trips.Count, vehicle.Reminders.Count,
                    vehicle.Documents.Count, 0m));
        }
    }

    private sealed class FakeCurrentUser(Guid householdId) : ICurrentUser
    {
        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string?> GetUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("test-user");
        public Task<string?> GetDisplayNameAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("Test");
        public Task<Guid> GetHouseholdIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(householdId);
        public Task<Guid?> TryGetHouseholdIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(householdId);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public DateOnly Today => today;
    }

    private sealed class FakeFileStore : IFileStore
    {
        public Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default) =>
            Task.FromResult($"{folder}/{fileName}");

        public Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetUrl(string storageKey) => $"/files/{storageKey}";
    }
}
