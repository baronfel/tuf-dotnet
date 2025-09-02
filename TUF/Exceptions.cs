using System.Net;

namespace TUF.Exceptions;

/// <summary>
/// Base exception for all TUF-specific errors
/// </summary>
public abstract class TufException : Exception
{
    protected TufException(string message) : base(message) { }
    protected TufException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when TUF metadata cannot be parsed or deserialized
/// </summary>
public class MetadataDeserializationException : TufException
{
    public string? MetadataType { get; }
    public string? RawMetadata { get; }

    public MetadataDeserializationException(string message, string? metadataType = null, string? rawMetadata = null) 
        : base(message)
    {
        MetadataType = metadataType;
        RawMetadata = rawMetadata;
    }

    public MetadataDeserializationException(string message, Exception innerException, string? metadataType = null, string? rawMetadata = null)
        : base(message, innerException)
    {
        MetadataType = metadataType;
        RawMetadata = rawMetadata;
    }
}

/// <summary>
/// Exception thrown when TUF metadata validation fails
/// </summary>
public class MetadataValidationException : TufException
{
    public string? MetadataType { get; }
    public string? ValidationRule { get; }

    public MetadataValidationException(string message, string? metadataType = null, string? validationRule = null)
        : base(message)
    {
        MetadataType = metadataType;
        ValidationRule = validationRule;
    }
}

/// <summary>
/// Exception thrown when cryptographic signature verification fails
/// </summary>
public class SignatureVerificationException : TufException
{
    public string? KeyId { get; }
    public string? MetadataType { get; }

    public SignatureVerificationException(string message, string? keyId = null, string? metadataType = null)
        : base(message)
    {
        KeyId = keyId;
        MetadataType = metadataType;
    }
}

/// <summary>
/// Exception thrown when insufficient valid signatures are present for a threshold requirement
/// </summary>
public class InsufficientSignaturesException : TufException
{
    public int RequiredSignatures { get; }
    public int ValidSignatures { get; }
    public string? RoleName { get; }

    public InsufficientSignaturesException(int requiredSignatures, int validSignatures, string? roleName = null)
        : base($"Insufficient valid signatures: {validSignatures} of {requiredSignatures} required" + (roleName != null ? $" for role {roleName}" : ""))
    {
        RequiredSignatures = requiredSignatures;
        ValidSignatures = validSignatures;
        RoleName = roleName;
    }
}

/// <summary>
/// Exception thrown when TUF metadata has expired
/// </summary>
public class ExpiredMetadataException : TufException
{
    public string? MetadataType { get; }
    public DateTime? ExpiryTime { get; }

    public ExpiredMetadataException(string message, string? metadataType = null, DateTime? expiryTime = null)
        : base(message)
    {
        MetadataType = metadataType;
        ExpiryTime = expiryTime;
    }
}

/// <summary>
/// Exception thrown when metadata version numbers indicate a rollback attack
/// </summary>
public class RollbackAttackException : TufException
{
    public string? MetadataType { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    public RollbackAttackException(string metadataType, int expectedVersion, int actualVersion)
        : base($"Rollback attack detected: {metadataType} version {actualVersion} is older than expected version {expectedVersion}")
    {
        MetadataType = metadataType;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}

/// <summary>
/// Exception thrown when a TUF target file cannot be found or is not available
/// </summary>
public class TargetNotFoundException : TufException
{
    public string? TargetPath { get; }

    public TargetNotFoundException(string message, string? targetPath = null) : base(message)
    {
        TargetPath = targetPath;
    }
}

/// <summary>
/// Exception thrown when target file integrity verification fails
/// </summary>
public class TargetIntegrityException : TufException
{
    public string? TargetPath { get; }
    public string? ExpectedHash { get; }
    public string? ActualHash { get; }

    public TargetIntegrityException(string message, string? targetPath = null, string? expectedHash = null, string? actualHash = null)
        : base(message)
    {
        TargetPath = targetPath;
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }
}

/// <summary>
/// Exception thrown when TUF delegation resolution fails
/// </summary>
public class DelegationException : TufException
{
    public string? RoleName { get; }
    public int DelegationDepth { get; }

    public DelegationException(string message, string? roleName = null, int delegationDepth = 0)
        : base(message)
    {
        RoleName = roleName;
        DelegationDepth = delegationDepth;
    }
}

/// <summary>
/// Exception thrown when maximum delegation depth is exceeded
/// </summary>
public class MaxDelegationDepthExceededException : DelegationException
{
    public int MaxDepth { get; }

    public MaxDelegationDepthExceededException(int maxDepth, int currentDepth)
        : base($"Maximum delegation depth {maxDepth} exceeded (current depth: {currentDepth})", null, currentDepth)
    {
        MaxDepth = maxDepth;
    }
}

/// <summary>
/// Exception thrown when TUF repository network operations fail
/// </summary>
public class RepositoryNetworkException : TufException
{
    public Uri? RepositoryUri { get; }
    public HttpStatusCode? StatusCode { get; }

    public RepositoryNetworkException(string message, Uri? repositoryUri = null, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException!)
    {
        RepositoryUri = repositoryUri;
        StatusCode = statusCode;
    }
}

/// <summary>
/// Exception thrown when TUF configuration is invalid
/// </summary>
public class TufConfigurationException : TufException
{
    public string? ConfigurationProperty { get; }

    public TufConfigurationException(string message, string? configurationProperty = null)
        : base(message)
    {
        ConfigurationProperty = configurationProperty;
    }
}

/// <summary>
/// Exception thrown when file size limits are exceeded
/// </summary>
public class FileSizeException : TufException
{
    public string? FilePath { get; }
    public long ActualSize { get; }
    public long MaxAllowedSize { get; }

    public FileSizeException(string filePath, long actualSize, long maxAllowedSize)
        : base($"File {filePath} size {actualSize} bytes exceeds maximum allowed size {maxAllowedSize} bytes")
    {
        FilePath = filePath;
        ActualSize = actualSize;
        MaxAllowedSize = maxAllowedSize;
    }
}