using System.Buffers;

namespace TUF.Performance;

/// <summary>
/// Memory pool for common TUF operations to reduce garbage collection pressure.
/// Provides reusable buffers for hex conversion, hashing, and signature operations.
/// </summary>
internal static class MemoryPool
{
    private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;
    private static readonly ArrayPool<char> CharPool = ArrayPool<char>.Shared;

    /// <summary>
    /// Gets a pooled byte buffer for temporary operations.
    /// Must be returned using ReturnBuffer.
    /// </summary>
    public static byte[] GetByteBuffer(int minimumLength) => BytePool.Rent(minimumLength);

    /// <summary>
    /// Gets a pooled char buffer for temporary operations.
    /// Must be returned using ReturnBuffer.
    /// </summary>
    public static char[] GetCharBuffer(int minimumLength) => CharPool.Rent(minimumLength);

    /// <summary>
    /// Returns a byte buffer to the pool.
    /// </summary>
    public static void ReturnBuffer(byte[] buffer, bool clearArray = false) => BytePool.Return(buffer, clearArray);

    /// <summary>
    /// Returns a char buffer to the pool.
    /// </summary>
    public static void ReturnBuffer(char[] buffer, bool clearArray = false) => CharPool.Return(buffer, clearArray);

    /// <summary>
    /// RAII wrapper for automatic buffer return.
    /// </summary>
    public readonly struct PooledByteBuffer : IDisposable
    {
        private readonly byte[] _buffer;
        private readonly bool _clearOnReturn;

        public PooledByteBuffer(int minimumLength, bool clearOnReturn = false)
        {
            _buffer = GetByteBuffer(minimumLength);
            _clearOnReturn = clearOnReturn;
        }

        public Span<byte> AsSpan() => _buffer.AsSpan();
        public Span<byte> AsSpan(int length) => _buffer.AsSpan(0, length);

        public void Dispose()
        {
            ReturnBuffer(_buffer, _clearOnReturn);
        }
    }

    /// <summary>
    /// RAII wrapper for automatic buffer return.
    /// </summary>
    public readonly struct PooledCharBuffer : IDisposable
    {
        private readonly char[] _buffer;
        private readonly bool _clearOnReturn;

        public PooledCharBuffer(int minimumLength, bool clearOnReturn = false)
        {
            _buffer = GetCharBuffer(minimumLength);
            _clearOnReturn = clearOnReturn;
        }

        public Span<char> AsSpan() => _buffer.AsSpan();
        public Span<char> AsSpan(int length) => _buffer.AsSpan(0, length);

        public void Dispose()
        {
            ReturnBuffer(_buffer, _clearOnReturn);
        }
    }
}