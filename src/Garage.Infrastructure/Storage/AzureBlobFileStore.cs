using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Garage.Application.Abstractions;
using Garage.Application.Files;
using Microsoft.Extensions.Logging;

namespace Garage.Infrastructure.Storage;

/// <summary>
/// Stores uploads in Azure Blob Storage.
/// Storage keys are relative paths ("vehicles/{guid}.jpg") that map directly to blob names.
/// Authentication uses DefaultAzureCredential which automatically detects:
/// - Managed Identity in Azure App Service (production)
/// - Azure CLI credentials (local development with 'az login')
/// - Visual Studio credentials (local development when signed in)
/// - Environment variables (for service principals)
/// </summary>
public class AzureBlobFileStore : IFileStore
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobFileStore> _logger;
    private readonly string _requestPath;

    public AzureBlobFileStore(AzureBlobStorageOptions options, ILogger<AzureBlobFileStore> logger)
    {
        _logger = logger;
        _requestPath = "/files";

        // Create BlobServiceClient with DefaultAzureCredential
        BlobServiceClient serviceClient;

        // For local development with Azurite (HTTP), use connection string
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            _logger.LogInformation("Using Azure Blob Storage connection string");
            serviceClient = new BlobServiceClient(options.ConnectionString);
        }
        // For production (HTTPS), use ServiceUrl with DefaultAzureCredential
        else if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            if (options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "HTTP endpoints require a connection string. " +
                    "Set Garage:FileStorage:AzureBlob:ConnectionString for Azurite or other HTTP endpoints.");
            }

            _logger.LogInformation("Using custom blob service endpoint with DefaultAzureCredential: {Endpoint}", options.ServiceUrl);
            serviceClient = new BlobServiceClient(new Uri(options.ServiceUrl), new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "Azure Blob Storage configuration is incomplete. " +
                "Either set Garage:FileStorage:AzureBlob:ConnectionString (for local development) " +
                "or Garage:FileStorage:AzureBlob:ServiceUrl (for production with DefaultAzureCredential).");
        }

        _containerClient = serviceClient.GetBlobContainerClient(options.ContainerName);

        // Auto-create container if it doesn't exist (confirmed design decision)
        try
        {
            _containerClient.CreateIfNotExists(PublicAccessType.None);
            _logger.LogInformation("Blob container '{ContainerName}' is ready", options.ContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or access blob container '{ContainerName}'", options.ContainerName);
            throw;
        }
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        // Generate safe storage key (same logic as LocalFileStore)
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
        {
            extension = string.Empty;
        }

        var safeFolder = string.Concat(folder.Where(char.IsLetterOrDigit));
        var storageKey = $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        // Get blob client
        var blobClient = _containerClient.GetBlobClient(storageKey);

        // Determine content type from UploadPolicy
        var contentType = UploadPolicy.ResolveContentType(storageKey) ?? "application/octet-stream";

        try
        {
            // Upload blob with content type metadata
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            await blobClient.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders
            }, cancellationToken);

            _logger.LogDebug("Uploaded blob {StorageKey} ({ContentType})", storageKey, contentType);
            return storageKey;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to upload blob {StorageKey}", storageKey);
            throw new InvalidOperationException($"Failed to upload file to Azure Blob Storage: {ex.Message}", ex);
        }
    }

    public async Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);

        try
        {
            // Check if blob exists
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return null;
            }

            // Download blob to a MemoryStream
            var memoryStream = new MemoryStream();
            await blobClient.DownloadToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob not found
            return null;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to download blob {StorageKey}", storageKey);
            throw new InvalidOperationException($"Failed to download file from Azure Blob Storage: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);

        try
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            _logger.LogDebug("Deleted blob {StorageKey}", storageKey);
        }
        catch (RequestFailedException ex)
        {
            // A stranded blob is not worth failing the user's action over
            _logger.LogWarning(ex, "Could not delete blob {StorageKey}", storageKey);
        }
    }

    public string GetUrl(string storageKey)
    {
        // Return same /files/{storageKey} format as LocalFileStore
        // The authorized endpoint in Program.cs will call OpenAsync() to stream the file
        // This maintains household authorization and works for both providers
        return $"{_requestPath}/{storageKey}";
    }
}
