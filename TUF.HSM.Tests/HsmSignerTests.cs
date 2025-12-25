using TUnit.Assertions;
using TUnit.Core;
using TUF.HSM;
using TUF.Models;

namespace TUF.HSM.Tests;

/// <summary>
/// Tests for HSM signer interfaces and base functionality.
/// Note: These tests focus on interface contracts and mock implementations,
/// as real HSM testing requires physical devices.
/// </summary>
public sealed class HsmSignerTests
{
    [Test]
    public async Task HsmProviderInfo_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var providerInfo = new HsmProviderInfo
        {
            Name = "Test HSM",
            Version = "1.0.0",
            Description = "Test HSM provider",
            SupportedKeyTypes = ["rsa", "ecdsa"],
            Metadata = new Dictionary<string, string>
            {
                ["TestKey"] = "TestValue"
            }
        };

        // Assert
        await Assert.That(providerInfo.Name).IsEqualTo("Test HSM");
        await Assert.That(providerInfo.Version).IsEqualTo("1.0.0");
        await Assert.That(providerInfo.Description).IsEqualTo("Test HSM provider");
        await Assert.That(providerInfo.SupportedKeyTypes).HasCount().EqualTo(2);
        await Assert.That(providerInfo.SupportedKeyTypes).Contains("rsa");
        await Assert.That(providerInfo.SupportedKeyTypes).Contains("ecdsa");
        await Assert.That(providerInfo.Metadata).IsNotNull();
        await Assert.That(providerInfo.Metadata!["TestKey"]).IsEqualTo("TestValue");
    }

    [Test]
    public async Task PinCredentials_Should_Store_Pin()
    {
        // Arrange & Act
        var credentials = new PinCredentials("123456");

        // Assert
        await Assert.That(credentials.Pin).IsEqualTo("123456");
    }

    [Test]
    public async Task CertificateCredentials_Should_Store_Certificate_Info()
    {
        // Arrange & Act
        var credentials = new CertificateCredentials("ABC123", "My");

        // Assert
        await Assert.That(credentials.CertificateThumbprint).IsEqualTo("ABC123");
        await Assert.That(credentials.StoreName).IsEqualTo("My");
    }

    [Test]
    public async Task CertificateCredentials_Should_Default_StoreName_To_Null()
    {
        // Arrange & Act
        var credentials = new CertificateCredentials("ABC123");

        // Assert
        await Assert.That(credentials.CertificateThumbprint).IsEqualTo("ABC123");
        await Assert.That(credentials.StoreName).IsNull();
    }

    [Test]
    public async Task NoCredentials_Should_Be_Singleton()
    {
        // Arrange & Act
        var credentials1 = NoCredentials.Instance;
        var credentials2 = NoCredentials.Instance;

        // Assert
        await Assert.That(ReferenceEquals(credentials1, credentials2)).IsTrue();
    }

    [Test]
    public async Task YubiKey_PivSlot_Should_Have_Expected_Values()
    {
        // Assert - Verify PIV slot values match standard
        await Assert.That((int)PivSlot.Authentication).IsEqualTo(0x9A);
        await Assert.That((int)PivSlot.DigitalSignature).IsEqualTo(0x9C);
        await Assert.That((int)PivSlot.KeyManagement).IsEqualTo(0x9D);
        await Assert.That((int)PivSlot.CardAuthentication).IsEqualTo(0x9E);
    }
}

/// <summary>
/// Mock HSM signer for testing interface implementations.
/// </summary>
internal sealed class MockHsmSigner : IHsmSigner, IDisposable
{
    private bool _isAuthenticated;

    public HsmProviderInfo ProviderInfo { get; } = new()
    {
        Name = "Mock HSM",
        Version = "1.0.0-test",
        Description = "Mock HSM signer for testing",
        SupportedKeyTypes = new[] { "rsa", "ecdsa", "ed25519" }
    };

    public bool RequiresAuthentication => true;

    public TUF.Models.Key Key { get; } = new()
    {
        KeyType = "ed25519",
        Scheme = "ed25519",
        KeyVal = new TUF.Models.KeyValue
        {
            Public = "abcdef123456789abcdef123456789abcdef123456789abcdef123456789abcdef12"
        }
    };

    public Task<bool> AuthenticateAsync(HsmCredentials credentials)
    {
        _isAuthenticated = credentials is PinCredentials { Pin: "123456" };
        return Task.FromResult(_isAuthenticated);
    }

    public bool IsAuthenticated() => _isAuthenticated;

    public TUF.Models.SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        if (!_isAuthenticated)
            throw new InvalidOperationException("Not authenticated");

        return new TUF.Models.SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = "mock_signature_" + Convert.ToHexString(data).ToLowerInvariant()
        };
    }

    public Task<TUF.Models.SignatureObject> SignBytesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SignBytes(data.Span));
    }

    public Task LogoutAsync()
    {
        _isAuthenticated = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isAuthenticated = false;
    }
}

/// <summary>
/// Tests for mock HSM signer implementation.
/// </summary>
public sealed class MockHsmSignerTests
{
    [Test]
    public async Task MockHsmSigner_Should_Require_Authentication()
    {
        // Arrange
        using var signer = new MockHsmSigner();

        // Assert
        await Assert.That(signer.RequiresAuthentication).IsTrue();
        await Assert.That(signer.IsAuthenticated()).IsFalse();
    }

    [Test]
    public async Task MockHsmSigner_Should_Authenticate_With_Correct_Pin()
    {
        // Arrange
        using var signer = new MockHsmSigner();
        var credentials = new PinCredentials("123456");

        // Act
        var result = await signer.AuthenticateAsync(credentials);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(signer.IsAuthenticated()).IsTrue();
    }

    [Test]
    public async Task MockHsmSigner_Should_Reject_Wrong_Pin()
    {
        // Arrange
        using var signer = new MockHsmSigner();
        var credentials = new PinCredentials("wrong");

        // Act
        var result = await signer.AuthenticateAsync(credentials);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(signer.IsAuthenticated()).IsFalse();
    }

    [Test]
    public async Task MockHsmSigner_Should_Sign_When_Authenticated()
    {
        // Arrange
        using var signer = new MockHsmSigner();
        await signer.AuthenticateAsync(new PinCredentials("123456"));
        var data = "test data"u8;

        // Act
        var signature = signer.SignBytes(data);

        // Assert
        await Assert.That(signature).IsNotNull();
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).StartsWith("mock_signature_");
    }

    [Test]
    public async Task MockHsmSigner_Should_Throw_When_Not_Authenticated()
    {
        // Arrange
        using var signer = new MockHsmSigner();
        var data = "test data"u8.ToArray();

        // Act & Assert
        await Assert.That(() => signer.SignBytes(data))
            .Throws<InvalidOperationException>()
            .WithMessage("Not authenticated");
    }

    [Test]
    public async Task MockHsmSigner_Should_Logout()
    {
        // Arrange
        using var signer = new MockHsmSigner();
        await signer.AuthenticateAsync(new PinCredentials("123456"));
        await Assert.That(signer.IsAuthenticated()).IsTrue();

        // Act
        await signer.LogoutAsync();

        // Assert
        await Assert.That(signer.IsAuthenticated()).IsFalse();
    }

    [Test]
    public async Task MockHsmSigner_Should_Sign_Async()
    {
        // Arrange
        using var signer = new MockHsmSigner();
        await signer.AuthenticateAsync(new PinCredentials("123456"));
        var data = "test data"u8.ToArray();

        // Act
        var signature = await signer.SignBytesAsync(data);

        // Assert
        await Assert.That(signature).IsNotNull();
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).StartsWith("mock_signature_");
    }
}