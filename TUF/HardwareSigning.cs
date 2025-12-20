using System.Security.Cryptography;

using TUF.Models;

namespace TUF.Hardware;

/// <summary>
/// Configuration for hardware security module (HSM) or hardware token signing providers.
/// Contains the necessary information to connect to and authenticate with hardware signing devices.
/// </summary>
/// <remarks>
/// Hardware signing provides enhanced security by:
/// - Keeping private keys secure within tamper-resistant hardware
/// - Requiring physical presence or PIN authentication for signing operations
/// - Meeting compliance requirements for high-value signing scenarios (root keys, etc.)
/// - Providing audit trails for all signing operations
/// 
/// Supported Hardware Types:
/// - YubiKey PIV tokens via PKCS#11
/// - Hardware Security Modules (HSMs) via PKCS#11
/// - Windows CNG Key Storage Providers (Windows only)
/// - Cloud HSM services (Azure Key Vault, AWS CloudHSM, etc.)
/// </remarks>
public class HardwareSigningConfig
{
    /// <summary>
    /// Hardware signing provider type.
    /// </summary>
    public HardwareSigningProvider Provider { get; init; } = HardwareSigningProvider.Pkcs11;

    /// <summary>
    /// Path to the PKCS#11 library for PKCS#11 providers.
    /// Platform-specific defaults:
    /// - Linux: /usr/local/lib/libykcs11.so (YubiKey), /usr/lib/softhsm/libsofthsm2.so (SoftHSM)
    /// - Windows: ykcs11.dll (YubiKey), after adding Yubico PIV Tool\bin to PATH
    /// - macOS: /usr/local/lib/libykcs11.dylib (YubiKey)
    /// </summary>
    public string? LibraryPath { get; init; }

    /// <summary>
    /// Slot ID for PKCS#11 tokens. YubiKey typically uses slot 0.
    /// Use null to auto-detect the first available slot with a token.
    /// </summary>
    public ulong? SlotId { get; init; }

    /// <summary>
    /// Key ID or label to identify the specific key on the hardware device.
    /// For YubiKey PIV, this corresponds to the PIV slot (e.g., "9c" for digital signature).
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// PIN for authenticating to the hardware device.
    /// SECURITY NOTE: In production, retrieve PIN from secure configuration, environment variables,
    /// or interactive prompts. Never hardcode PINs in source code.
    /// </summary>
    public string? Pin { get; init; }

    /// <summary>
    /// Windows CNG Key Storage Provider name for Windows CNG providers.
    /// Examples: "Microsoft Software Key Storage Provider", "Microsoft Smart Card Key Storage Provider"
    /// </summary>
    public string? CngProvider { get; init; }

    /// <summary>
    /// Connection timeout for hardware operations (default: 30 seconds).
    /// Hardware devices may require longer timeouts for operations requiring user interaction.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to require user interaction (physical presence) for signing operations.
    /// When true, the hardware device will require user confirmation (touch, etc.) for each signature.
    /// Recommended for high-security scenarios like root key signing.
    /// </summary>
    public bool RequireUserPresence { get; init; } = false;
}

/// <summary>
/// Hardware signing provider types supported by TUF .NET.
/// </summary>
public enum HardwareSigningProvider
{
    /// <summary>
    /// PKCS#11 hardware tokens and HSMs.
    /// Cross-platform support for YubiKey PIV, SafeNet, Thales, and other PKCS#11 devices.
    /// Requires PKCS#11 library installation and Pkcs11Interop NuGet package.
    /// </summary>
    Pkcs11,

    /// <summary>
    /// Windows Cryptography Next Generation (CNG) Key Storage Providers.
    /// Windows-only support for hardware devices with CNG providers.
    /// Includes Microsoft Smart Card KSP and third-party HSM providers.
    /// </summary>
    WindowsCng,

    /// <summary>
    /// Azure Key Vault Hardware Security Modules.
    /// Cloud-based HSM service with REST API access.
    /// Requires Azure SDK and appropriate authentication.
    /// </summary>
    AzureKeyVault,

    /// <summary>
    /// AWS CloudHSM hardware security modules.
    /// Cloud-based HSM service with PKCS#11 interface.
    /// Requires AWS SDK and VPN/private connection to HSM cluster.
    /// </summary>
    AwsCloudHsm
}

/// <summary>
/// Abstract base class for hardware-backed TUF signers.
/// Provides common infrastructure for hardware signing operations while allowing
/// provider-specific implementations for different hardware types.
/// </summary>
/// <remarks>
/// Hardware signers implement enhanced security practices:
/// - Private keys never leave the secure hardware boundary
/// - Signing operations are performed within tamper-resistant hardware
/// - Audit logging of all cryptographic operations
/// - Optional user presence requirements for high-security operations
/// 
/// Performance Considerations:
/// - Hardware operations are typically slower than software signing
/// - Network-based HSMs have additional latency
/// - User presence requirements add interactive delays
/// - Consider caching certificates and public key information
/// </remarks>
public abstract class HardwareSigner : ISigner, IDisposable
{
    /// <summary>
    /// Hardware signing configuration used by this signer.
    /// </summary>
    public HardwareSigningConfig Config { get; }

