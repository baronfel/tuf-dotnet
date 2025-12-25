using TUF.Models;

namespace TUF.HSM;

/// <summary>
/// YubiKey PIV (Personal Identity Verification) signer implementation.
/// Provides a simplified interface specifically for YubiKey devices using PIV slots.
/// </summary>
/// <remarks>
/// YubiKey PIV provides hardware-backed cryptographic operations with:
/// - Secure key storage in dedicated PIV slots (9A, 9C, 9D, 9E)
/// - PIN-based authentication
/// - Support for RSA 1024/2048 and ECC P-256/P-384 keys
/// - Air-gapped signing capabilities for offline root keys
/// - FIPS 140-2 Level 2 security (on FIPS models)
/// 
/// PIV Slot Usage for TUF:
/// - Slot 9A: Authentication - Recommended for client authentication
/// - Slot 9C: Digital Signature - **Recommended for TUF root keys**  
/// - Slot 9D: Key Management - For encryption/decryption
/// - Slot 9E: Card Authentication - For device authentication
/// 
/// Usage Example:
/// ```csharp
/// // For root key signing (air-gapped, highest security)
/// var rootSigner = await YubiKeySigner.CreateAsync(
///     slot: PivSlot.DigitalSignature, // 9C - requires user presence
///     pin: "123456"
/// );
/// 
/// // For automated signing in CI/CD (targets, timestamp, snapshot)
/// var targetsSigner = await YubiKeySigner.CreateAsync(
///     slot: PivSlot.Authentication, // 9A - can be configured for automatic use
///     pin: "123456"
/// );
/// ```
/// 
/// Security Considerations:
/// - Slot 9C requires user presence (touch) for each signing operation
/// - Use slot 9C for root keys to ensure manual approval of each signature
/// - Use slot 9A/9E for automated roles like targets, timestamp, snapshot
/// - PIN should be strong and unique to the YubiKey
/// - Consider using management key for generating keys directly on device
/// </remarks>
public sealed class YubiKeySigner : IHsmSigner, IDisposable
{
    private readonly Pkcs11Signer _pkcs11Signer;

    /// <summary>
    /// PIV slot used by this YubiKey signer.
    /// </summary>
    public PivSlot PivSlot { get; }

    /// <summary>
    /// The TUF Key information for this YubiKey signer.
    /// </summary>
    public Key Key => _pkcs11Signer.Key;

    /// <summary>
    /// Information about the YubiKey provider.
    /// </summary>
    public HsmProviderInfo ProviderInfo { get; }

    /// <summary>
    /// YubiKey PIV always requires PIN authentication.
    /// </summary>
    public bool RequiresAuthentication => true;

    private YubiKeySigner(Pkcs11Signer pkcs11Signer, PivSlot pivSlot)
    {
        _pkcs11Signer = pkcs11Signer;
        PivSlot = pivSlot;

        // Customize provider info for YubiKey
        ProviderInfo = new HsmProviderInfo
        {
            Name = "YubiKey PIV",
            Version = _pkcs11Signer.ProviderInfo.Version,
            Description = $"YubiKey PIV slot {pivSlot} - {GetSlotDescription(pivSlot)}",
            SupportedKeyTypes = new[] { "rsa", "ecdsa" },
            Metadata = new Dictionary<string, string>(_pkcs11Signer.ProviderInfo.Metadata ?? new Dictionary<string, string>())
            {
                ["PIVSlot"] = pivSlot.ToString(),
                ["SlotHex"] = $"0x{(int)pivSlot:X2}",
                ["UserPresenceRequired"] = (pivSlot == PivSlot.DigitalSignature).ToString()
            }
        };
    }

    /// <summary>
    /// Creates a YubiKey signer for the specified PIV slot.
    /// Automatically detects YubiKey devices and uses the YKCS11 library.
    /// </summary>
    /// <param name="slot">PIV slot to use for signing</param>
    /// <param name="pin">YubiKey PIN (default PIN is "123456")</param>
    /// <param name="ykcs11LibraryPath">Optional path to YKCS11 library (auto-detected if null)</param>
    /// <returns>Configured YubiKey signer</returns>
    /// <exception cref="InvalidOperationException">Thrown if YubiKey not found or slot empty</exception>
    public static async Task<YubiKeySigner> CreateAsync(
        PivSlot slot,
        string pin = "123456",
        string? ykcs11LibraryPath = null)
    {
        // Auto-detect YKCS11 library path if not provided
        ykcs11LibraryPath ??= DetectYkcs11LibraryPath();

        if (ykcs11LibraryPath == null)
            throw new InvalidOperationException(
                "YKCS11 library not found. Please install YubiKey PIV Tool or specify the library path explicitly.");

        // Create key label based on PIV slot
        var keyLabel = $"PIV {slot}";
        var keyType = "rsa"; // Default, will be detected from actual key

        // Find the first YubiKey slot (typically slot 0)
        ulong slotId = 0;

        // Create the underlying PKCS#11 signer
        var pkcs11Signer = await Pkcs11Signer.CreateAsync(ykcs11LibraryPath, slotId, keyLabel, keyType);

        // Authenticate with PIN
        var success = await pkcs11Signer.AuthenticateAsync(new PinCredentials(pin));
        if (!success)
            throw new InvalidOperationException("YubiKey PIN authentication failed. Check PIN and try again.");

        return new YubiKeySigner(pkcs11Signer, slot);
    }

