using System.Security.Cryptography;
using TUF.Models;

namespace TUF.Hsm;

/// <summary>
/// Interface for hardware security module (HSM) signers that extends the basic ISigner interface
/// with HSM-specific capabilities like token management and session handling.
/// </summary>
/// <remarks>
/// HSM signers provide enhanced security by storing private keys in tamper-resistant hardware,
/// ensuring keys never exist in software memory. This interface provides the foundation for
/// integrating TUF with various HSM devices through standards like PKCS#11.
/// </remarks>
public interface IHsmSigner : ISigner, IDisposable
{
    /// <summary>
    /// The label or identifier of the key stored in the HSM token.
    /// This is used to locate the specific key within the HSM device.
    /// </summary>
    string KeyLabel { get; }

    /// <summary>
    /// The serial number or identifier of the HSM token containing the key.
    /// Used to identify the specific HSM token when multiple tokens are available.
    /// </summary>
    string TokenSerial { get; }

    /// <summary>
    /// Tests if the HSM connection and key access are working correctly.
    /// </summary>
    /// <returns>True if the HSM is accessible and the key can be used for signing</returns>
    bool TestConnection();
}

/// <summary>
/// Mock HSM signer implementation for demonstration and testing purposes.
/// This shows the architecture and integration patterns for HSM signers without
/// requiring actual HSM hardware. Production implementations would use PKCS#11
/// or vendor-specific APIs to communicate with real HSM devices.
/// </summary>
/// <remarks>
/// This mock implementation demonstrates:
/// - How HSM signers integrate with the TUF ISigner interface
/// - The conceptual flow of HSM operations (token selection, authentication, signing)
/// - Error handling patterns for HSM-specific failures
/// - Resource management and disposal patterns
/// 
/// For production use, replace this with actual HSM implementations using:
/// - Pkcs11Interop library for PKCS#11 standard HSMs
/// - Vendor-specific SDKs (SafeNet Luna, Thales, etc.)
/// - Cloud HSM APIs (AWS CloudHSM, Azure Dedicated HSM)
/// - Smart card APIs for PIV cards (YubiKey, CAC cards)
/// </remarks>
public sealed class MockHsmSigner : IHsmSigner
{
    private readonly string _keyLabel;
    private readonly string _tokenSerial;
    private readonly ISigner _underlyingSigner;
    private bool _disposed;

    /// <summary>
    /// The TUF Key information for this HSM signer.
    /// In a real implementation, this would be retrieved from the HSM device.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// The label of the key stored in the HSM token.
    /// </summary>
    public string KeyLabel => _keyLabel;

    /// <summary>
    /// The serial number of the HSM token.
    /// </summary>
    public string TokenSerial => _tokenSerial;

    /// <summary>
    /// Creates a mock HSM signer that demonstrates the HSM integration patterns.
    /// </summary>
    /// <param name="tokenSerial">Mock token serial number</param>
    /// <param name="keyLabel">Mock key label</param>
    /// <param name="algorithm">Signature algorithm to simulate</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    public MockHsmSigner(string tokenSerial, string keyLabel, HsmAlgorithm algorithm)
    {
        if (string.IsNullOrWhiteSpace(tokenSerial))
            throw new ArgumentException("Token serial cannot be empty", nameof(tokenSerial));
        if (string.IsNullOrWhiteSpace(keyLabel))
            throw new ArgumentException("Key label cannot be empty", nameof(keyLabel));

        _tokenSerial = tokenSerial;
        _keyLabel = keyLabel;

        // In a real implementation, this would connect to the HSM and retrieve the public key
        // For demonstration, we use software signers to show the integration pattern
        _underlyingSigner = algorithm switch
        {
            HsmAlgorithm.RsaPssSha256 => RsaSigner.Generate(3072),
            HsmAlgorithm.EcdsaSha2NistP256 => EcdsaSigner.Generate(),
            HsmAlgorithm.Ed25519 => Ed25519Signer.Generate(),
            _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}", nameof(algorithm))
        };

        Key = _underlyingSigner.Key;
        
