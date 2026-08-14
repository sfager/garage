namespace Garage.Domain.Common;

/// <summary>
/// Raised when an operation would break a domain rule. The message is written for
/// the user, not the log, because the UI surfaces it directly.
/// </summary>
public class DomainException(string message) : Exception(message);