    /// <summary>
    /// Creates a YubiKey signer and generates a new key pair in the specified slot.
    /// This is a advanced operation that requires the management key.
    /// </summary>
    /// <param name="slot">PIV slot where the new key will be generated</param>
    /// <param name="keyType">Type of key to generate ("rsa" or "ecdsa")</param>
    /// <param name="pin">YubiKey PIN</param>
    /// <param name="managementKey">YubiKey management key (hex string)</param>
    /// <param name="subject">Subject name for the self-signed certificate</param>
    /// <returns>YubiKey signer with newly generated key</returns>
    /// <exception cref="NotImplementedException">This method requires direct YubiKey PIV tool integration</exception>
    public static async Task<YubiKeySigner> CreateWithNewKeyAsync(
        PivSlot slot,
        string keyType,
        string pin = "123456",
        string? managementKey = null,
        string subject = "CN=TUF Signing Key")
    {
        // This would require integration with yubico-piv-tool or similar
        // For now, we'll throw NotImplementedException
        throw new NotImplementedException(
            "Key generation requires integration with yubico-piv-tool. " +
            "Please generate keys using external tools first, then use CreateAsync().");
    }

    /// <summary>
    /// Authenticates to the YubiKey using PIN.
    /// </summary>
    /// <param name="credentials">PIN credentials</param>
    /// <returns>True if authentication successful</returns>
    public async Task<bool> AuthenticateAsync(HsmCredentials credentials)
    {
        return await _pkcs11Signer.AuthenticateAsync(credentials);
    }

    /// <summary>
    /// Checks if currently authenticated to the YubiKey.
    /// </summary>
    public bool IsAuthenticated()
    {
        return _pkcs11Signer.IsAuthenticated();
    }

    /// <summary>
    /// Signs data using the YubiKey PIV slot.
    /// For slot 9C (DigitalSignature), this will require touching the YubiKey.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object</returns>
    /// <remarks>
    /// If using PIV slot 9C (Digital Signature), the YubiKey will require
    /// user presence confirmation (touching the gold contact) for each signature.
    /// This provides additional security for root key operations.
    /// </remarks>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        return _pkcs11Signer.SignBytes(data);
    }

    /// <summary>
    /// Signs data asynchronously using the YubiKey PIV slot.
    /// For slot 9C (DigitalSignature), this will require touching the YubiKey.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TUF signature object</returns>
    public async Task<SignatureObject> SignBytesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        return await _pkcs11Signer.SignBytesAsync(data, cancellationToken);
    }

    /// <summary>
    /// Logs out from the YubiKey PIV session.
    /// </summary>
    public async Task LogoutAsync()
    {
        await _pkcs11Signer.LogoutAsync();
    }

    /// <summary>
    /// Disposes the YubiKey resources.
    /// </summary>
    public void Dispose()
    {
        _pkcs11Signer?.Dispose();
    }

    /// <summary>
    /// Attempts to auto-detect the YKCS11 library path on the current platform.
    /// </summary>
    private static string? DetectYkcs11LibraryPath()
    {
        var candidatePaths = OperatingSystem.IsWindows() ? new string[] {
            @"C:\Program Files\Yubico\Yubico PIV Tool\bin\libykcs11.dll",
            @"C:\Program Files (x86)\Yubico\Yubico PIV Tool\bin\libykcs11.dll",
            @"libykcs11.dll" // In PATH
        } : OperatingSystem.IsMacOS() ? new string[] {
            "/usr/local/lib/libykcs11.dylib",
            "/opt/homebrew/lib/libykcs11.dylib",
            "libykcs11.dylib" // In PATH
        } : new string[] {
            "/usr/local/lib/libykcs11.so",
            "/usr/lib/libykcs11.so",
            "/usr/lib/x86_64-linux-gnu/libykcs11.so",
            "libykcs11.so" // In PATH
        };

        return candidatePaths.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Gets a human-readable description of a PIV slot.
    /// </summary>
    private static string GetSlotDescription(PivSlot slot)
    {
        return slot switch
        {
            PivSlot.Authentication => "Authentication (9A) - User authentication, automated signing",
            PivSlot.DigitalSignature => "Digital Signature (9C) - Requires user presence, ideal for root keys",
            PivSlot.KeyManagement => "Key Management (9D) - Key agreement, encryption/decryption",
            PivSlot.CardAuthentication => "Card Authentication (9E) - Device authentication",
            _ => $"Slot {slot:X2}"
        };
    }
}

/// <summary>
/// YubiKey PIV slots for cryptographic operations.
/// </summary>
public enum PivSlot
{
    /// <summary>
    /// PIV Authentication slot (9A) - Used for user authentication.
    /// Can be configured for automatic use without user presence.
    /// Recommended for: Targets, Timestamp, Snapshot role signing.
    /// </summary>
    Authentication = 0x9A,

    /// <summary>
    /// PIV Digital Signature slot (9C) - Used for digital signatures.
    /// **Requires user presence (touch) for each signing operation**.
    /// Recommended for: Root role signing (maximum security).
    /// </summary>
    DigitalSignature = 0x9C,

    /// <summary>
    /// PIV Key Management slot (9D) - Used for key agreement and encryption.
    /// Not typically used for TUF signing operations.
    /// </summary>
    KeyManagement = 0x9D,

    /// <summary>
    /// PIV Card Authentication slot (9E) - Used for device authentication.
    /// Can be configured for automatic use without user presence.
    /// Alternative for: Automated signing when 9A is unavailable.
    /// </summary>
    CardAuthentication = 0x9E
}