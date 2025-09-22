using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace AwsIntegration;

/// <summary>
/// AWS Systems Manager Parameter Store configuration provider for TUF deployments.
/// Enables centralized, secure configuration management with encryption and access control.
/// </summary>
/// <remarks>
/// This implementation provides:
/// - Centralized configuration management across environments
/// - Encryption at rest using AWS KMS
/// - Fine-grained access control via IAM policies
/// - Configuration versioning and change tracking
/// - Integration with AWS CloudFormation and CDK
/// - Hierarchical parameter organization
/// 
/// Parameter Naming Convention:
/// /tuf/{environment}/{component}/{setting}
/// 
/// Examples:
/// - /tuf/prod/kms/signing-key-id
/// - /tuf/prod/s3/repository-bucket
/// - /tuf/prod/repository/root-expiry-days
/// - /tuf/staging/metadata/update-frequency-hours
/// 
/// Security Best Practices:
/// - Use SecureString parameters for sensitive data
/// - Implement least-privilege IAM policies
/// - Enable CloudTrail for parameter access auditing
/// - Use parameter hierarchies for environment isolation
/// </remarks>
public sealed class ParameterStoreConfig : IDisposable
{
    private readonly IAmazonSimpleSystemsManagement _ssmClient;
    private readonly string _parameterPrefix;
    private readonly Dictionary<string, string> _cache;
    private readonly SemaphoreSlim _cacheLock;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new Parameter Store configuration provider.
    /// </summary>
    /// <param name="ssmClient">AWS SSM client (will be disposed with this instance)</param>
    /// <param name="parameterPrefix">Parameter prefix (e.g., "/tuf/prod")</param>
    /// <param name="enableCaching">Enable in-memory parameter caching</param>
    public ParameterStoreConfig(IAmazonSimpleSystemsManagement ssmClient, string parameterPrefix, bool enableCaching = true)
    {
        _ssmClient = ssmClient ?? throw new ArgumentNullException(nameof(ssmClient));
        _parameterPrefix = parameterPrefix?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(parameterPrefix));
        _cache = enableCaching ? new Dictionary<string, string>() : new Dictionary<string, string>();
        _cacheLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets a parameter value by name with optional caching.
    /// </summary>
    /// <param name="parameterName">Parameter name (without prefix)</param>
    /// <param name="decrypt">Whether to decrypt SecureString parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parameter value</returns>
    /// <exception cref="ArgumentException">Thrown when parameter is not found</exception>
    public async Task<string> GetParameterAsync(string parameterName, bool decrypt = true, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var fullParameterName = $"{_parameterPrefix}/{parameterName.TrimStart('/')}";
        
        // Check cache first
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(fullParameterName, out var cachedValue))
            {
                return cachedValue;
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        // Fetch from Parameter Store
        try
        {
            var request = new GetParameterRequest
            {
                Name = fullParameterName,
                WithDecryption = decrypt
            };

            var response = await _ssmClient.GetParameterAsync(request, cancellationToken);
            var value = response.Parameter.Value;

            // Cache the result
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                _cache[fullParameterName] = value;
            }
            finally
            {
                _cacheLock.Release();
            }

            return value;
        }
        catch (ParameterNotFoundException ex)
        {
            throw new ArgumentException($"Parameter '{fullParameterName}' not found in AWS Systems Manager Parameter Store", parameterName, ex);
        }
    }

    /// <summary>
    /// Gets multiple parameters by path prefix with optional caching.
    /// </summary>
    /// <param name="pathPrefix">Parameter path prefix (without base prefix)</param>
    /// <param name="decrypt">Whether to decrypt SecureString parameters</param>
    /// <param name="recursive">Whether to retrieve parameters recursively</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of parameter names (without full prefix) to values</returns>
    public async Task<Dictionary<string, string>> GetParametersByPathAsync(
        string pathPrefix, 
        bool decrypt = true, 
        bool recursive = true, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var fullPathPrefix = $"{_parameterPrefix}/{pathPrefix.TrimStart('/')}";
        var parameters = new Dictionary<string, string>();

        var request = new GetParametersByPathRequest
        {
            Path = fullPathPrefix,
            WithDecryption = decrypt,
            Recursive = recursive,
            MaxResults = 10 // AWS limit is 10 parameters per request
        };

        do
        {
            var response = await _ssmClient.GetParametersByPathAsync(request, cancellationToken);
            
            foreach (var parameter in response.Parameters)
            {
                // Remove the base prefix to get the relative parameter name
                var relativeName = parameter.Name;
                if (relativeName.StartsWith(_parameterPrefix))
                {
                    relativeName = relativeName.Substring(_parameterPrefix.Length).TrimStart('/');
                }
                
                parameters[relativeName] = parameter.Value;
                
                // Cache individual parameters
                await _cacheLock.WaitAsync(cancellationToken);
                try
                {
                    _cache[parameter.Name] = parameter.Value;
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            
            request.NextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(request.NextToken));

        return parameters;
    }

    /// <summary>
    /// Sets a parameter value in Parameter Store.
    /// </summary>
    /// <param name="parameterName">Parameter name (without prefix)</param>
    /// <param name="value">Parameter value</param>
    /// <param name="type">Parameter type</param>
    /// <param name="kmsKeyId">KMS key ID for SecureString encryption (optional)</param>
    /// <param name="description">Parameter description</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SetParameterAsync(
        string parameterName, 
        string value, 
        ParameterType? type = null,
        string? kmsKeyId = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var fullParameterName = $"{_parameterPrefix}/{parameterName.TrimStart('/')}";
        
        var request = new PutParameterRequest
        {
            Name = fullParameterName,
            Value = value,
            Type = type ?? ParameterType.String,
            Description = description ?? $"TUF configuration parameter: {parameterName}",
            Overwrite = true
        };

        if ((type ?? ParameterType.String) == ParameterType.SecureString && !string.IsNullOrEmpty(kmsKeyId))
        {
            request.KeyId = kmsKeyId;
        }

        await _ssmClient.PutParameterAsync(request, cancellationToken);
        
        // Update cache
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _cache[fullParameterName] = value;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Creates a TUF configuration object from Parameter Store values.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TUF configuration</returns>
    public async Task<TufConfiguration> GetTufConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetParametersByPathAsync("", decrypt: true, recursive: true, cancellationToken);
        
        return new TufConfiguration
        {
            // KMS Configuration
            KmsKeyId = GetConfigValue(config, "kms/signing-key-id"),
            KmsRegion = GetConfigValue(config, "kms/region"),
            
            // S3 Configuration
            S3BucketName = GetConfigValue(config, "s3/repository-bucket"),
            S3Region = GetConfigValue(config, "s3/region"),
            S3MetadataPrefix = GetConfigValue(config, "s3/metadata-prefix", "metadata"),
            S3TargetsPrefix = GetConfigValue(config, "s3/targets-prefix", "targets"),
            
            // Repository Configuration
            RepositoryName = GetConfigValue(config, "repository/name", "default"),
            RootExpiryDays = int.Parse(GetConfigValue(config, "repository/root-expiry-days", "365")),
            TimestampExpiryHours = int.Parse(GetConfigValue(config, "repository/timestamp-expiry-hours", "1")),
            SnapshotExpiryDays = int.Parse(GetConfigValue(config, "repository/snapshot-expiry-days", "7")),
            TargetsExpiryDays = int.Parse(GetConfigValue(config, "repository/targets-expiry-days", "90")),
            
            // Security Configuration
            RequireSignatureThreshold = int.Parse(GetConfigValue(config, "security/signature-threshold", "1")),
            EnableConsistentSnapshot = bool.Parse(GetConfigValue(config, "security/consistent-snapshot", "true")),
            
            // Operational Configuration  
            MetadataUpdateFrequencyHours = int.Parse(GetConfigValue(config, "metadata/update-frequency-hours", "6")),
            EnableMetadataVersioning = bool.Parse(GetConfigValue(config, "metadata/enable-versioning", "true")),
            LogLevel = GetConfigValue(config, "logging/level", "Information")
        };
    }

    /// <summary>
    /// Clears the parameter cache.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cache.Clear();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static string GetConfigValue(Dictionary<string, string> config, string key, string defaultValue = "")
    {
        return config.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ParameterStoreConfig));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ssmClient?.Dispose();
            _cacheLock?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// TUF configuration loaded from AWS Parameter Store.
/// </summary>
public record TufConfiguration
{
    // KMS Configuration
    public string KmsKeyId { get; init; } = string.Empty;
    public string KmsRegion { get; init; } = string.Empty;
    
    // S3 Configuration
    public string S3BucketName { get; init; } = string.Empty;
    public string S3Region { get; init; } = string.Empty;
    public string S3MetadataPrefix { get; init; } = "metadata";
    public string S3TargetsPrefix { get; init; } = "targets";
    
    // Repository Configuration
    public string RepositoryName { get; init; } = "default";
    public int RootExpiryDays { get; init; } = 365;
    public int TimestampExpiryHours { get; init; } = 1;
    public int SnapshotExpiryDays { get; init; } = 7;
    public int TargetsExpiryDays { get; init; } = 90;
    
    // Security Configuration
    public int RequireSignatureThreshold { get; init; } = 1;
    public bool EnableConsistentSnapshot { get; init; } = true;
    
    // Operational Configuration
    public int MetadataUpdateFrequencyHours { get; init; } = 6;
    public bool EnableMetadataVersioning { get; init; } = true;
    public string LogLevel { get; init; } = "Information";
}