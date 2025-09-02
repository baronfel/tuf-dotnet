using System.Globalization;
using System.Text.Encodings.Web;

using Serde;

namespace CanonicalJson;
/// <summary>
/// Custom ISerializer implementation that produces canonical JSON output with explicit low-level control.
/// This implements Serde.NET's ISerializer interface to provide direct control over JSON emission.
/// </summary>
public sealed class CanonicalJsonSerializer : ISerializer, ITypeSerializer, IDisposable
{
    public readonly System.Text.Json.Utf8JsonWriter _writer;
    private readonly Stream _stream;
    private readonly System.Text.UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private bool _disposed;

    public CanonicalJsonSerializer(Stream memoryStream)
    {
        _stream = memoryStream;
        _writer = new System.Text.Json.Utf8JsonWriter(_stream, new System.Text.Json.JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true,
            Encoder = CanonicalJsonEncoder.Instance
        });
    }

    public void WriteBool(bool b) => _writer.WriteBooleanValue(b);
    public void WriteChar(char c) => WriteString(c.ToString());
    public void WriteString(string s) => _writer.WriteStringValue(s);

    public void WriteI8(sbyte i8) => _writer.WriteNumberValue(i8);
    public void WriteI16(short i16) => _writer.WriteNumberValue(i16);
    public void WriteI32(int i32) => _writer.WriteNumberValue(i32);
    public void WriteI64(long i64) => _writer.WriteNumberValue(i64);

    public void WriteU8(byte u8) => _writer.WriteNumberValue(u8);
    public void WriteU16(ushort u16) => _writer.WriteNumberValue(u16);
    public void WriteU32(uint u32) => _writer.WriteNumberValue(u32);
    public void WriteU64(ulong u64) => _writer.WriteNumberValue(u64);

    public void WriteF32(float f32) => WriteRaw(f32.ToString("G9", CultureInfo.InvariantCulture));
    public void WriteF64(double f64) => WriteRaw(f64.ToString("G17", CultureInfo.InvariantCulture));

    public void WriteDecimal(decimal dec) => WriteRaw(dec.ToString("G29", CultureInfo.InvariantCulture));

    public void WriteDateTime(DateTime dateTime) => WriteString(dateTime.ToString(Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture));
    public void WriteDateTimeOffset(DateTimeOffset dateTimeOffset) => WriteString(dateTimeOffset.ToString(Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture));

    public void WriteBytes(ReadOnlyMemory<byte> bytes) => _writer.WriteBase64StringValue(bytes.Span);

    public void WriteNull() => _writer.WriteNullValue();

    public ITypeSerializer WriteCollection(ISerdeInfo info, int? length)
    {
        switch (info.Kind)
        {
            case InfoKind.Dictionary:
                _writer.WriteStartObject();
                return new DictImpl(this, _writer);
            case InfoKind.List:
                _writer.WriteStartArray();
                return new EnumerableImpl(this, _writer);
            default:
                throw new NotSupportedException($"Unsupported collection type: {info.Kind}");
        }
    }

    public ITypeSerializer WriteType(ISerdeInfo info)
    {
        return new ReorderingSerializer(this);
    }

    public void WriteRaw(string value) => _writer.WriteRawValue(_utf8.GetBytes(value));

    public void WriteObjectStart() => _writer.WriteStartObject();
    public void WriteObjectEnd() => _writer.WriteEndObject();
    public void WriteArrayStart() => _writer.WriteStartArray();
    public void WriteArrayEnd() => _writer.WriteEndArray();

    public void WriteBool(ISerdeInfo info, int index, bool b)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteBool(b);
    }

    public void WriteChar(ISerdeInfo info, int index, char c)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteChar(c);
    }

    public void WriteString(ISerdeInfo info, int index, string s)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteString(s);
    }

    public void WriteI8(ISerdeInfo info, int index, sbyte i8)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteI8(i8);
    }

    public void WriteI16(ISerdeInfo info, int index, short i16)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteI16(i16);
    }

    public void WriteI32(ISerdeInfo info, int index, int i32)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteI32(i32);
    }

    public void WriteI64(ISerdeInfo info, int index, long i64)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteI64(i64);
    }

    public void WriteU8(ISerdeInfo info, int index, byte u8)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteU8(u8);
    }

    public void WriteU16(ISerdeInfo info, int index, ushort u16)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteU16(u16);
    }

    public void WriteU32(ISerdeInfo info, int index, uint u32)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteU32(u32);
    }

    public void WriteU64(ISerdeInfo info, int index, ulong u64)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteU64(u64);
    }

    public void WriteF32(ISerdeInfo info, int index, float f32)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteF32(f32);
    }

    public void WriteF64(ISerdeInfo info, int index, double f64)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteF64(f64);
    }

    public void WriteDecimal(ISerdeInfo info, int index, decimal dec)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteDecimal(dec);
    }

    public void WriteDateTime(ISerdeInfo info, int index, DateTime dateTime)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteDateTime(dateTime);
    }

    public void WriteDateTimeOffset(ISerdeInfo info, int index, DateTimeOffset dateTimeOffset)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteDateTimeOffset(dateTimeOffset);
    }

    public void WriteBytes(ISerdeInfo info, int index, ReadOnlyMemory<byte> bytes)
    {
        _writer.WritePropertyName(info.GetFieldName(index));
        WriteBytes(bytes);
    }

    public void WriteNull(ISerdeInfo info, int index) => WriteNull();

    void ITypeSerializer.WriteValue<T>(ISerdeInfo typeInfo, int index, T value, ISerialize<T> serialize) where T : class
    {
        _writer.WritePropertyName(typeInfo.GetFieldName(index));
        serialize.Serialize(value, this);
    }

    void ITypeSerializer.End(ISerdeInfo info) => _writer.WriteEndObject();

    public void Dispose()
    {
        if (!_disposed)
        {
            _writer?.Flush();
            _disposed = true;
        }
    }

    public sealed class DictImpl(ISerializer parent, System.Text.Json.Utf8JsonWriter writer) : ITypeSerializer
    {
        private readonly ISerializer _keySerializer = new KeySerializer(writer);
        public void End(ISerdeInfo info)
        {
            writer.WriteEndObject();
        }

        private ISerializer GetSerializer(int index)
            => index % 2 == 0 ? _keySerializer : parent;

        public void WriteBool(ISerdeInfo typeInfo, int index, bool b) => GetSerializer(index).WriteBool(b);
        public void WriteChar(ISerdeInfo typeInfo, int index, char c) => GetSerializer(index).WriteChar(c);
        public void WriteDecimal(ISerdeInfo typeInfo, int index, decimal d) => GetSerializer(index).WriteDecimal(d);
        public void WriteF32(ISerdeInfo typeInfo, int index, float f) => GetSerializer(index).WriteF32(f);
        public void WriteF64(ISerdeInfo typeInfo, int index, double d) => GetSerializer(index).WriteF64(d);
        public void WriteI16(ISerdeInfo typeInfo, int index, short i16) => GetSerializer(index).WriteI16(i16);
        public void WriteI32(ISerdeInfo typeInfo, int index, int i32) => GetSerializer(index).WriteI32(i32);
        public void WriteI64(ISerdeInfo typeInfo, int index, long i64) => GetSerializer(index).WriteI64(i64);
        public void WriteI8(ISerdeInfo typeInfo, int index, sbyte b) => GetSerializer(index).WriteI8(b);
        public void WriteNull(ISerdeInfo typeInfo, int index) => GetSerializer(index).WriteNull();
        public void WriteString(ISerdeInfo typeInfo, int index, string s) => GetSerializer(index).WriteString(s);
        public void WriteU16(ISerdeInfo typeInfo, int index, ushort u16) => GetSerializer(index).WriteU16(u16);
        public void WriteU32(ISerdeInfo typeInfo, int index, uint u32) => GetSerializer(index).WriteU32(u32);
        public void WriteU64(ISerdeInfo typeInfo, int index, ulong u64) => GetSerializer(index).WriteU64(u64);
        public void WriteU8(ISerdeInfo typeInfo, int index, byte b) => GetSerializer(index).WriteU8(b);
        public void WriteDateTime(ISerdeInfo typeInfo, int index, DateTime dt) => GetSerializer(index).WriteDateTime(dt);
        public void WriteDateTimeOffset(ISerdeInfo typeInfo, int index, DateTimeOffset dt) => GetSerializer(index).WriteDateTimeOffset(dt);
        public void WriteBytes(ISerdeInfo typeInfo, int index, ReadOnlyMemory<byte> bytes) => GetSerializer(index).WriteBytes(bytes);
        public void WriteValue<T>(ISerdeInfo typeInfo, int index, T value, ISerialize<T> serialize)
            where T : class?
            => serialize.Serialize(value, GetSerializer(index));
    }

    public sealed class KeySerializer(System.Text.Json.Utf8JsonWriter writer) : ISerializer
    {
        internal sealed class KeyNotStringException() : Exception("JSON allows only strings in this location, expected a string.") { }
        public void WriteBool(bool b) => throw new KeyNotStringException();
        public void WriteChar(char c) => throw new KeyNotStringException();
        public void WriteU8(byte b) => throw new KeyNotStringException();
        public void WriteU16(ushort u16) => throw new KeyNotStringException();
        public void WriteU32(uint u32) => throw new KeyNotStringException();
        public void WriteU64(ulong u64) => throw new KeyNotStringException();
        public void WriteI8(sbyte b) => throw new KeyNotStringException();
        public void WriteI16(short i16) => throw new KeyNotStringException();
        public void WriteI32(int i32) => throw new KeyNotStringException();
        public void WriteI64(long i64) => throw new KeyNotStringException();
        public void WriteF32(float f) => throw new KeyNotStringException();
        public void WriteF64(double d) => throw new KeyNotStringException();
        public void WriteDecimal(decimal d) => throw new KeyNotStringException();
        public void WriteDateTime(DateTime dt) => throw new KeyNotStringException();
        public void WriteDateTimeOffset(DateTimeOffset dt) => throw new KeyNotStringException();
        public void WriteBytes(ReadOnlyMemory<byte> bytes) => throw new KeyNotStringException();

        public void WriteString(string s)
        {
            writer.WritePropertyName(s);
        }

        ITypeSerializer ISerializer.WriteCollection(ISerdeInfo typeInfo, int? size) => throw new KeyNotStringException();
        ITypeSerializer ISerializer.WriteType(ISerdeInfo typeInfo) => throw new KeyNotStringException();
        public void WriteNull() => throw new KeyNotStringException();
    }

    public sealed class EnumerableImpl(ISerializer serializer, System.Text.Json.Utf8JsonWriter writer) : ITypeSerializer
    {
        public void End(ISerdeInfo info)
        {
            writer.WriteEndArray();
        }

        public void WriteBool(ISerdeInfo typeInfo, int index, bool b)
        {
            serializer.WriteBool(b);
        }

        public void WriteChar(ISerdeInfo typeInfo, int index, char c)
        {
            serializer.WriteChar(c);
        }

        public void WriteU8(ISerdeInfo typeInfo, int index, byte b)
        {
            serializer.WriteU8(b);
        }

        public void WriteU16(ISerdeInfo typeInfo, int index, ushort u16) => serializer.WriteU16(u16);

        public void WriteU32(ISerdeInfo typeInfo, int index, uint u32)
        {
            serializer.WriteU32(u32);
        }

        public void WriteU64(ISerdeInfo typeInfo, int index, ulong u64)
        {
            serializer.WriteU64(u64);
        }

        public void WriteI8(ISerdeInfo typeInfo, int index, sbyte b)
        {
            serializer.WriteI8(b);
        }

        public void WriteI16(ISerdeInfo typeInfo, int index, short i16)
        {
            serializer.WriteI16(i16);
        }

        public void WriteI32(ISerdeInfo typeInfo, int index, int i32)
        {
            serializer.WriteI32(i32);
        }

        public void WriteI64(ISerdeInfo typeInfo, int index, long i64)
        {
            serializer.WriteI64(i64);
        }

        public void WriteF32(ISerdeInfo typeInfo, int index, float f)
        {
            serializer.WriteF32(f);
        }

        public void WriteF64(ISerdeInfo typeInfo, int index, double d)
        {
            serializer.WriteF64(d);
        }

        public void WriteDecimal(ISerdeInfo typeInfo, int index, decimal d)
        {
            serializer.WriteDecimal(d);
        }

        public void WriteString(ISerdeInfo typeInfo, int index, string s)
        {
            serializer.WriteString(s);
        }

        public void WriteNull(ISerdeInfo typeInfo, int index)
        {
            serializer.WriteNull();
        }

        public void WriteDateTime(ISerdeInfo typeInfo, int index, DateTime dt)
        {
            serializer.WriteDateTime(dt);
        }
        public void WriteDateTimeOffset(ISerdeInfo typeInfo, int index, DateTimeOffset dt)
        {
            serializer.WriteDateTimeOffset(dt);
        }
        public void WriteBytes(ISerdeInfo typeInfo, int index, ReadOnlyMemory<byte> bytes)
        {
            serializer.WriteBytes(bytes);
        }

        public void WriteValue<T>(ISerdeInfo typeInfo, int index, T value, ISerialize<T> serialize)
            where T : class?
        {
            serialize.Serialize(value, serializer);
        }
    }
}

