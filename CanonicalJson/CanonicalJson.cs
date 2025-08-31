using System.Buffers;
using System.Globalization;
using System.Text;

using Serde;

namespace CanonicalJson;

/// <summary>
/// The CanonicalJsonSerializer serializes and deserializes objects to/from a canonical JSON format
/// following the Canonical JSON reference (see http://wiki.laptop.org/go/Canonical_JSON).
/// This implementation uses Serde.NET for explicit, low-level control of JSON values.
///
/// Notes:
/// - Object properties are emitted in lexicographic order by UTF-8 byte sequence.
/// - No insignificant whitespace is emitted.
/// - Strings are emitted with minimal escaping (only backslash and double quote).
/// - Numbers attempt to use a shortest, round-trip-safe representation.
/// </summary>
public static class CanonicalJsonSerializer
{
    /// <summary>
    /// Serialize an object to canonical JSON using Serde.NET's serialization infrastructure.
    /// This provides explicit, low-level control of the JSON output format.
    /// </summary>
    public static byte[] Serialize<T>(T obj) where T : ISerializeProvider<T> => Serialize<T, T>(obj);

    /// <summary>
    /// Serialize an object to canonical JSON using Serde.NET's serialization infrastructure.
    /// This provides explicit, low-level control of the JSON output format.
    /// </summary>
    public static byte[] Serialize<T, TProvider>(T obj) where TProvider : ISerializeProvider<T>
    {
        using var ms = new MemoryStream();
        var serializer = new CanonicalJsonSerdeWriter(ms);
        TProvider.Instance.Serialize(obj, serializer);
        return ms.ToArray();
    }

    // Compare two strings by their UTF-8 byte sequence (lexicographic order)
    public static int CompareUtf8(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        int min = Math.Min(ab.Length, bb.Length);
        for (int i = 0; i < min; i++)
        {
            int ca = ab[i];
            int cb = bb[i];
            if (ca != cb) return ca - cb;
        }
        return ab.Length - bb.Length;
    }
}

/// <summary>
/// Custom ISerializer implementation that produces canonical JSON output with explicit low-level control.
/// This implements Serde.NET's ISerializer interface to provide direct control over JSON emission.
/// </summary>
public sealed class CanonicalJsonSerdeWriter : ISerializer, IDisposable
{
    private readonly Stream _stream;
    private readonly UTF8Encoding _utf8 = new(false);
    private bool _disposed;

    public CanonicalJsonSerdeWriter(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public void WriteBool(bool b) => WriteRaw(b ? "true" : "false");
    public void WriteChar(char c) => WriteString(c.ToString());

    public void WriteString(string s)
    {
        WriteRaw("\"");
        WriteEscapedString(s);
        WriteRaw("\"");
    }

    public void WriteI8(sbyte i8) => WriteRaw(i8.ToString(CultureInfo.InvariantCulture));
    public void WriteI16(short i16) => WriteRaw(i16.ToString(CultureInfo.InvariantCulture));
    public void WriteI32(int i32) => WriteRaw(i32.ToString(CultureInfo.InvariantCulture));
    public void WriteI64(long i64) => WriteRaw(i64.ToString(CultureInfo.InvariantCulture));

    public void WriteU8(byte u8) => WriteRaw(u8.ToString(CultureInfo.InvariantCulture));
    public void WriteU16(ushort u16) => WriteRaw(u16.ToString(CultureInfo.InvariantCulture));
    public void WriteU32(uint u32) => WriteRaw(u32.ToString(CultureInfo.InvariantCulture));
    public void WriteU64(ulong u64) => WriteRaw(u64.ToString(CultureInfo.InvariantCulture));

    public void WriteF32(float f32) => WriteRaw(f32.ToString("G9", CultureInfo.InvariantCulture));
    public void WriteF64(double f64) => WriteRaw(f64.ToString("G17", CultureInfo.InvariantCulture));

    public void WriteDecimal(decimal dec) => WriteRaw(dec.ToString("G29", CultureInfo.InvariantCulture));

    public void WriteDateTime(DateTime dateTime) => WriteString(dateTime.ToString("O", CultureInfo.InvariantCulture));
    public void WriteDateTimeOffset(DateTimeOffset dateTimeOffset) => WriteString(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));

    public void WriteBytes(ReadOnlyMemory<byte> bytes) => WriteString(Convert.ToBase64String(bytes.Span));

    public void WriteNull() => WriteRaw("null");

    public ITypeSerializer WriteCollection(ISerdeInfo info, int? length)
    {
        WriteArrayStart();
        return new CanonicalJsonTypeSerializer(this, isArray: true);
    }

    public ITypeSerializer WriteType(ISerdeInfo info)
    {
        WriteObjectStart();
        return new CanonicalJsonTypeSerializer(this, isArray: false);
    }

    // Helper methods for direct writing
    public void WriteRaw(string value)
    {
        var bytes = _utf8.GetBytes(value);
        _stream.Write(bytes, 0, bytes.Length);
    }

