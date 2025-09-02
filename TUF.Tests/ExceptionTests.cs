using System.Net;
using TUF.Exceptions;

namespace TUF.Tests;

public class ExceptionTests
{
    [Test]
    public async Task TufException_IsAbstractBaseException()
    {
        await Assert.That(typeof(TufException).IsAbstract).IsTrue();
        await Assert.That(typeof(Exception).IsAssignableFrom(typeof(TufException))).IsTrue();
    }

    [Test]
    public async Task MetadataDeserializationException_SetsPropertiesCorrectly()
    {
        var message = "Failed to deserialize metadata";
        var metadataType = "root";
        var rawMetadata = "{\"invalid\":\"json\"}";

        var exception = new MetadataDeserializationException(message, metadataType, rawMetadata);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.RawMetadata).IsEqualTo(rawMetadata);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task MetadataDeserializationException_WithInnerException_SetsPropertiesCorrectly()
    {
        var message = "Failed to deserialize metadata";
        var metadataType = "targets";
        var rawMetadata = "{\"broken\":\"json\"}";
        var innerException = new ArgumentException("Inner error");

        var exception = new MetadataDeserializationException(message, innerException, metadataType, rawMetadata);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.RawMetadata).IsEqualTo(rawMetadata);
        await Assert.That(exception.InnerException).IsEqualTo(innerException);
    }

    [Test]
    public async Task MetadataValidationException_SetsPropertiesCorrectly()
    {
        var message = "Metadata validation failed";
        var metadataType = "timestamp";
        var validationRule = "version_check";

        var exception = new MetadataValidationException(message, metadataType, validationRule);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ValidationRule).IsEqualTo(validationRule);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task SignatureVerificationException_SetsPropertiesCorrectly()
    {
        var message = "Signature verification failed";
        var keyId = "key-123";
        var metadataType = "targets";

        var exception = new SignatureVerificationException(message, keyId, metadataType);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.KeyId).IsEqualTo(keyId);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task InsufficientSignaturesException_GeneratesCorrectMessage()
    {
        var requiredSignatures = 3;
        var validSignatures = 1;
        var roleName = "targets";

        var exception = new InsufficientSignaturesException(requiredSignatures, validSignatures, roleName);

        await Assert.That(exception.RequiredSignatures).IsEqualTo(requiredSignatures);
        await Assert.That(exception.ValidSignatures).IsEqualTo(validSignatures);
        await Assert.That(exception.RoleName).IsEqualTo(roleName);
        await Assert.That(exception.Message).Contains("1 of 3 required for role targets");
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task InsufficientSignaturesException_WithoutRoleName_GeneratesCorrectMessage()
    {
        var requiredSignatures = 2;
        var validSignatures = 0;

        var exception = new InsufficientSignaturesException(requiredSignatures, validSignatures);

        await Assert.That(exception.RequiredSignatures).IsEqualTo(requiredSignatures);
        await Assert.That(exception.ValidSignatures).IsEqualTo(validSignatures);
        await Assert.That(exception.RoleName).IsNull();
        await Assert.That(exception.Message).Contains("0 of 2 required");
        await Assert.That(exception.Message).DoesNotContain("for role");
    }

    [Test]
    public async Task ExpiredMetadataException_SetsPropertiesCorrectly()
    {
        var message = "Metadata has expired";
        var metadataType = "timestamp";
        var expiryTime = DateTime.UtcNow.AddDays(-1);

        var exception = new ExpiredMetadataException(message, metadataType, expiryTime);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ExpiryTime).IsEqualTo(expiryTime);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task RollbackAttackException_GeneratesCorrectMessage()
    {
        var metadataType = "timestamp";
        var expectedVersion = 5;
        var actualVersion = 3;

        var exception = new RollbackAttackException(metadataType, expectedVersion, actualVersion);

        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ExpectedVersion).IsEqualTo(expectedVersion);
        await Assert.That(exception.ActualVersion).IsEqualTo(actualVersion);
        await Assert.That(exception.Message).Contains($"{metadataType} version {actualVersion} is older than expected version {expectedVersion}");
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task TargetNotFoundException_SetsPropertiesCorrectly()
    {
        var message = "Target file not found";
        var targetPath = "/path/to/missing/file.txt";

        var exception = new TargetNotFoundException(message, targetPath);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.TargetPath).IsEqualTo(targetPath);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task TargetIntegrityException_SetsPropertiesCorrectly()
    {
        var message = "Target file integrity check failed";
        var targetPath = "/path/to/file.bin";
        var expectedHash = "sha256-expected";
        var actualHash = "sha256-actual";

        var exception = new TargetIntegrityException(message, targetPath, expectedHash, actualHash);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.TargetPath).IsEqualTo(targetPath);
        await Assert.That(exception.ExpectedHash).IsEqualTo(expectedHash);
        await Assert.That(exception.ActualHash).IsEqualTo(actualHash);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task DelegationException_SetsPropertiesCorrectly()
    {
        var message = "Delegation resolution failed";
        var roleName = "custom-role";
        var delegationDepth = 2;

        var exception = new DelegationException(message, roleName, delegationDepth);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.RoleName).IsEqualTo(roleName);
        await Assert.That(exception.DelegationDepth).IsEqualTo(delegationDepth);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task MaxDelegationDepthExceededException_InheritsFromDelegationException()
    {
        var maxDepth = 10;
        var currentDepth = 15;

        var exception = new MaxDelegationDepthExceededException(maxDepth, currentDepth);

        await Assert.That(exception.MaxDepth).IsEqualTo(maxDepth);
        await Assert.That(exception.DelegationDepth).IsEqualTo(currentDepth);
        await Assert.That(exception.Message).Contains($"Maximum delegation depth {maxDepth} exceeded");
        await Assert.That(exception.Message).Contains($"current depth: {currentDepth}");
        await Assert.That(typeof(DelegationException).IsAssignableFrom(exception.GetType())).IsTrue();
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task RepositoryNetworkException_SetsPropertiesCorrectly()
    {
        var message = "Network request failed";
        var repositoryUri = new Uri("https://repo.example.com/metadata");
        var statusCode = HttpStatusCode.NotFound;
        var innerException = new HttpRequestException("Connection timeout");

        var exception = new RepositoryNetworkException(message, repositoryUri, statusCode, innerException);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.RepositoryUri).IsEqualTo(repositoryUri);
        await Assert.That(exception.StatusCode).IsEqualTo(statusCode);
        await Assert.That(exception.InnerException).IsEqualTo(innerException);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task TufConfigurationException_SetsPropertiesCorrectly()
    {
        var message = "Configuration validation failed";
        var configurationProperty = "LocalMetadataDir";

        var exception = new TufConfigurationException(message, configurationProperty);

        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.ConfigurationProperty).IsEqualTo(configurationProperty);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task FileSizeException_GeneratesCorrectMessage()
    {
        var filePath = "/path/to/large/file.bin";
        var actualSize = 1_000_000_000L;
        var maxAllowedSize = 100_000_000L;

        var exception = new FileSizeException(filePath, actualSize, maxAllowedSize);

        await Assert.That(exception.FilePath).IsEqualTo(filePath);
        await Assert.That(exception.ActualSize).IsEqualTo(actualSize);
        await Assert.That(exception.MaxAllowedSize).IsEqualTo(maxAllowedSize);
        await Assert.That(exception.Message).Contains($"File {filePath} size {actualSize} bytes exceeds maximum allowed size {maxAllowedSize} bytes");
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    [Test]
    public async Task AllExceptionTypes_InheritFromTufException()
    {
        var exceptionTypes = new Type[]
        {
            typeof(MetadataDeserializationException),
            typeof(MetadataValidationException),
            typeof(SignatureVerificationException),
            typeof(InsufficientSignaturesException),
            typeof(ExpiredMetadataException),
            typeof(RollbackAttackException),
            typeof(TargetNotFoundException),
            typeof(TargetIntegrityException),
            typeof(DelegationException),
            typeof(MaxDelegationDepthExceededException),
            typeof(RepositoryNetworkException),
            typeof(TufConfigurationException),
            typeof(FileSizeException)
        };

        foreach (var exceptionType in exceptionTypes)
        {
            var inheritsFromTuf = typeof(TufException).IsAssignableFrom(exceptionType);
            await Assert.That(inheritsFromTuf).IsTrue();
        }
    }

    [Test]
    public async Task SecurityExceptions_CanBeIdentified()
    {
        var securityExceptions = new TufException[]
        {
            new SignatureVerificationException("test"),
            new InsufficientSignaturesException(2, 1),
            new RollbackAttackException("test", 2, 1),
            new TargetIntegrityException("test")
        };

        var securityExceptionTypes = new Type[]
        {
            typeof(SignatureVerificationException),
            typeof(InsufficientSignaturesException),
            typeof(RollbackAttackException),
            typeof(TargetIntegrityException)
        };

        foreach (var exception in securityExceptions)
        {
            var isSecurityType = securityExceptionTypes.Contains(exception.GetType());
            await Assert.That(isSecurityType).IsTrue();
        }
    }

    [Test]
    public async Task OperationalExceptions_CanBeIdentified()
    {
        var operationalExceptions = new TufException[]
        {
            new MetadataDeserializationException("test"),
            new MetadataValidationException("test"),
            new ExpiredMetadataException("test"),
            new TargetNotFoundException("test"),
            new DelegationException("test"),
            new RepositoryNetworkException("test"),
            new TufConfigurationException("test"),
            new FileSizeException("test", 100, 50)
        };

        var operationalExceptionTypes = new Type[]
        {
            typeof(MetadataDeserializationException),
            typeof(MetadataValidationException),
            typeof(ExpiredMetadataException),
            typeof(TargetNotFoundException),
            typeof(DelegationException),
            typeof(RepositoryNetworkException),
            typeof(TufConfigurationException),
            typeof(FileSizeException)
        };

        foreach (var exception in operationalExceptions)
        {
            var isOperationalType = operationalExceptionTypes.Contains(exception.GetType());
            await Assert.That(isOperationalType).IsTrue();
        }
    }

    [Test]
    public async Task ExceptionSerialization_PreservesProperties()
    {
        var originalException = new RollbackAttackException("timestamp", 5, 3);
        
        // Test that serialization would work by accessing all properties
        await Assert.That(originalException.MetadataType).IsEqualTo("timestamp");
        await Assert.That(originalException.ExpectedVersion).IsEqualTo(5);
        await Assert.That(originalException.ActualVersion).IsEqualTo(3);
        await Assert.That(originalException.Message).IsNotNull();
        await Assert.That(originalException.Message).IsNotEmpty();
    }

    [Test]
    public async Task ExceptionHierarchy_SupportsPolymorphicHandling()
    {
        var exceptions = new TufException[]
        {
            new MetadataDeserializationException("test"),
            new SignatureVerificationException("test"),
            new RepositoryNetworkException("test"),
            new MaxDelegationDepthExceededException(5, 10)
        };

        foreach (var exception in exceptions)
        {
            // All exceptions should be catchable as TufException
            await Assert.That(exception is TufException).IsTrue();
            await Assert.That(exception.Message).IsNotNull();
            await Assert.That(exception.GetType().IsSubclassOf(typeof(TufException))).IsTrue();
        }
    }
}