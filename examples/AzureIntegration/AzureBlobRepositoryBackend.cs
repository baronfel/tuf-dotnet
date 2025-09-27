using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace AzureIntegration;

/// <summary>
/// Azure Blob Storage backend for TUF repository storage.
/// Provides enterprise-grade storage with global distribution capabilities through Azure CDN integration.
/// </summary>
/// <remarks>
/// Azure Blob Storage offers several advantages for TUF repositories:
/// - 99.999999999% (11 9's) durability with automatic replication
/// - Global distribution via Azure CDN and edge locations
/// - Server-side encryption at rest with customer-managed keys
/// - Fine-grained access control using Azure RBAC and SAS tokens
/// - Lifecycle management for cost optimization
/// - Object versioning and soft delete for data protection
/// - Integration with Azure Monitor for operational visibility
/// 
/// Performance Characteristics:
/// - Hot tier: Optimized for frequent access (metadata files)
/// - Cool tier: Cost-optimized for infrequent access (large target files)
/// - Archive tier: Lowest cost for long-term retention
/// 
/// Security Features:
/// - Private endpoints for network isolation
/// - Azure AD integration for identity-based access
/// - Encryption in transit (HTTPS only)
/// - Immutable blob storage with legal hold capabilities
/// - Advanced threat protection and security monitoring
/// </remarks>
public sealed class AzureBlobRepositoryBackend
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobRepositoryBackend>? _logger;

    /// <summary>
    /// Creates an Azure Blob Storage repository backend.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name</param>
    /// <param name="containerName">The container name for TUF repository data</param>
    /// <param name="credential">Azure credential for authentication (optional, uses DefaultAzureCredential if null)</param>
    /// <param name="logger">Optional logger for operational visibility</param>
    public AzureBlobRepositoryBackend(
        string accountName,
        string containerName,
        TokenCredential? credential = null,
        ILogger<AzureBlobRepositoryBackend>? logger = null)
    {
        credential ??= new DefaultAzureCredential();
        var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        
        _blobServiceClient = new BlobServiceClient(serviceUri, credential);
        _containerName = containerName;
        _logger = logger;
    }

    /// <summary>
    /// Uploads TUF metadata to Azure Blob Storage with optimized settings.
    /// </summary>
    /// <param name="path">The relative path within the repository (e.g., "metadata/root.json")</param>
    /// <param name="content">The file content as bytes</param>
    /// <param name="contentType">Optional content type (defaults based on file extension)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The full URI of the uploaded blob</returns>
    public async Task<Uri> UploadFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Uploading file to Azure Blob Storage: {Path}", path);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(path);
        
        // Set appropriate content type based on file extension
        contentType ??= GetContentTypeForPath(path);
        
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                CacheControl = GetCacheControlForPath(path)
            },
            Metadata = new Dictionary<string, string>
            {
                ["tuf-repository"] = "true",
                ["upload-time"] = DateTimeOffset.UtcNow.ToString("O"),
                ["file-type"] = GetFileTypeForPath(path)
            }
        };

        await blobClient.UploadAsync(new BinaryData(content), options, cancellationToken);
        
        _logger?.LogInformation("Successfully uploaded file: {Path} ({Size} bytes)", path, content.Length);
        
        return blobClient.Uri;
    }

    /// <summary>
    /// Downloads a file from Azure Blob Storage.
    /// </summary>
    /// <param name="path">The relative path within the repository</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The file content as bytes, or null if the file doesn't exist</returns>
    public async Task<byte[]?> DownloadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Downloading file from Azure Blob Storage: {Path}", path);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(path);

        try
        {
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var content = response.Value.Content.ToArray();
            
            _logger?.LogDebug("Successfully downloaded file: {Path} ({Size} bytes)", path, content.Length);
            return content;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger?.LogWarning("File not found in Azure Blob Storage: {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Checks if a file exists in Azure Blob Storage.
    /// </summary>
    /// <param name="path">The relative path within the repository</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if the file exists, false otherwise</returns>
    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(path);

        try
        {
            await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    /// <summary>
    /// Lists all files in the repository with optional prefix filtering.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter files (e.g., "metadata/" for metadata files only)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>An enumerable of file paths</returns>
    public async IAsyncEnumerable<string> ListFilesAsync(
        string? prefix = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        
        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            yield return blobItem.Name;
        }
    }

    /// <summary>
    /// Deletes a file from Azure Blob Storage.
    /// </summary>
    /// <param name="path">The relative path within the repository</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if the file was deleted, false if it didn't exist</returns>
    public async Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Deleting file from Azure Blob Storage: {Path}", path);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(path);

        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        
        if (response.Value)
        {
            _logger?.LogInformation("Successfully deleted file: {Path}", path);
        }
        else
        {
            _logger?.LogWarning("File not found for deletion: {Path}", path);
        }

        return response.Value;
    }

    /// <summary>
    /// Gets the public URL for a blob, suitable for TUF client access.
    /// </summary>
    /// <param name="path">The relative path within the repository</param>
    /// <returns>The public URL for the blob</returns>
    public Uri GetPublicUrl(string path)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(path);
        return blobClient.Uri;
    }

    /// <summary>
    /// Determines the appropriate content type based on file extension.
    /// </summary>
    private static string GetContentTypeForPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".exe" => "application/octet-stream",
            ".msi" => "application/x-msi",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Determines appropriate cache control headers based on file type.
    /// </summary>
    private static string GetCacheControlForPath(string path)
    {
        // Metadata files should have shorter cache times for freshness
        if (path.StartsWith("metadata/", StringComparison.OrdinalIgnoreCase))
        {
            return "public, max-age=300"; // 5 minutes for metadata
        }
        
        // Target files can be cached longer since they're content-addressed
        return "public, max-age=3600"; // 1 hour for target files
    }

    /// <summary>
    /// Determines the file type for metadata tagging.
    /// </summary>
    private static string GetFileTypeForPath(string path)
    {
        if (path.StartsWith("metadata/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            return fileName switch
            {
                "root" => "tuf-root-metadata",
                "timestamp" => "tuf-timestamp-metadata",
                "snapshot" => "tuf-snapshot-metadata",
                "targets" => "tuf-targets-metadata",
                _ when fileName.EndsWith(".delegated") => "tuf-delegated-targets-metadata",
                _ => "tuf-metadata"
            };
        }
        
        return path.StartsWith("targets/", StringComparison.OrdinalIgnoreCase) 
            ? "tuf-target-file" 
            : "tuf-repository-file";
    }
}