    /// <summary>
    /// The TUF Key information for this hardware signer.
    /// Contains the public key information retrieved from the hardware device.
    /// </summary>
    public abstract Key Key { get; }

    /// <summary>
    /// Whether the hardware device is currently available and accessible.
    /// </summary>
    public abstract bool IsAvailable { get; }

    /// <summary>
    /// Last known status of the hardware device connection.
    /// </summary>
    public abstract string Status { get; }

    /// <summary>
    /// Initializes a new hardware signer with the specified configuration.
    /// </summary>
    /// <param name="config">Hardware signing configuration</param>
    /// <exception cref="ArgumentNullException">Thrown if config is null</exception>
    protected HardwareSigner(HardwareSigningConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Signs the provided data using the hardware signing device.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object with key ID and signature value</returns>
    /// <exception cref="HardwareSigningException">Thrown if hardware signing fails</exception>
    public abstract SignatureObject SignBytes(ReadOnlySpan<byte> data);

    /// <summary>
    /// Tests connectivity and authentication to the hardware device.
    /// </summary>
    /// <returns>True if device is accessible and ready for signing</returns>
    public abstract Task<bool> TestConnectionAsync();

    /// <summary>
    /// Gets detailed information about the hardware device and key.
    /// Useful for diagnostics and audit logging.
    /// </summary>
    /// <returns>Hardware device information</returns>
    public abstract Task<HardwareDeviceInfo> GetDeviceInfoAsync();

    /// <summary>
    /// Releases hardware resources and closes connections.
    /// </summary>
    public abstract void Dispose();
}

/// <summary>
/// Information about a connected hardware signing device.
/// Used for diagnostics, audit logging, and device management.
/// </summary>
public class HardwareDeviceInfo
{
    /// <summary>
    /// Device manufacturer (e.g., "Yubico", "SafeNet", "Thales").
    /// </summary>
    public required string Manufacturer { get; init; }

    /// <summary>
    /// Device model or product name.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Device serial number for unique identification.
    /// </summary>
    public required string SerialNumber { get; init; }

    /// <summary>
    /// Firmware version running on the device.
    /// </summary>
    public string? FirmwareVersion { get; init; }

    /// <summary>
    /// Hardware version or revision.
    /// </summary>
    public string? HardwareVersion { get; init; }

    /// <summary>
    /// Supported algorithms by this device and key.
    /// </summary>
    public required IReadOnlyList<string> SupportedAlgorithms { get; init; }

    /// <summary>
    /// Whether this device requires user presence for signing.
    /// </summary>
    public bool RequiresUserPresence { get; init; }

    /// <summary>
    /// FIPS 140-2 compliance level (if applicable).
    /// </summary>
    public string? FipsLevel { get; init; }

    /// <summary>
    /// Common Criteria certification level (if applicable).
    /// </summary>
    public string? CommonCriteriaLevel { get; init; }

    /// <summary>
    /// Additional device-specific properties.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalProperties { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Exception thrown when hardware signing operations fail.
/// Provides detailed error information for troubleshooting hardware issues.
/// </summary>
public class HardwareSigningException : Exception
{
    /// <summary>
    /// Error code from the hardware provider (PKCS#11 return code, CNG error, etc.).
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Type of hardware operation that failed.
    /// </summary>
    public HardwareOperation Operation { get; }

