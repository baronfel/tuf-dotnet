using System.Runtime.CompilerServices;

namespace TUF.Performance;

/// <summary>
/// High-performance utilities for hex string conversion optimized for TUF operations.
/// Uses Span&lt;T&gt; and Memory&lt;T&gt; to minimize allocations during signature generation and verification.
/// </summary>
public static class HexUtilities
{
    private static ReadOnlySpan<byte> HexLookup => "0123456789abcdef"u8;

    /// <summary>
    /// Converts bytes to lowercase hex string using a provided buffer to avoid allocations.
    /// </summary>
    /// <param name="bytes">Input bytes to convert</param>
    /// <param name="destination">Destination span for hex characters (must be at least bytes.Length * 2)</param>
    /// <returns>The actual number of characters written</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToHexString(ReadOnlySpan<byte> bytes, Span<char> destination)
    {
        if (destination.Length < bytes.Length * 2)
            throw new ArgumentException("Destination buffer too small", nameof(destination));

        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            destination[i * 2] = (char)HexLookup[b >> 4];
            destination[i * 2 + 1] = (char)HexLookup[b & 0xF];
        }

        return bytes.Length * 2;
    }

    /// <summary>
    /// Converts bytes to lowercase hex string with allocation.
    /// Use only when buffer-based version cannot be used.
    /// </summary>
    /// <param name="bytes">Input bytes to convert</param>
    /// <returns>Hex string representation</returns>
    public static string ToHexString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length <= 256) // Use stackalloc for small byte arrays
        {
            Span<char> buffer = stackalloc char[bytes.Length * 2];
            var charsWritten = ToHexString(bytes, buffer);
            return new string(buffer.Slice(0, charsWritten));
        }
        else
        {
            // Fallback to built-in method for large arrays
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}