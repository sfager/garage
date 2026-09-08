namespace Garage.Domain.Common;

/// <summary>Base for every persisted aggregate and child record.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