    private void WriteEscapedString(string value)
    {
        // Canonical JSON only requires escaping backslash and double quote
        var searchValues = SearchValues.Create(['\\', '\"']);
        var span = value.AsSpan();

        while (span.IndexOfAny(searchValues) is int idx && idx != -1)
        {
            if (idx > 0)
            {
                WriteRaw(span[..idx].ToString());
            }
            WriteRaw("\\");
            WriteRaw(span[idx].ToString());
            span = span[(idx + 1)..];
        }

        if (span.Length > 0)
        {
            WriteRaw(span.ToString());
        }
    }

    public void WriteComma() => WriteRaw(",");
    public void WriteColon() => WriteRaw(":");
    public void WriteObjectStart() => WriteRaw("{");
    public void WriteObjectEnd() => WriteRaw("}");
    public void WriteArrayStart() => WriteRaw("[");
    public void WriteArrayEnd() => WriteRaw("]");

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Flush();
            _disposed = true;
        }
    }

    private sealed class CanonicalJsonTypeSerializer : ITypeSerializer
    {
        private readonly CanonicalJsonSerdeWriter _writer;
        private readonly bool _isArray;
        private bool _first = true;

        public CanonicalJsonTypeSerializer(CanonicalJsonSerdeWriter writer, bool isArray)
        {
            _writer = writer;
            _isArray = isArray;
        }

        public void WriteBool(ISerdeInfo info, int index, bool b)
        {
            WritePrefix(info, index);
            _writer.WriteBool(b);
        }

        public void WriteChar(ISerdeInfo info, int index, char c)
        {
            WritePrefix(info, index);
            _writer.WriteChar(c);
        }

        public void WriteString(ISerdeInfo info, int index, string s)
        {
            WritePrefix(info, index);
            _writer.WriteString(s);
        }

        public void WriteI8(ISerdeInfo info, int index, sbyte i8)
        {
            WritePrefix(info, index);
            _writer.WriteI8(i8);
        }

        public void WriteI16(ISerdeInfo info, int index, short i16)
        {
            WritePrefix(info, index);
            _writer.WriteI16(i16);
        }

        public void WriteI32(ISerdeInfo info, int index, int i32)
        {
            WritePrefix(info, index);
            _writer.WriteI32(i32);
        }

        public void WriteI64(ISerdeInfo info, int index, long i64)
        {
            WritePrefix(info, index);
            _writer.WriteI64(i64);
        }

        public void WriteU8(ISerdeInfo info, int index, byte u8)
        {
            WritePrefix(info, index);
            _writer.WriteU8(u8);
        }

        public void WriteU16(ISerdeInfo info, int index, ushort u16)
        {
            WritePrefix(info, index);
            _writer.WriteU16(u16);
        }

        public void WriteU32(ISerdeInfo info, int index, uint u32)
        {
            WritePrefix(info, index);
            _writer.WriteU32(u32);
        }

        public void WriteU64(ISerdeInfo info, int index, ulong u64)
        {
            WritePrefix(info, index);
            _writer.WriteU64(u64);
        }

        public void WriteF32(ISerdeInfo info, int index, float f32)
        {
            WritePrefix(info, index);
            _writer.WriteF32(f32);
        }

        public void WriteF64(ISerdeInfo info, int index, double f64)
        {
            WritePrefix(info, index);
            _writer.WriteF64(f64);
        }

        public void WriteDecimal(ISerdeInfo info, int index, decimal dec)
        {
            WritePrefix(info, index);
            _writer.WriteDecimal(dec);
        }

        public void WriteDateTime(ISerdeInfo info, int index, DateTime dateTime)
        {
            WritePrefix(info, index);
            _writer.WriteDateTime(dateTime);
        }

        public void WriteDateTimeOffset(ISerdeInfo info, int index, DateTimeOffset dateTimeOffset)
        {
            WritePrefix(info, index);
            _writer.WriteDateTimeOffset(dateTimeOffset);
        }

        public void WriteBytes(ISerdeInfo info, int index, ReadOnlyMemory<byte> bytes)
        {
            WritePrefix(info, index);
            _writer.WriteBytes(bytes);
        }

        public void WriteNull(ISerdeInfo info, int index)
        {
            WritePrefix(info, index);
            _writer.WriteNull();
        }

        void ITypeSerializer.WriteValue<T>(ISerdeInfo info, int index, T value, ISerialize<T> serialize)
        {
            WritePrefix(info, index);
            serialize.Serialize(value, _writer);
        }

        public void End(ISerdeInfo info)
        {
            if (_isArray)
                _writer.WriteArrayEnd();
            else
                _writer.WriteObjectEnd();
        }

        private void WritePrefix(ISerdeInfo info, int index)
        {
            if (!_first)
            {
                _writer.WriteComma();
            }
            _first = false;

            if (!_isArray)
            {
                // For objects, write the field name
                _writer.WriteString(info.Name);
                _writer.WriteColon();
            }
        }
    }
}