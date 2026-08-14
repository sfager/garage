using Garage.Domain.Common;

namespace Garage.Domain.Entities;

/// <summary>
/// The unit of sharing. Two people who share the same cars belong to one household,
/// and every vehicle hangs off exactly one.
/// </summary>
public class Household : Entity
{
    private readonly List<Vehicle> _vehicles = [];

    private Household() { }

    public Household(string name)
    {
        Rename(name);
    }

    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<Vehicle> Vehicles => _vehicles;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A household needs a name.");
        }

        Name = name.Trim();
    }
}
