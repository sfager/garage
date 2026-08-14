namespace Garage.Application.Abstractions;

/// <summary>Injected so "due soon" and "expires in 21 days" are testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
}
