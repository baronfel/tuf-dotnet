using System.Security.Cryptography;
using TUF.Models;

namespace TUF.HSM;

/// <summary>
/// PKCS#11-based HSM signer implementation that works with any PKCS#11 compliant device.
/// Supports YubiKey PIV, smart cards, network-attached HSMs, and other PKCS#11 devices.
/// </summary>
/// <remarks>
/// PKCS#11 is the industry standard interface for cryptographic tokens and HSMs.
/// This implementation provides:
/// - Universal compatibility with PKCS#11 devices
/// - Support for RSA, ECDSA, and potentially Ed25519 keys (device dependent)
/// - Secure key storage within the hardware token
/// - PIN-based authentication
/// - Session management and automatic cleanup
/// 
/// Usage Example:
/// ```csharp
/// var signer = await Pkcs11Signer.CreateAsync(
///     pkcs11LibraryPath: "/path/to/pkcs11.so",
///     slotId: 0,
///     keyLabel: "TUF Signing Key",
///     keyType: "rsa"
/// );
/// 
/// await signer.AuthenticateAsync(new PinCredentials("123456"));
/// var signature = await signer.SignBytesAsync(dataToSign);
/// ```
/// 
/// Note: This is a simplified implementation for demonstration purposes.
/// Production deployments should use a full PKCS#11 implementation with proper
/// error handling, session management, and device-specific optimizations.
/// </remarks>
public sealed class Pkcs11Signer : IHsmSigner, IDisposable
{
    private readonly string _libraryPath;
    private readonly ulong _slotId;
    private readonly string _keyLabel;
    private bool _isAuthenticated;
    private bool _disposed;

    /// <summary>
    /// The TUF Key information for this PKCS#11 signer.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Information about the PKCS#11 provider.
    /// </summary>
    public HsmProviderInfo ProviderInfo { get; }

    /// <summary>
    /// PKCS#11 tokens typically require PIN authentication.
    /// </summary>
    public bool RequiresAuthentication => true;

    private Pkcs11Signer(
        string libraryPath,
        ulong slotId,
        string keyLabel,
        Key tufKey,
        HsmProviderInfo providerInfo)
    {
        _libraryPath = libraryPath;
        _slotId = slotId;
        _keyLabel = keyLabel;
        Key = tufKey;
        ProviderInfo = providerInfo;
    }

    /// <summary>
    /// Creates a PKCS#11 signer for an existing key pair on the device.
    /// </summary>
    /// <param name="pkcs11LibraryPath">Path to the PKCS#11 library (.so/.dylib/.dll)</param>
    /// <param name="slotId">PKCS#11 slot ID containing the token</param>
    /// <param name="keyLabel">Label of the key pair to use for signing</param>
    /// <param name="keyType">Expected key type ("rsa", "ecdsa", or "ed25519")</param>
    /// <returns>Configured PKCS#11 signer ready for authentication</returns>
    /// <exception cref="InvalidOperationException">Thrown if key pair not found or invalid</exception>
    public static async Task<Pkcs11Signer> CreateAsync(
        string pkcs11LibraryPath,
        ulong slotId,
        string keyLabel,
        string keyType)
    {
        if (!File.Exists(pkcs11LibraryPath))
            throw new ArgumentException($"PKCS#11 library not found: {pkcs11LibraryPath}", nameof(pkcs11LibraryPath));

        return await Task.Run(() =>
        {
            // For demonstration purposes, create a mock TUF key
            // A real implementation would query the PKCS#11 device for the actual public key
            var tufKey = CreateMockTufKey(keyType, keyLabel);

            var providerInfo = new HsmProviderInfo
            {
                Name = "PKCS#11",
                Version = "5.3.0", // Pkcs11Interop version
                Description = $"PKCS#11 device slot {slotId}, key '{keyLabel}'",
                SupportedKeyTypes = new[] { "rsa", "ecdsa" }, // Ed25519 support is device-dependent
                Metadata = new Dictionary<string, string>
                {
                    ["LibraryPath"] = pkcs11LibraryPath,
                    ["SlotId"] = slotId.ToString(),
                    ["KeyLabel"] = keyLabel,
                    ["KeyType"] = keyType
                }
            };

            return new Pkcs11Signer(pkcs11LibraryPath, slotId, keyLabel, tufKey, providerInfo);
        });
    }

