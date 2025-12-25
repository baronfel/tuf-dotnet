using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TUF.Models;

namespace TUF.HSM;

/// <summary>
/// Windows CNG (Cryptography Next Generation) HSM signer implementation.
/// Works with Windows-integrated HSMs, smart cards, and TPM-backed certificates.
/// </summary>
/// <remarks>
/// Windows CNG provides native HSM integration through:
/// - Cryptographic Service Providers (CSPs)
/// - Key Storage Providers (KSPs) 
/// - Smart card middleware
/// - TPM (Trusted Platform Module) integration
/// - Certificate-based authentication
/// 
/// This implementation leverages X.509 certificates stored in Windows certificate stores
/// that are backed by HSM hardware. Common scenarios include:
/// - Smart cards with PIV/CAC certificates
/// - Network-attached HSMs with CNG providers
/// - TPM-backed certificates for device identity
/// - USB security tokens
/// 
/// Usage Example:
/// ```csharp
/// var signer = await WindowsCngSigner.CreateFromCertificateAsync(
///     certificateThumbprint: "ABC123...",
///     storeName: StoreName.My,
///     storeLocation: StoreLocation.CurrentUser
/// );
/// 
/// // CNG signers typically don't require separate authentication
/// // as they use Windows integrated authentication
/// var signature = await signer.SignBytesAsync(dataToSign);
/// ```
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsCngSigner : IHsmSigner, IDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly AsymmetricAlgorithm _algorithm;
    private readonly bool _isHardwareBacked;

    /// <summary>
    /// The TUF Key information for this CNG signer.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Information about the Windows CNG provider.
    /// </summary>
    public HsmProviderInfo ProviderInfo { get; }

    /// <summary>
    /// Most CNG signers use Windows integrated authentication and don't require separate credentials.
    /// However, some smart cards may require PIN entry through Windows UI.
    /// </summary>
    public bool RequiresAuthentication { get; }

    private WindowsCngSigner(
        X509Certificate2 certificate,
        AsymmetricAlgorithm algorithm,
        Key tufKey,
        HsmProviderInfo providerInfo,
        bool requiresAuthentication,
        bool isHardwareBacked)
    {
        _certificate = certificate;
        _algorithm = algorithm;
        Key = tufKey;
        ProviderInfo = providerInfo;
        RequiresAuthentication = requiresAuthentication;
        _isHardwareBacked = isHardwareBacked;
    }

    /// <summary>
    /// Creates a Windows CNG signer from a certificate in the Windows certificate store.
    /// </summary>
    /// <param name="certificateThumbprint">Thumbprint of the certificate to use</param>
    /// <param name="storeName">Certificate store name (default: My)</param>
    /// <param name="storeLocation">Certificate store location (default: CurrentUser)</param>
    /// <returns>Configured Windows CNG signer</returns>
    /// <exception cref="InvalidOperationException">Thrown if certificate not found or not suitable for signing</exception>
    public static async Task<WindowsCngSigner> CreateFromCertificateAsync(
        string certificateThumbprint,
        StoreName storeName = StoreName.My,
        StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        return await Task.Run(() =>
        {
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);
            if (certificates.Count == 0)
                throw new InvalidOperationException($"Certificate with thumbprint {certificateThumbprint} not found in {storeLocation}\\{storeName}");

            var certificate = certificates[0];
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Certificate must have an associated private key for signing");

            // Get the private key algorithm
            var algorithm = certificate.GetRSAPrivateKey() as AsymmetricAlgorithm 
                ?? certificate.GetECDsaPrivateKey() as AsymmetricAlgorithm
                ?? throw new InvalidOperationException("Certificate must use RSA or ECDSA keys");

            // Determine if the key is hardware-backed
            var isHardwareBacked = IsHardwareBacked(algorithm);
            // For simplicity, assume most certificates don't require separate authentication
            // Windows handles authentication through the OS
            var requiresAuth = false;

            // Create TUF Key based on algorithm type
            var tufKey = algorithm switch
            {
                RSA rsa => CreateRsaTufKey(rsa),
                ECDsa ecdsa => CreateEcdsaTufKey(ecdsa),
                _ => throw new NotSupportedException($"Algorithm {algorithm.GetType()} not supported")
            };

            // Get provider information
            var providerInfo = new HsmProviderInfo
            {
                Name = "Windows CNG",
                Version = Environment.OSVersion.Version.ToString(),
                Description = isHardwareBacked 
                    ? $"Hardware-backed certificate: {certificate.Subject}"
                    : $"Software certificate: {certificate.Subject}",
                SupportedKeyTypes = new[] { "rsa", "ecdsa" },
                Metadata = new Dictionary<string, string>
                {
                    ["CertificateThumbprint"] = certificate.Thumbprint,
                    ["Subject"] = certificate.Subject,
                    ["Issuer"] = certificate.Issuer,
                    ["SerialNumber"] = certificate.SerialNumber,
                    ["IsHardwareBacked"] = isHardwareBacked.ToString(),
                    ["StoreName"] = storeName.ToString(),
                    ["StoreLocation"] = storeLocation.ToString()
                }
            };

            return new WindowsCngSigner(certificate, algorithm, tufKey, providerInfo, requiresAuth, isHardwareBacked);
        });
    }

    /// <summary>
    /// Creates a Windows CNG signer by searching for a certificate by subject name.
    /// </summary>
    /// <param name="subjectName">Subject name to search for (can be partial)</param>
    /// <param name="storeName">Certificate store name</param>
    /// <param name="storeLocation">Certificate store location</param>
    /// <returns>Configured Windows CNG signer using the first matching certificate</returns>
    public static async Task<WindowsCngSigner> CreateFromSubjectAsync(
        string subjectName,
        StoreName storeName = StoreName.My,
        StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        return await Task.Run(() =>
        {
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
            if (certificates.Count == 0)
                throw new InvalidOperationException($"No certificates found with subject containing '{subjectName}' in {storeLocation}\\{storeName}");

            // Find the first certificate with a private key
            var certificate = certificates.Cast<X509Certificate2>()
                .FirstOrDefault(c => c.HasPrivateKey)
                ?? throw new InvalidOperationException("No certificates with private keys found");

            return CreateFromCertificateAsync(certificate.Thumbprint, storeName, storeLocation).Result;
        });
    }

    /// <summary>
    /// For Windows CNG, authentication is typically handled by Windows.
    /// This method is provided for compatibility but may not perform any action.
    /// </summary>
    /// <param name="credentials">Unused for most CNG providers</param>
    /// <returns>Always returns true as Windows handles authentication</returns>
    public async Task<bool> AuthenticateAsync(HsmCredentials credentials)
    {
        // For most Windows CNG scenarios, authentication is handled by Windows itself
        // Some smart card scenarios might prompt for PIN through Windows UI
        return await Task.FromResult(true);
    }

    /// <summary>
    /// For Windows CNG, we assume always authenticated through Windows.
    /// </summary>
    /// <returns>Always returns true</returns>
    public bool IsAuthenticated() => true;

    /// <summary>
    /// Signs data using the Windows CNG provider.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object</returns>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        return _algorithm switch
        {
            RSA rsa => SignWithRsa(rsa, data),
            ECDsa ecdsa => SignWithEcdsa(ecdsa, data),
            _ => throw new NotSupportedException($"Algorithm {_algorithm.GetType()} not supported for signing")
        };
    }

    /// <summary>
    /// Signs data asynchronously using the Windows CNG provider.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TUF signature object</returns>
    public async Task<SignatureObject> SignBytesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SignBytes(data.Span), cancellationToken);
    }

    /// <summary>
    /// Windows CNG handles session management automatically.
    /// </summary>
    public async Task LogoutAsync()
    {
        // CNG handles session management automatically
        await Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the certificate and algorithm resources.
    /// </summary>
    public void Dispose()
    {
        _certificate?.Dispose();
        _algorithm?.Dispose();
    }

    /// <summary>
    /// Signs data using RSA algorithm.
    /// </summary>
    private SignatureObject SignWithRsa(RSA rsa, ReadOnlySpan<byte> data)
    {
        // Determine RSA signing approach based on TUF key scheme
        byte[] signatureBytes = Key.Scheme switch
        {
            "rsassa-pss-sha256" => rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            "rsa-pkcs1v15-sha256" => rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            _ => rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss) // Default to PSS
        };

        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();
        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = signatureHex
        };
    }

    /// <summary>
    /// Signs data using ECDSA algorithm.
    /// </summary>
    private SignatureObject SignWithEcdsa(ECDsa ecdsa, ReadOnlySpan<byte> data)
    {
        var signatureBytes = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = signatureHex
        };
    }

    /// <summary>
    /// Creates a TUF Key object from an RSA algorithm instance.
    /// </summary>
    private static Key CreateRsaTufKey(RSA rsa)
    {
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();
        return new Key
        {
            KeyType = "rsa",
            Scheme = "rsassa-pss-sha256", // Prefer PSS for new keys
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    /// <summary>
    /// Creates a TUF Key object from an ECDSA algorithm instance.
    /// </summary>
    private static Key CreateEcdsaTufKey(ECDsa ecdsa)
    {
        if (ecdsa.KeySize != 256)
            throw new NotSupportedException("Only NIST P-256 ECDSA keys are supported for TUF");

        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        return new Key
        {
            KeyType = "ecdsa",
            Scheme = "ecdsa-sha2-nistp256",
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    /// <summary>
    /// Attempts to determine if an algorithm is hardware-backed.
    /// This is a best-effort check and may not be 100% reliable.
    /// </summary>
    private static bool IsHardwareBacked(AsymmetricAlgorithm algorithm)
    {
        try
        {
            // Try to access provider information
            // Hardware providers typically have specific names
            var algorithmName = algorithm.GetType().Name;
            
            // CNG algorithms backed by hardware often have different type names
            // or properties that indicate hardware backing
            if (algorithmName.Contains("Cng") || algorithmName.Contains("Csp"))
            {
                // Additional checks could be added here based on provider names
                // For now, assume CNG/CSP algorithms might be hardware-backed
                return true;
            }

            return false;
        }
        catch
        {
            // If we can't determine, assume software-backed
            return false;
        }
    }
}