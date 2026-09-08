namespace Garage.Application.Files;

/// <summary>
/// What may be uploaded, and how it is allowed to come back out.
///
/// Uploaded files are served from the application's own origin, so an HTML or SVG file
/// served as itself would run script as the signed-in user — and in a shared household
/// that means one member attacking another. The type is therefore decided here, from an
/// allowlist, and never taken from what the browser claimed at upload time.
/// </summary>
public static class UploadPolicy
{
    /// <summary>Extension to the content type we will serve it as. Nothing else is stored.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".pdf"] = "application/pdf"
    };

    /// <summary>
    /// Types safe to render in the browser. Everything else is sent as a download, so it
    /// cannot execute in this origin even if the allowlist is widened later.
    /// </summary>
    private static readonly HashSet<string> InlineSafe = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/heic", "application/pdf"
    };

    public static string Describe => "JPEG, PNG, GIF, WebP, HEIC or PDF";

    /// <summary>Images only, for a vehicle photo.</summary>
    public static bool IsImage(string fileName) =>
        ResolveContentType(fileName) is { } type && type.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>The content type to store and serve, or null when the file is not allowed.</summary>
    public static string? ResolveContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return Allowed.GetValueOrDefault(extension);
    }

    public static bool IsAllowed(string fileName) => ResolveContentType(fileName) is not null;

    /// <summary>True when the browser may render it rather than download it.</summary>
    public static bool CanRenderInline(string contentType) => InlineSafe.Contains(contentType);
}