/// <summary>
/// tracks property writes and reorders them for canonicalization.
/// builds it up internally and when 'end' is called flushes the whole thing
/// to the parent writer
/// </summary>
internal class ReorderingSerializer : ITypeSerializer
{
    private readonly CanonicalJsonSerializer _parent;
    private readonly Dictionary<string, Action> _properties = new();

    public ReorderingSerializer(CanonicalJsonSerializer parent)
    {
        _parent = parent;
    }

    public void End(ISerdeInfo info)
    {
        _parent._writer.WriteStartObject();
        foreach (var prop in _properties.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            prop.Value();
        }
        _parent._writer.WriteEndObject();
    }

    public void WriteBool(ISerdeInfo typeInfo, int index, bool b)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteBoolean(name, b);
    }

    public void WriteBytes(ISerdeInfo typeInfo, int index, ReadOnlyMemory<byte> bytes)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => { _parent._writer.WriteBase64String(name, bytes.Span); };
    }

    public void WriteChar(ISerdeInfo typeInfo, int index, char c)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteString(name, c.ToString());
    }

    public void WriteDateTime(ISerdeInfo typeInfo, int index, DateTime dt)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteString(name, dt.ToString(Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat));
    }

    public void WriteDateTimeOffset(ISerdeInfo typeInfo, int index, DateTimeOffset dt)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteString(name, dt.ToString(Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat));
    }

    public void WriteDecimal(ISerdeInfo typeInfo, int index, decimal d)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, d);
    }

    public void WriteF32(ISerdeInfo typeInfo, int index, float f)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, f);
    }

    public void WriteF64(ISerdeInfo typeInfo, int index, double d)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, d);
    }

    public void WriteI16(ISerdeInfo typeInfo, int index, short i16)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, i16);
    }

    public void WriteI32(ISerdeInfo typeInfo, int index, int i32)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, i32);
    }

    public void WriteI64(ISerdeInfo typeInfo, int index, long i64)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, i64);
    }

    public void WriteI8(ISerdeInfo typeInfo, int index, sbyte b)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, b);
    }

    public void WriteNull(ISerdeInfo typeInfo, int index)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNull(name);
    }

    public void WriteString(ISerdeInfo typeInfo, int index, string s)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteString(name, s);
    }

    public void WriteU16(ISerdeInfo typeInfo, int index, ushort u16)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, u16);
    }

    public void WriteU32(ISerdeInfo typeInfo, int index, uint u32)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, u32);
    }

    public void WriteU64(ISerdeInfo typeInfo, int index, ulong u64)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, u64);
    }

    public void WriteU8(ISerdeInfo typeInfo, int index, byte b)
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () => _parent._writer.WriteNumber(name, b);
    }

    void ITypeSerializer.WriteValue<T>(ISerdeInfo typeInfo, int index, T value, ISerialize<T> serialize) where T : class
    {
        var name = typeInfo.GetFieldStringName(index);
        _properties[name] = () =>
        {
            _parent._writer.WritePropertyName(name);
            serialize.Serialize(value, _parent);
        };
    }
}

