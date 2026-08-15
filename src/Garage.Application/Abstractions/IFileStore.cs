namespace Garage.Application.Abstractions;

/// <summary>
/// Where uploaded files live — vehicle photos now, receipts and documents later.
/// The Application layer deals in opaque storage keys and never in paths.
/// </summary>
public interface IFileStore
{
    /// <summary>Stores the stream and returns the key needed to read it back.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default);

    Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>The URL a browser can fetch the file from.</summary>
    string GetUrl(string storageKey);
}
