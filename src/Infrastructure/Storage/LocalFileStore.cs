using Garage.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Garage.Infrastructure.Storage;

public class FileStoreOptions
{
    /// <summary>Root directory uploads are written to, relative to the content root.</summary>
    public string Root { get; set; } = "App_Data/files";

    /// <summary>Path prefix the app serves stored files from.</summary>
    public string RequestPath { get; set; } = "/files";
}

/// <summary>
/// Stores uploads on the local disk. Storage keys are relative paths ("vehicles/{guid}.jpg")
/// generated here, never taken from the client, so an uploaded name cannot escape the root.
/// </summary>
public class LocalFileStore(FileStoreOptions options, ILogger<LocalFileStore> logger) : IFileStore
{
    public async Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
        {
            extension = string.Empty;
        }

        var safeFolder = string.Concat(folder.Where(char.IsLetterOrDigit));
        var key = $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destination = ResolveWithinRoot(key);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using (var file = File.Create(destination))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return key;
    }

    public Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolveWithinRoot(storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = ResolveWithinRoot(storageKey);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            // A stranded file is not worth failing the user's action over.
            logger.LogWarning(ex, "Could not delete stored file {Key}", storageKey);
        }

        return Task.CompletedTask;
    }

    public string GetUrl(string storageKey) => $"{options.RequestPath}/{storageKey}";

    /// <summary>Resolves a key under the root and refuses anything that escapes it.</summary>
    private string ResolveWithinRoot(string storageKey)
    {
        var root = Path.GetFullPath(options.Root);
        var resolved = Path.GetFullPath(Path.Combine(root, storageKey));

        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("That storage key points outside the file store.");
        }

        return resolved;
    }
}
