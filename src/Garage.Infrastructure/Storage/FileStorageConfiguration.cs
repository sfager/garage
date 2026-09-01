namespace Garage.Infrastructure.Storage;

/// <summary>
/// Configuration for file storage provider selection and settings.
/// Supports both local file system and Azure Blob Storage providers.
/// </summary>
public class FileStorageConfiguration
{
    /// <summary>
    /// Storage provider to use: "Local" or "AzureBlob".
    /// Default is "Local" for backward compatibility.
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Configuration for Azure Blob Storage provider.
    /// Required when Provider is "AzureBlob".
    /// </summary>
    public AzureBlobStorageOptions? AzureBlob { get; set; }

    /// <summary>
    /// Root directory for local file storage.
    /// Used when Provider is "Local".
    /// Can be absolute or relative to content root.
    /// </summary>
    public string? LocalRoot { get; set; }
}

/// <summary>
/// Configuration options for Azure Blob Storage file provider.
/// Authentication uses DefaultAzureCredential which supports:
/// - Managed Identity (production Azure App Service)
/// - Azure CLI credentials (local development)
/// - Visual Studio credentials (local development)
/// </summary>
public class AzureBlobStorageOptions
{
    /// <summary>
    /// Name of the blob container to store files in.
    /// Container will be created automatically if it doesn't exist.
    /// </summary>
    public string ContainerName { get; set; } = "garage-files";

    /// <summary>
    /// Optional: Custom blob service endpoint URL.
    /// Use for Azurite local development: "http://127.0.0.1:10000/devstoreaccount1"
    /// Leave empty/null for production Azure Storage endpoint.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Optional: Connection string for Azure Blob Storage.
    /// Use for local development with Azurite or other HTTP endpoints.
    /// Leave empty/null for production with DefaultAzureCredential.
    /// </summary>
    public string? ConnectionString { get; set; }
}
