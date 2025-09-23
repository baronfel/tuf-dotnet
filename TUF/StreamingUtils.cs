using System.Security.Cryptography;
using TUF.Exceptions;
using TUF.Models;

namespace TUF;

/// <summary>
/// Utilities for streaming operations in TUF, enabling memory-efficient handling of large files.
/// This addresses a key parity gap by providing streaming support matching other mature TUF implementations.
/// </summary>
public static class StreamingUtils
{
    /// <summary>
    /// Default buffer size for streaming operations (64KB)
    /// Optimized for balancing memory usage and performance
    /// </summary>
    public const int DefaultBufferSize = 65536;

    /// <summary>
    /// Copies data from source stream to destination stream while verifying target file integrity.
    /// This method enables memory-efficient downloading of large files without loading entire content into memory.
    /// </summary>
    /// <param name="source">Source stream to read from</param>
    /// <param name="destination">Destination stream to write to</param>
    /// <param name="targetFile">Target file metadata for verification</param>
    /// <param name="progress">Optional progress reporter for download progress</param>
    /// <param name="bufferSize">Buffer size for streaming operations (default 64KB)</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
    /// <returns>The computed hash of the downloaded data</returns>
    /// <exception cref="FileSizeException">Thrown when file size doesn't match expected length</exception>
    /// <exception cref="TargetIntegrityException">Thrown when hash verification fails</exception>
    public static async Task<string> CopyStreamWithHashVerification(
        Stream source,
        Stream destination,
        TargetFile targetFile,
        IProgress<(long bytesDownloaded, long totalBytes)>? progress = null,
        int bufferSize = DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
        var hashAlgorithms = CreateHashAlgorithms(targetFile.Hashes);
        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        long expectedLength = targetFile.Length;

        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                // Check if we're exceeding expected file size
                if (totalBytesRead + bytesRead > expectedLength)
                {
                    throw new FileSizeException($"Target file", expectedLength, totalBytesRead + bytesRead);
                }

                // Write to destination and update hash algorithms
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                
                foreach (var hashAlgorithm in hashAlgorithms)
                {
                    hashAlgorithm.Value.TransformBlock(buffer, 0, bytesRead, null, 0);
                }

                totalBytesRead += bytesRead;

                // Report progress if callback provided (always report, even for small files)
                progress?.Report((totalBytesRead, expectedLength));
            }
            
            // Ensure all data is written to destination
            await destination.FlushAsync(cancellationToken);

            // Verify final length
            if (totalBytesRead != expectedLength)
            {
                throw new FileSizeException($"Target file", expectedLength, totalBytesRead);
            }

            // Finalize hash calculations and verify
            var computedHashes = new Dictionary<string, string>();
            foreach (var (hashName, hashAlgorithm) in hashAlgorithms)
            {
                hashAlgorithm.TransformFinalBlock([], 0, 0);
                var computedHash = Convert.ToHexString(hashAlgorithm.Hash!).ToLowerInvariant();
                computedHashes[hashName] = computedHash;

                // Verify each hash
                if (targetFile.Hashes.TryGetValue(hashName, out var expectedHash) && 
                    !string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new TargetIntegrityException(
                        $"Hash verification failed for {hashName}",
                        "target file",
                        expectedHash,
                        computedHash);
                }
            }

            // Return first computed hash (typically sha256)
            return computedHashes.Values.First();
        }
        finally
        {
            // Dispose hash algorithms
            foreach (var hashAlgorithm in hashAlgorithms.Values)
            {
                hashAlgorithm.Dispose();
            }
        }
    }

    /// <summary>
    /// Downloads a file from HTTP source with streaming and hash verification.
    /// This provides memory-efficient alternative to loading entire files into byte arrays.
    /// </summary>
    /// <param name="httpClient">HTTP client for downloading</param>
    /// <param name="uri">URI to download from</param>
    /// <param name="targetFile">Target file metadata for verification</param>
    /// <param name="destinationStream">Stream to write downloaded data to</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="bufferSize">Buffer size for streaming</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Computed hash of downloaded data</returns>
    public static async Task<string> DownloadToStreamAsync(
        HttpClient httpClient,
        Uri uri,
        TargetFile targetFile,
        Stream destinationStream,
        IProgress<(long bytesDownloaded, long totalBytes)>? progress = null,
        int bufferSize = DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Verify content length if available
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value != targetFile.Length)
        {
            throw new FileSizeException("Downloaded file", targetFile.Length, contentLength.Value);
        }

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await CopyStreamWithHashVerification(
            responseStream, 
            destinationStream, 
            targetFile, 
            progress, 
            bufferSize, 
            cancellationToken);
    }

    /// <summary>
    /// Creates hash algorithms for the specified hash types in target file
    /// </summary>
    private static Dictionary<string, HashAlgorithm> CreateHashAlgorithms(Dictionary<string, string> hashes)
    {
        var algorithms = new Dictionary<string, HashAlgorithm>();

        foreach (var hashName in hashes.Keys)
        {
            HashAlgorithm algorithm = hashName.ToLowerInvariant() switch
            {
                "sha256" => SHA256.Create(),
                "sha512" => SHA512.Create(),
                "sha1" => SHA1.Create(),
                "md5" => MD5.Create(),
                _ => throw new ArgumentException($"Unsupported hash algorithm: {hashName}")
            };

            algorithms[hashName] = algorithm;
        }

        return algorithms;
    }
}