/// <summary>
/// A custom JavaScriptEncoder that implements canonical JSON escaping rules.
/// This encoder escapes only the two byte values required by canonical JSON:
/// - Backslash (\) -> \\
/// - Double quote (") -> \"
/// All other characters, including control characters and Unicode, remain as literal bytes.
/// </summary>
public sealed class CanonicalJsonEncoder : JavaScriptEncoder
{
    /// <summary>
    /// Singleton instance of the canonical JSON encoder.
    /// </summary>
    public static readonly CanonicalJsonEncoder Instance = new();

    private CanonicalJsonEncoder() { }

    public override bool WillEncode(int unicodeScalar)
    {
        // Canonical JSON as subset of valid JSON - escape all required JSON characters
        return unicodeScalar == 0x22   // " (quote)
            || unicodeScalar == 0x5C   // \ (backslash) 
            || unicodeScalar == 0x08   // \b (backspace)
            || unicodeScalar == 0x0C   // \f (form feed)
            || unicodeScalar == 0x0A   // \n (line feed)
            || unicodeScalar == 0x0D   // \r (carriage return)
            || unicodeScalar == 0x09   // \t (tab)
            || unicodeScalar < 0x20;   // Other control characters
    }

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        for (int i = 0; i < textLength; i++)
        {
            if (WillEncode(text[i]))
            {
                return i;
            }
        }
        return -1;
    }

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
    {
        if (!WillEncode(unicodeScalar))
        {
            numberOfCharactersWritten = 0;
            return false;
        }

        if (bufferLength < 2)
        {
            numberOfCharactersWritten = 0;
            return false;
        }

        switch (unicodeScalar)
        {
            case 0x22: // "
                buffer[0] = '\\';
                buffer[1] = '"';
                numberOfCharactersWritten = 2;
                return true;
            case 0x5C: // \
                buffer[0] = '\\';
                buffer[1] = '\\';
                numberOfCharactersWritten = 2;
                return true;
            case 0x08: // \b
                buffer[0] = '\\';
                buffer[1] = 'b';
                numberOfCharactersWritten = 2;
                return true;
            case 0x0C: // \f
                buffer[0] = '\\';
                buffer[1] = 'f';
                numberOfCharactersWritten = 2;
                return true;
            case 0x0A: // \n
                buffer[0] = '\\';
                buffer[1] = 'n';
                numberOfCharactersWritten = 2;
                return true;
            case 0x0D: // \r
                buffer[0] = '\\';
                buffer[1] = 'r';
                numberOfCharactersWritten = 2;
                return true;
            case 0x09: // \t
                buffer[0] = '\\';
                buffer[1] = 't';
                numberOfCharactersWritten = 2;
                return true;
            default:
                // Other control characters - use Unicode escape
                if (unicodeScalar < 0x20 && bufferLength >= 6)
                {
                    buffer[0] = '\\';
                    buffer[1] = 'u';
                    buffer[2] = '0';
                    buffer[3] = '0';
                    var hex = ((ushort)unicodeScalar).ToString("x2");
                    buffer[4] = hex[0];
                    buffer[5] = hex[1];
                    numberOfCharactersWritten = 6;
                    return true;
                }
                break;
        }

        numberOfCharactersWritten = 0;
        return false;
    }

    public override int MaxOutputCharactersPerInputCharacter => 6; // For \uXXXX Unicode escapes
}