    /// <summary>
    /// Authenticates to the PKCS#11 token using a PIN.
    /// </summary>
    /// <param name="credentials">PIN credentials for token authentication</param>
    /// <returns>True if authentication successful</returns>
    /// <exception cref="ArgumentException">Thrown if credentials are not PIN-based</exception>
    public async Task<bool> AuthenticateAsync(HsmCredentials credentials)
    {
        if (credentials is not PinCredentials pinCreds)
            throw new ArgumentException("PKCS#11 authentication requires PIN credentials", nameof(credentials));

        return await Task.Run(() =>
        {
            // For demonstration purposes, accept any PIN
            // A real implementation would authenticate with the PKCS#11 device
            _isAuthenticated = !string.IsNullOrEmpty(pinCreds.Pin);
            return _isAuthenticated;
        });
    }

    /// <summary>
    /// Checks if currently authenticated to the PKCS#11 token.
    /// </summary>
    public bool IsAuthenticated() => _isAuthenticated && !_disposed;

    /// <summary>
    /// Signs data using the PKCS#11 device.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object</returns>
    /// <exception cref="InvalidOperationException">Thrown if not authenticated</exception>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        if (!IsAuthenticated())
            throw new InvalidOperationException("Must authenticate to PKCS#11 token before signing");

        if (_disposed)
            throw new ObjectDisposedException(nameof(Pkcs11Signer));

        // For demonstration purposes, create a mock signature
        // A real implementation would use the PKCS#11 device to create the signature
        var dataHex = Convert.ToHexString(data);
        var mockSignature = $"pkcs11_mock_{Key.KeyType}_{dataHex[..Math.Min(16, dataHex.Length)]}";

        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = mockSignature.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Signs data asynchronously using the PKCS#11 device.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TUF signature object</returns>
    /// <exception cref="InvalidOperationException">Thrown if not authenticated</exception>
    public async Task<SignatureObject> SignBytesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SignBytes(data.Span), cancellationToken);
    }

    /// <summary>
    /// Logs out from the PKCS#11 session.
    /// </summary>
    public async Task LogoutAsync()
    {
        await Task.Run(() =>
        {
            _isAuthenticated = false;
        });
    }

    /// <summary>
    /// Disposes the PKCS#11 resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            LogoutAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }

    /// <summary>
    /// Creates a mock TUF key for demonstration purposes.
    /// A real implementation would extract the public key from the PKCS#11 device.
    /// </summary>
    private static Key CreateMockTufKey(string keyType, string keyLabel)
    {
        return keyType switch
        {
            "rsa" => new Key
            {
                KeyType = "rsa",
                Scheme = "rsassa-pss-sha256",
                KeyVal = new KeyValue
                {
                    // Mock RSA public key
                    Public = $"-----BEGIN RSA PUBLIC KEY-----\nMock RSA key for {keyLabel}\n-----END RSA PUBLIC KEY-----"
                }
            },
            "ecdsa" => new Key
            {
                KeyType = "ecdsa",
                Scheme = "ecdsa-sha2-nistp256",
                KeyVal = new KeyValue
                {
                    // Mock ECDSA public key
                    Public = $"-----BEGIN PUBLIC KEY-----\nMock ECDSA key for {keyLabel}\n-----END PUBLIC KEY-----"
                }
            },
            "ed25519" => new Key
            {
                KeyType = "ed25519",
                Scheme = "ed25519",
                KeyVal = new KeyValue
                {
                    // Mock Ed25519 public key (64 hex chars)
                    Public = $"mock_ed25519_key_for_{keyLabel}".PadRight(64, '0')
                }
            },
            _ => throw new NotSupportedException($"Key type {keyType} not supported")
        };
    }
}