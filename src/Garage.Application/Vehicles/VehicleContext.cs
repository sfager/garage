using Garage.Application.Abstractions;
using Garage.Domain.Entities;

namespace Garage.Application.Vehicles;

/// <summary>
/// Story F-3. Holds the vehicle every screen is currently showing, restores the
/// choice from the persisted store on first use, and tells subscribers when it changes
/// so open pages reload rather than showing another car's data.
///
/// Several components on one page ask for the garage at once — Blazor initialises a
/// child while its parent's own initialisation is still in flight — and they share one
/// scoped DbContext, which permits no concurrent operations. So loads are deduplicated
/// onto a single in-flight task and every database-touching method is serialised.
/// </summary>
public class VehicleContext(
    IVehicleRepository vehicles,
    ICurrentUser currentUser,
    ISelectedVehicleStore store) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<VehicleSummary> _garage = [];
    private Task? _loadTask;

    /// <summary>Raised after the selection or the garage list changes.</summary>
    public event Func<Task>? Changed;

    public VehicleSummary? Selected { get; private set; }

    public IReadOnlyList<VehicleSummary> Garage => _garage;

    public bool HasVehicles => _garage.Count > 0;

    /// <summary>
    /// Loads the garage once per circuit. Concurrent callers await the same load
    /// rather than issuing a second query on the shared context.
    /// </summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        _loadTask ??= LoadOnceAsync(cancellationToken);

    private async Task LoadOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReloadAsync(cancellationToken);
        }
        catch
        {
            // A failed load must not be remembered as done, or the page stays empty forever.
            _loadTask = null;
            throw;
        }
    }

    /// <summary>Re-reads the garage and re-resolves the selection. Call after adding or archiving.</summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (await currentUser.TryGetHouseholdIdAsync(cancellationToken) is { } householdId)
            {
                var active = await vehicles.ListActiveAsync(householdId, cancellationToken);
                _garage = [.. active.Select(VehicleSummary.From)];

                var storedId = await store.GetAsync(cancellationToken);
                Selected = _garage.FirstOrDefault(v => v.Id == storedId) ?? _garage.FirstOrDefault();

                if (Selected is not null && Selected.Id != storedId)
                {
                    await store.SetAsync(Selected.Id, cancellationToken);
                }
            }
            else
            {
                _garage = [];
                Selected = null;
            }
        }
        finally
        {
            _gate.Release();
        }

        _loadTask = Task.CompletedTask;
        await NotifyAsync();
    }

    public async Task SelectAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var match = _garage.FirstOrDefault(v => v.Id == vehicleId);
        if (match is null || match.Id == Selected?.Id)
        {
            return;
        }

        Selected = match;
        await store.SetAsync(vehicleId, cancellationToken);
        await NotifyAsync();
    }

    /// <summary>
    /// Loads the full aggregate for the selected vehicle, scoped to the household.
    /// Returns null when the garage is empty or the selection has gone stale.
    /// </summary>
    public async Task<Vehicle?> GetSelectedVehicleAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        if (Selected is null)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
            return await vehicles.GetForHouseholdAsync(Selected.Id, householdId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Refreshes the cached summary after a write moved the odometer.</summary>
    public async Task RefreshSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (Selected is null)
        {
            return;
        }

        Vehicle? vehicle;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var householdId = await currentUser.GetHouseholdIdAsync(cancellationToken);
            vehicle = await vehicles.GetForHouseholdAsync(Selected.Id, householdId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        if (vehicle is null)
        {
            await ReloadAsync(cancellationToken);
            return;
        }

        var summary = VehicleSummary.From(vehicle);
        Selected = summary;
        _garage = [.. _garage.Select(v => v.Id == summary.Id ? summary : v)];
        await NotifyAsync();
    }

    /// <summary>
    /// Invokes subscribers one at a time. A multicast Func&lt;Task&gt; returns only the
    /// last handler's task from Invoke(), so awaiting that alone would leave every
    /// earlier handler's database work running unobserved — and this context is shared,
    /// which permits no concurrent operations.
    /// </summary>
    private async Task NotifyAsync()
    {
        if (Changed is null)
        {
            return;
        }

        foreach (var handler in Changed.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }

    public void Dispose() => _gate.Dispose();
}