    /// <summary>
    /// Creates a new hardware signing exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="operation">Hardware operation that failed</param>
    /// <param name="errorCode">Provider-specific error code</param>
    /// <param name="innerException">Underlying exception</param>
    public HardwareSigningException(string message, HardwareOperation operation, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Types of hardware operations that can fail.
/// </summary>
public enum HardwareOperation
{
    /// <summary>
    /// Connecting to the hardware device.
    /// </summary>
    Connect,

    /// <summary>
    /// Authenticating to the hardware device (PIN verification).
    /// </summary>
    Authenticate,

    /// <summary>
    /// Finding or loading a key on the device.
    /// </summary>
    LoadKey,

    /// <summary>
    /// Performing a signing operation.
    /// </summary>
    Sign,

    /// <summary>
    /// Reading device or key information.
    /// </summary>
    GetInfo,

    /// <summary>
    /// General hardware communication error.
    /// </summary>
    Communication
}

/// <summary>
/// Static factory class for creating hardware signers based on configuration.
/// Provides a unified entry point for all supported hardware signing providers.
/// </summary>
/// <remarks>
/// Usage Examples:
/// 
/// // YubiKey PIV signing
/// var yubiConfig = new HardwareSigningConfig
/// {
///     Provider = HardwareSigningProvider.Pkcs11,
///     KeyId = "9c", // PIV Digital Signature slot
///     Pin = Environment.GetEnvironmentVariable("YUBIKEY_PIN")
/// };
/// var yubiSigner = await HardwareSigningFactory.CreateAsync(yubiConfig);
/// 
/// // Windows Smart Card
/// var smartCardConfig = new HardwareSigningConfig
/// {
///     Provider = HardwareSigningProvider.WindowsCng,
///     CngProvider = "Microsoft Smart Card Key Storage Provider",
///     KeyId = "MySigningKey"
/// };
/// var smartCardSigner = await HardwareSigningFactory.CreateAsync(smartCardConfig);
/// </remarks>
public static class HardwareSigningFactory
{
    /// <summary>
    /// Creates a hardware signer based on the provided configuration.
    /// </summary>
    /// <param name="config">Hardware signing configuration</param>
    /// <returns>Hardware signer instance ready for signing operations</returns>
    /// <exception cref="ArgumentNullException">Thrown if config is null</exception>
    /// <exception cref="NotSupportedException">Thrown if provider is not supported</exception>
    /// <exception cref="HardwareSigningException">Thrown if hardware initialization fails</exception>
    public static async Task<HardwareSigner> CreateAsync(HardwareSigningConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        return config.Provider switch
        {
            HardwareSigningProvider.Pkcs11 => throw new NotImplementedException("PKCS#11 signer requires Pkcs11Interop package. See implementation guidance in documentation."),
            HardwareSigningProvider.WindowsCng => throw new NotImplementedException("Windows CNG signer requires Windows platform. See implementation guidance in documentation."),
            HardwareSigningProvider.AzureKeyVault => throw new NotImplementedException("Azure Key Vault signer requires Azure SDK. See implementation guidance in documentation."),
            HardwareSigningProvider.AwsCloudHsm => throw new NotImplementedException("AWS CloudHSM signer requires AWS SDK. See implementation guidance in documentation."),
            _ => throw new NotSupportedException($"Hardware signing provider {config.Provider} is not supported.")
        };
    }

    /// <summary>
    /// Gets information about available hardware signing providers on the current platform.
    /// </summary>
    /// <returns>List of supported providers with capability information</returns>
    public static async Task<IReadOnlyList<HardwareProviderInfo>> GetAvailableProvidersAsync()
    {
        var providers = new List<HardwareProviderInfo>();

        // PKCS#11 support (cross-platform)
        providers.Add(new HardwareProviderInfo
        {
            Provider = HardwareSigningProvider.Pkcs11,
            Name = "PKCS#11 Hardware Tokens and HSMs",
            IsAvailable = true, // Always potentially available
            SupportedAlgorithms = ["ed25519", "ecdsa-sha2-nistp256", "rsassa-pss-sha256"],
            RequiredPackages = ["Pkcs11Interop"],
            PlatformSupport = "Cross-platform (Linux, Windows, macOS)"
        });

        // Windows CNG support
        if (OperatingSystem.IsWindows())
        {
            providers.Add(new HardwareProviderInfo
            {
                Provider = HardwareSigningProvider.WindowsCng,
                Name = "Windows Cryptography Next Generation (CNG)",
                IsAvailable = true,
                SupportedAlgorithms = ["ecdsa-sha2-nistp256", "rsassa-pss-sha256"],
                RequiredPackages = [], // Built into .NET on Windows
                PlatformSupport = "Windows only"
            });
        }

        // Cloud providers (always potentially available)
        providers.Add(new HardwareProviderInfo
        {
            Provider = HardwareSigningProvider.AzureKeyVault,
            Name = "Azure Key Vault HSM",
            IsAvailable = true,
            SupportedAlgorithms = ["ecdsa-sha2-nistp256", "rsassa-pss-sha256"],
            RequiredPackages = ["Azure.Security.KeyVault.Keys", "Azure.Identity"],
            PlatformSupport = "Cross-platform (cloud service)"
        });

        providers.Add(new HardwareProviderInfo
        {
            Provider = HardwareSigningProvider.AwsCloudHsm,
            Name = "AWS CloudHSM",
            IsAvailable = true,
            SupportedAlgorithms = ["ecdsa-sha2-nistp256", "rsassa-pss-sha256"],
            RequiredPackages = ["AWSSDK.CloudHSM", "Pkcs11Interop"],
            PlatformSupport = "Cross-platform (cloud service)"
        });

        return providers.AsReadOnly();
    }
}

/// <summary>
/// Information about a hardware signing provider's capabilities and requirements.
/// </summary>
public class HardwareProviderInfo
{
    /// <summary>
    /// Hardware signing provider type.
    /// </summary>
    public required HardwareSigningProvider Provider { get; init; }

    /// <summary>
    /// Human-readable name of the provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this provider is available on the current platform.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// TUF signature schemes supported by this provider.
    /// </summary>
    public required IReadOnlyList<string> SupportedAlgorithms { get; init; }

    /// <summary>
    /// NuGet packages required to use this provider.
    /// </summary>
    public required IReadOnlyList<string> RequiredPackages { get; init; }

    /// <summary>
    /// Platform support description.
    /// </summary>
    public required string PlatformSupport { get; init; }

    /// <summary>
    /// Additional configuration notes or requirements.
    /// </summary>
    public string? ConfigurationNotes { get; init; }
}