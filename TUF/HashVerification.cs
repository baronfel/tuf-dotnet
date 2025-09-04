using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace TUF;

/// <summary>
/// High-performance hash verification utilities that eliminate string allocations
/// by working directly with byte spans and hex conversion.
/// </summary>
internal static class HashVerification
{
    private const int StackAllocThreshold = 256; // 64 bytes hash * 2 (hex) = 128 chars, well under threshold

    /// <summary>
    /// Verifies that the provided data matches at least one of the expected hashes.
    /// Uses optimized byte-level comparisons to avoid string allocations.
    /// </summary>
    /// <param name="data">The data to verify</param>
    /// <param name="expectedHashes">Dictionary of algorithm name to expected hex hash</param>
    /// <returns>True if at least one hash matches, false otherwise</returns>
    public static bool VerifyHashesOptimized(ReadOnlySpan<byte> data, IReadOnlyDictionary<string, string> expectedHashes)
    {
        foreach (var (algorithm, expectedHex) in expectedHashes)
        {
            if (VerifySingleHashOptimized(data, algorithm, expectedHex))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Verifies a single hash using optimized byte-level comparison.
    /// Avoids string allocations by converting hex strings to bytes and comparing directly.
    /// </summary>
    /// <param name="data">The data to hash and verify</param>
    /// <param name="algorithm">Hash algorithm name (case-insensitive)</param>
    /// <param name="expectedHex">Expected hash as hex string (case-insensitive)</param>
    /// <returns>True if the hash matches, false otherwise</returns>
    public static bool VerifySingleHashOptimized(ReadOnlySpan<byte> data, string algorithm, string expectedHex)
    {
        // Convert expected hex string to bytes for comparison
        var expectedHexSpan = expectedHex.AsSpan();
        
        // Stack allocate for typical hash sizes (SHA256=64 chars, SHA512=128 chars)
        Span<byte> expectedBytes = expectedHexSpan.Length <= StackAllocThreshold 
            ? stackalloc byte[expectedHexSpan.Length / 2]
            : new byte[expectedHexSpan.Length / 2];

        if (!TryParseHexString(expectedHexSpan, expectedBytes))
        {
            return false;
        }

        // Compute actual hash
        return algorithm.AsSpan().Equals("sha256", StringComparison.OrdinalIgnoreCase)
            ? VerifySha256Hash(data, expectedBytes)
            : algorithm.AsSpan().Equals("sha512", StringComparison.OrdinalIgnoreCase)
            ? VerifySha512Hash(data, expectedBytes)
            : false; // Unsupported algorithm
    }

    /// <summary>
    /// Computes SHA256 hash and compares directly with expected bytes.
    /// Uses stack allocation for the computed hash to avoid heap allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool VerifySha256Hash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expectedBytes)
    {
        Span<byte> actualHash = stackalloc byte[32]; // SHA256 is always 32 bytes
        var success = SHA256.TryHashData(data, actualHash, out var bytesWritten);
        return success && bytesWritten == 32 && actualHash.SequenceEqual(expectedBytes);
    }

    /// <summary>
    /// Computes SHA512 hash and compares directly with expected bytes.
    /// Uses stack allocation for the computed hash to avoid heap allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool VerifySha512Hash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expectedBytes)
    {
        Span<byte> actualHash = stackalloc byte[64]; // SHA512 is always 64 bytes
        var success = SHA512.TryHashData(data, actualHash, out var bytesWritten);
        return success && bytesWritten == 64 && actualHash.SequenceEqual(expectedBytes);
    }

    /// <summary>
    /// Parses a hex string into bytes. Case-insensitive and optimized for performance.
    /// Uses lookup table to avoid expensive char-to-int conversions.
    /// </summary>
    private static bool TryParseHexString(ReadOnlySpan<char> hex, Span<byte> bytes)
    {
        if (hex.Length % 2 != 0 || hex.Length / 2 != bytes.Length)
        {
            return false;
        }

        for (int i = 0; i < bytes.Length; i++)
        {
            if (!TryParseHexByte(hex[i * 2], hex[i * 2 + 1], out bytes[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Parses a single hex byte (two characters) using efficient lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexByte(char high, char low, out byte result)
    {
        var highValue = GetHexValue(high);
        var lowValue = GetHexValue(low);
        
        if (highValue < 0 || lowValue < 0)
        {
            result = 0;
            return false;
        }
        
        result = (byte)((highValue << 4) | lowValue);
        return true;
    }

    /// <summary>
    /// Converts a hex character to its numeric value. Case-insensitive and branchless.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHexValue(char c)
    {
        // Branchless hex parsing: works for both upper and lowercase
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return -1; // Invalid hex character
    }
}