        // Log that this is a mock for demonstration
        Console.WriteLine($"🔐 Mock HSM Signer initialized:");
        Console.WriteLine($"   Token: {_tokenSerial}");
        Console.WriteLine($"   Key: {_keyLabel}");
        Console.WriteLine($"   Algorithm: {algorithm}");
        Console.WriteLine($"   Key ID: {Key.GetKeyId()}");
        Console.WriteLine($"   📝 Note: This is a mock implementation for demonstration.");
        Console.WriteLine($"   📝 Production use requires actual HSM integration via PKCS#11 or vendor APIs.");
    }

    /// <summary>
    /// Supported HSM signature algorithms for the mock implementation.
    /// </summary>
    public enum HsmAlgorithm
    {
        /// <summary>RSA-PSS with SHA-256</summary>
        RsaPssSha256,
        /// <summary>ECDSA with SHA-256 on NIST P-256</summary>
        EcdsaSha2NistP256,
        /// <summary>Ed25519 signatures</summary>
        Ed25519
    }

    /// <summary>
    /// Signs data using the mock HSM implementation.
    /// In a real implementation, this would send the data to the HSM for signing.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object</returns>
    /// <exception cref="ObjectDisposedException">Thrown when signer has been disposed</exception>
    /// <exception cref="HsmException">Thrown when HSM operation fails</exception>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockHsmSigner));

        try
        {
            // In a real implementation, this would:
            // 1. Authenticate with the HSM (PIN/password)
            // 2. Send the data to the HSM for signing
            // 3. Return the hardware-generated signature
            // 4. Handle HSM-specific errors (device not found, authentication failure, etc.)
            
            Console.WriteLine($"🔐 Mock HSM signing operation:");
            Console.WriteLine($"   Token: {_tokenSerial}");
            Console.WriteLine($"   Key: {_keyLabel}");
            Console.WriteLine($"   Data length: {data.Length} bytes");
            Console.WriteLine($"   📝 Real HSM would sign this in hardware");

            return _underlyingSigner.SignBytes(data);
        }
        catch (Exception ex)
        {
            throw new HsmException($"Mock HSM signing failed for key '{_keyLabel}' on token '{_tokenSerial}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tests the mock HSM connection.
    /// In a real implementation, this would verify HSM connectivity and key accessibility.
    /// </summary>
    /// <returns>True if connection test succeeds</returns>
    public bool TestConnection()
    {
        if (_disposed)
            return false;

        try
        {
            // Mock connection test - in reality this would:
            // 1. Connect to HSM device
            // 2. Authenticate with PIN/password
            // 3. Verify key exists and is accessible
            // 4. Test basic signing operation
            
            Console.WriteLine($"🔍 Testing mock HSM connection:");
            Console.WriteLine($"   Token: {_tokenSerial}");
            Console.WriteLine($"   Key: {_keyLabel}");
            
            var testData = "HSM connection test"u8;
            var signature = SignBytes(testData);
            var success = !string.IsNullOrEmpty(signature.Sig);
            
            Console.WriteLine($"   Result: {(success ? "✅ Connected" : "❌ Failed")}");
            return success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the mock HSM signer and releases resources.
    /// In a real implementation, this would close HSM sessions and connections.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Console.WriteLine($"🔐 Disposing mock HSM signer:");
        Console.WriteLine($"   Token: {_tokenSerial}");
        Console.WriteLine($"   Key: {_keyLabel}");
        Console.WriteLine($"   📝 Real HSM would close sessions and logout");

        if (_underlyingSigner is IDisposable disposable)
            disposable.Dispose();

        _disposed = true;
    }
}

/// <summary>
/// Exception thrown when HSM operations fail.
/// Provides HSM-specific error context for debugging and incident response.
/// </summary>
public class HsmException : Exception
{
    /// <summary>
    /// Creates a new HSM exception.
    /// </summary>
    /// <param name="message">Error message</param>
    public HsmException(string message) : base(message) { }

    /// <summary>
    /// Creates a new HSM exception with an inner exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Underlying exception</param>
    public HsmException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Factory for creating HSM signers with different backends.
/// Provides a clean abstraction for switching between mock, PKCS#11, and vendor-specific implementations.
/// </summary>
/// <remarks>
/// This factory pattern allows:
/// - Easy testing with mock implementations
/// - Production deployment with real HSM devices
/// - Switching between different HSM vendors/technologies
/// - Centralized configuration of HSM parameters
/// </remarks>
public static class HsmSignerFactory
{
    /// <summary>
    /// Creates a mock HSM signer for demonstration and testing.
    /// </summary>
    /// <param name="tokenSerial">Token serial number</param>
    /// <param name="keyLabel">Key label</param>
    /// <param name="algorithm">Signature algorithm</param>
    /// <returns>Mock HSM signer</returns>
    public static IHsmSigner CreateMockSigner(string tokenSerial, string keyLabel, MockHsmSigner.HsmAlgorithm algorithm)
    {
        return new MockHsmSigner(tokenSerial, keyLabel, algorithm);
    }

    // Future implementations would include:
    // public static IHsmSigner CreatePkcs11Signer(string libraryPath, string tokenSerial, string keyLabel, string pin, Pkcs11Algorithm algorithm)
    // public static IHsmSigner CreateLunaSigner(LunaConfig config)
    // public static IHsmSigner CreateCloudHsmSigner(CloudHsmConfig config)
    // public static IHsmSigner CreateYubikeySigner(YubikeyConfig config)
}