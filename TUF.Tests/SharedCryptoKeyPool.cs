using System.Collections.Concurrent;

using TUF.Models;

namespace TUF.Tests;

/// <summary>
/// Shared cryptographic key pool for tests to reduce overhead of key generation.
/// This significantly improves test performance by reusing pre-generated cryptographic keys.
/// </summary>
public static class SharedCryptoKeyPool
{
    private static readonly ConcurrentBag<Ed25519Signer> _availableEd25519Signers = new();
    private static readonly ConcurrentBag<RsaSigner> _availableRsaSigners = new();
    private static readonly ConcurrentBag<EcdsaSigner> _availableEcdsaSigners = new();
    
    private static readonly object _ed25519Lock = new();
    private static readonly object _rsaLock = new();
    private static readonly object _ecdsaLock = new();

    private static int _ed25519Count = 0;
    private static int _rsaCount = 0;
    private static int _ecdsaCount = 0;

    private static volatile bool _initialized = false;

    /// <summary>
    /// Lazily initialize key pools with pre-generated keys for better performance.
    /// Only called when first key is requested.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            lock (_ed25519Lock)
            {
                if (!_initialized)
                {
                    // Pre-generate Ed25519 keys (most commonly used)
                    for (int i = 0; i < 10; i++)
                    {
                        _availableEd25519Signers.Add(Ed25519Signer.Generate());
                    }

                    // Pre-generate RSA keys (more expensive, fewer needed)  
                    for (int i = 0; i < 3; i++)
                    {
                        _availableRsaSigners.Add(RsaSigner.Generate());
                    }

                    // Pre-generate ECDSA keys
                    for (int i = 0; i < 3; i++)
                    {
                        _availableEcdsaSigners.Add(EcdsaSigner.Generate());
                    }

                    _initialized = true;
                }
            }
        }
    }

    /// <summary>
    /// Gets an Ed25519 signer from the pool or creates a new one if none are available.
    /// </summary>
    /// <returns>An Ed25519 signer ready for use.</returns>
    public static Ed25519Signer GetEd25519Signer()
    {
        EnsureInitialized();
        
        if (_availableEd25519Signers.TryTake(out var signer))
        {
            return signer;
        }

        lock (_ed25519Lock)
        {
            // Double-check in case another thread created one
            if (_availableEd25519Signers.TryTake(out signer))
            {
                return signer;
            }

            // Create new signer if pool is empty
            _ed25519Count++;
            return Ed25519Signer.Generate();
        }
    }

    /// <summary>
    /// Gets an RSA signer from the pool or creates a new one if none are available.
    /// </summary>
    /// <returns>An RSA signer ready for use.</returns>
    public static RsaSigner GetRsaSigner()
    {
        EnsureInitialized();
        
        if (_availableRsaSigners.TryTake(out var signer))
        {
            return signer;
        }

        lock (_rsaLock)
        {
            // Double-check in case another thread created one
            if (_availableRsaSigners.TryTake(out signer))
            {
                return signer;
            }

            // Create new signer if pool is empty
            _rsaCount++;
            return RsaSigner.Generate();
        }
    }

    /// <summary>
    /// Gets an ECDSA signer from the pool or creates a new one if none are available.
    /// </summary>
    /// <returns>An ECDSA signer ready for use.</returns>
    public static EcdsaSigner GetEcdsaSigner()
    {
        EnsureInitialized();
        
        if (_availableEcdsaSigners.TryTake(out var signer))
        {
            return signer;
        }

        lock (_ecdsaLock)
        {
            // Double-check in case another thread created one
            if (_availableEcdsaSigners.TryTake(out signer))
            {
                return signer;
            }

            // Create new signer if pool is empty
            _ecdsaCount++;
            return EcdsaSigner.Generate();
        }
    }

    /// <summary>
    /// Returns an Ed25519 signer to the pool for potential reuse.
    /// Note: In cryptographic tests, we typically don't reuse keys for security reasons,
    /// but we pre-generate them to save initialization time.
    /// </summary>
    /// <param name="signer">The signer to return (currently not reused).</param>
    public static void ReturnEd25519Signer(Ed25519Signer signer)
    {
        // For security reasons in tests, we don't reuse cryptographic keys
        // This method is provided for API consistency but currently does nothing
    }

    /// <summary>
    /// Returns an RSA signer to the pool for potential reuse.
    /// </summary>
    /// <param name="signer">The signer to return (currently not reused).</param>
    public static void ReturnRsaSigner(RsaSigner signer)
    {
        // For security reasons in tests, we don't reuse cryptographic keys
        // This method is provided for API consistency but currently does nothing
    }

    /// <summary>
    /// Returns an ECDSA signer to the pool for potential reuse.
    /// </summary>
    /// <param name="signer">The signer to return (currently not reused).</param>
    public static void ReturnEcdsaSigner(EcdsaSigner signer)
    {
        // For security reasons in tests, we don't reuse cryptographic keys
        // This method is provided for API consistency but currently does nothing
    }

    /// <summary>
    /// Gets statistics about key pool usage for performance monitoring.
    /// </summary>
    /// <returns>A tuple with usage statistics.</returns>
    public static (int Ed25519Generated, int RsaGenerated, int EcdsaGenerated, int Ed25519Available, int RsaAvailable, int EcdsaAvailable) GetPoolStats()
    {
        return (
            _ed25519Count,
            _rsaCount, 
            _ecdsaCount,
            _availableEd25519Signers.Count,
            _availableRsaSigners.Count,
            _availableEcdsaSigners.Count
        );
    }
}