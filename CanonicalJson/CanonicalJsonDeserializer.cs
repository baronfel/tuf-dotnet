using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Serde;

namespace CanonicalJson;

/// <summary>
/// Custom IDeserializer implementation that reads canonical JSON implementing its unusual escaping rules.
/// This implements Serde.NET's IDeserializer and ITypeDeserializer interfaces to provide 
/// direct control over JSON deserialization.
/// 
/// Canonical JSON only escapes '\' and '"' characters in strings - all other content 
/// including control characters should remain unescaped per the canonical JSON spec.
/// </summary>
public sealed class CanonicalJsonSerdeReader : IDeserializer, ITypeDeserializer, IDisposable
{
    private readonly JsonDocument _document;
    internal JsonElement _currentElement;
    private ObjectEnumeratorState? _currentObjectState;
    private readonly Stack<ObjectEnumeratorState> _objectStateStack;
    private readonly Stack<ArrayEnumeratorState> _arrayStateStack;
    private bool _disposed;

    // Reference-based enumerator state to avoid struct copying issues
    private class ObjectEnumeratorState
    {
        public JsonProperty[] Enumerator { get; set; }
        private int idx = -1;

        public ObjectEnumeratorState(JsonElement element)
        {
            Enumerator = element.EnumerateObject().ToArray();
        }

        public bool TryMoveNext()
        {
            if (idx >= Enumerator.Length - 1) return false;
            idx++;
            return true;
        }

        public JsonProperty Current
        {
            get
            {
                if (idx == -1) throw new InvalidOperationException("Enumerator is not positioned at a valid property");
                return Enumerator[idx];
            }
        }

        public bool HasValue => idx != -1;
        public bool IsFinished => idx == Enumerator.Length;
    }

    private class ArrayEnumeratorState
    {
        public JsonElement[] Enumerator { get; set; }
        public bool HasMoreElements => idx < Enumerator.Length;
        private int idx = -1;

        public ArrayEnumeratorState(JsonElement element)
        {
            Enumerator = element.EnumerateArray().ToArray();
        }

        public bool MoveNext()
        {
            if (idx >= Enumerator.Length - 1) return false;
            idx++;
            return true;
        }

        public JsonElement Current => Enumerator[idx];
    }

    public CanonicalJsonSerdeReader(string content)
    {
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Content cannot be null or empty", nameof(content));

        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        };

        _document = JsonDocument.Parse(content, options);
        _objectStateStack = new Stack<ObjectEnumeratorState>();
        _arrayStateStack = new Stack<ArrayEnumeratorState>();
        _currentElement = _document.RootElement;
    }

    public int? SizeOpt => null;

    public void Dispose()
    {
        if (!_disposed)
        {
            _document?.Dispose();
            _disposed = true;
        }
    }

    public bool ReadBool()
    {
        EnsureValueKind(JsonValueKind.True, JsonValueKind.False);
        return _currentElement.GetBoolean();
    }

    public bool ReadBool(ISerdeInfo info, int index) => ReadBool();

    public void ReadBytes(IBufferWriter<byte> writer)
    {
        EnsureValueKind(JsonValueKind.String);
        var base64String = GetCanonicalString();
        var bytes = Convert.FromBase64String(base64String);
        writer.Write(bytes);
    }

    public void ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer) => ReadBytes(writer);

    public char ReadChar()
    {
        var str = ReadString();
        if (str.Length != 1)
            throw new JsonException($"Expected single character, got string of length {str.Length}");
        return str[0];
    }

    public char ReadChar(ISerdeInfo info, int index) => ReadChar();

    public DateTime ReadDateTime()
    {
        EnsureValueKind(JsonValueKind.String);
        var dateString = GetCanonicalString();
        return DateTimeOffset.ParseExact(dateString, Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture).UtcDateTime;
    }

    public DateTime ReadDateTime(ISerdeInfo info, int index) => ReadDateTime();

    public decimal ReadDecimal()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetDecimal();
    }

    public decimal ReadDecimal(ISerdeInfo info, int index) => ReadDecimal();

    public float ReadF32()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetSingle();
    }

    public float ReadF32(ISerdeInfo info, int index) => ReadF32();

    public double ReadF64()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetDouble();
    }

    public double ReadF64(ISerdeInfo info, int index) => ReadF64();

    public short ReadI16()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetInt16();
    }

    public short ReadI16(ISerdeInfo info, int index) => ReadI16();

    public int ReadI32()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetInt32();
    }

    public int ReadI32(ISerdeInfo info, int index) => ReadI32();

    public long ReadI64()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetInt64();
    }

    public long ReadI64(ISerdeInfo info, int index) => ReadI64();

    public sbyte ReadI8()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetSByte();
    }

    public sbyte ReadI8(ISerdeInfo info, int index) => ReadI8();

    public T? ReadNullableRef<T>(IDeserialize<T> deserialize) where T : class
    {
        if (_currentElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return deserialize.Deserialize(this);
    }

    public string ReadString()
    {
        if (_currentElement.ValueKind == JsonValueKind.Null)
            return null!;

        EnsureValueKind(JsonValueKind.String);
        return GetCanonicalString();
    }

    public string ReadString(ISerdeInfo info, int index) => ReadString();

    public ITypeDeserializer ReadType(ISerdeInfo typeInfo)
    {
        if (typeInfo.Kind == InfoKind.List)
        {
            EnsureValueKind(JsonValueKind.Array);
            return new ArrayTypeDeserializer(this);
        }
        else if (typeInfo.Kind == InfoKind.Dictionary)
        {
            EnsureValueKind(JsonValueKind.Object);
            return new DictionaryTypeDeserializer(this, typeInfo);
        }
        else
        {
            EnsureValueKind(JsonValueKind.Object);
            return new ObjectTypeDeserializer(this, typeInfo);
        }
    }

    public ushort ReadU16()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetUInt16();
    }

    public ushort ReadU16(ISerdeInfo info, int index) => ReadU16();

    public uint ReadU32()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetUInt32();
    }

    public uint ReadU32(ISerdeInfo info, int index) => ReadU32();

    public ulong ReadU64()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetUInt64();
    }

    public ulong ReadU64(ISerdeInfo info, int index) => ReadU64();

    public byte ReadU8()
    {
        EnsureValueKind(JsonValueKind.Number);
        return _currentElement.GetByte();
    }

    public byte ReadU8(ISerdeInfo info, int index) => ReadU8();

    T ITypeDeserializer.ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) where T : class
    {
        return deserialize.Deserialize(this);
    }

    public void SkipValue(ISerdeInfo info, int index)
    {
        // For JsonDocument approach, we don't need to skip since elements are already parsed
    }

    public int TryReadIndex(ISerdeInfo info, out string? errorName)
    {
        errorName = null;

        if (_currentObjectState == null)
        {
            return ITypeDeserializer.EndOfType;
        }

        if (!_currentObjectState.TryMoveNext())
        {
            _currentObjectState = null;
            return ITypeDeserializer.EndOfType;
        }

        var property = _currentObjectState.Current;
        var propertyNameBytes = Encoding.UTF8.GetBytes(property.Name);

        var index = info.TryGetIndex(propertyNameBytes);
        _currentElement = property.Value;

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureValueKind(params JsonValueKind[] expectedKinds)
    {
        foreach (var expectedKind in expectedKinds)
        {
            if (_currentElement.ValueKind == expectedKind)
                return;
        }

        throw new JsonException($"Expected {string.Join(" or ", expectedKinds)}, got {_currentElement.ValueKind}");
    }

    /// <summary>
    /// Gets the string value from the current JSON string element with canonical JSON string handling.
    /// Only unescapes '\\' and '\"' - all other content remains as-is per canonical JSON spec.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetCanonicalString()
    {
        var rawString = _currentElement.GetRawText();

        // Remove surrounding quotes
        if (rawString.Length >= 2 && rawString[0] == '"' && rawString[^1] == '"')
        {
            rawString = rawString[1..^1];
        }

        // Fast path: if no backslashes, return directly
        if (rawString.IndexOf('\\') == -1)
        {
            return rawString;
        }

        // Properly unescape JSON escape sequences
        var sb = new StringBuilder(rawString.Length);
        var i = 0;

        while (i < rawString.Length)
        {
            if (rawString[i] == '\\' && i + 1 < rawString.Length)
            {
                var nextChar = rawString[i + 1];
                switch (nextChar)
                {
                    case '"':
                        sb.Append('"');
                        i += 2;
                        continue;
                    case '\\':
                        sb.Append('\\');
                        i += 2;
                        continue;
                    case '/':
                        sb.Append('/');
                        i += 2;
                        continue;
                    case 'b':
                        sb.Append('\b');
                        i += 2;
                        continue;
                    case 'f':
                        sb.Append('\f');
                        i += 2;
                        continue;
                    case 'n':
                        sb.Append('\n');
                        i += 2;
                        continue;
                    case 'r':
                        sb.Append('\r');
                        i += 2;
                        continue;
                    case 't':
                        sb.Append('\t');
                        i += 2;
                        continue;
                    case 'u' when i + 5 < rawString.Length:
                        // Unicode escape sequence \uXXXX
                        var hexString = rawString.Substring(i + 2, 4);
                        if (ushort.TryParse(hexString, NumberStyles.HexNumber, null, out ushort unicodeValue))
                        {
                            sb.Append((char)unicodeValue);
                            i += 6;
                            continue;
                        }
                        break;
                }
            }

            // Copy character as-is
            sb.Append(rawString[i]);
            i++;
        }

        return sb.ToString();
    }

    public void AdvancePastPropertyName()
    {
        // if(_currentElement.)
    }

    // Internal methods for type deserializers
    internal void StartObject(JsonElement element)
    {
        // Push current object state to stack if we have one
        if (_currentObjectState != null)
        {
            _objectStateStack.Push(_currentObjectState);
        }

        _currentObjectState = new ObjectEnumeratorState(element);
    }

    internal void EndObject()
    {
        // Pop previous object state from stack
        _currentObjectState = _objectStateStack.Count > 0 ? _objectStateStack.Pop() : null;
    }

    internal void StartArray(JsonElement element)
    {
        _arrayStateStack.Push(new ArrayEnumeratorState(element));
    }

    internal bool TryGetNextArrayElement(out JsonElement element)
    {
        element = default;
        if (_arrayStateStack.Count == 0) return false;

        var arrayState = _arrayStateStack.Peek();
        if (arrayState.MoveNext())
        {
            element = arrayState.Current;
            _currentElement = element;
            return true;
        }
        _currentElement = default;
        _arrayStateStack.Pop();
        return false;
    }

    internal bool TryGetNextDictionaryKeyValue(out JsonProperty property)
    {
        property = default;
        if (_currentObjectState == null) return false;

        // Move to the next property in the dictionary
        if (_currentObjectState.TryMoveNext())
        {
            property = _currentObjectState.Current;
            _currentElement = property.Value;
            return true;
        }

        _currentElement = default;
        return false;
    }
    
    internal string? GetCurrentDictionaryKey()
    {
        return _currentObjectState?.HasValue == true ? _currentObjectState.Current.Name : null;
    }
}

/// <summary>
/// Type deserializer for JSON objects
/// </summary>
internal sealed class ObjectTypeDeserializer : ITypeDeserializer
{
    private readonly CanonicalJsonSerdeReader _parent;
    private readonly ISerdeInfo _typeInfo;
    private bool _initialized;
    
    public ObjectTypeDeserializer(CanonicalJsonSerdeReader parent, ISerdeInfo typeInfo)
    {
        _parent = parent;
        _typeInfo = typeInfo;
    }

    public int? SizeOpt => null;
    
    public int TryReadIndex(ISerdeInfo info, out string? errorName)
    {
        if (!_initialized)
        {
            _initialized = true;
            _parent.StartObject(_parent._currentElement);
        }
        
        var result = _parent.TryReadIndex(info, out errorName);
        
        if (result == ITypeDeserializer.EndOfType)
        {
            _parent.EndObject();
        }
        
        return result;
    }
    
    public T ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) where T : class?
    {
        return deserialize.Deserialize(_parent);
    }
    
    public void SkipValue(ISerdeInfo info, int index)
    {
        // No-op for JsonDocument approach
    }
    
    // All the primitive read methods - delegate to parent
    public bool ReadBool(ISerdeInfo info, int index) => _parent.ReadBool(info, index);
    public char ReadChar(ISerdeInfo info, int index) => _parent.ReadChar(info, index);
    public byte ReadU8(ISerdeInfo info, int index) => _parent.ReadU8(info, index);
    public ushort ReadU16(ISerdeInfo info, int index) => _parent.ReadU16(info, index);
    public uint ReadU32(ISerdeInfo info, int index) => _parent.ReadU32(info, index);
    public ulong ReadU64(ISerdeInfo info, int index) => _parent.ReadU64(info, index);
    public sbyte ReadI8(ISerdeInfo info, int index) => _parent.ReadI8(info, index);
    public short ReadI16(ISerdeInfo info, int index) => _parent.ReadI16(info, index);
    public int ReadI32(ISerdeInfo info, int index) => _parent.ReadI32(info, index);
    public long ReadI64(ISerdeInfo info, int index) => _parent.ReadI64(info, index);
    public float ReadF32(ISerdeInfo info, int index) => _parent.ReadF32(info, index);
    public double ReadF64(ISerdeInfo info, int index) => _parent.ReadF64(info, index);
    public decimal ReadDecimal(ISerdeInfo info, int index) => _parent.ReadDecimal(info, index);
    public string ReadString(ISerdeInfo info, int index) => _parent.ReadString(info, index);
    public DateTime ReadDateTime(ISerdeInfo info, int index) => _parent.ReadDateTime(info, index);
    public void ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer) => _parent.ReadBytes(info, index, writer);
}

/// <summary>
/// Type deserializer for JSON objects that represent dictionaries
/// Dictionary deserialization needs to enumerate through JSON properties sequentially,
/// and alternate between reading property names (keys) and property values
/// </summary>
internal sealed class DictionaryTypeDeserializer : ITypeDeserializer
{
    private readonly CanonicalJsonSerdeReader _parent;
    private readonly ISerdeInfo _typeInfo;
    private bool _initialized;
    private bool _isKey = true; // Track if we're reading key or value
    
    public DictionaryTypeDeserializer(CanonicalJsonSerdeReader parent, ISerdeInfo typeInfo)
    {
        _parent = parent;
        _typeInfo = typeInfo;
    }

    public int? SizeOpt => null;
    
    public int TryReadIndex(ISerdeInfo info, out string? errorName)
    {
        errorName = null;
        
        if (!_initialized)
        {
            _initialized = true;
            _parent.StartObject(_parent._currentElement);
            
            // Try to get the first property
            if (!_parent.TryGetNextDictionaryKeyValue(out var firstProperty))
            {
                _parent.EndObject();
                return ITypeDeserializer.EndOfType;
            }
            
            _isKey = true;
            return 0; // Always return 0 for dictionary entries
        }
        
        if (_isKey)
        {
            // We've positioned at a key, now prepare for value read
            _isKey = false;
            return 0; // Still the same "entry"
        }
        else
        {
            // We just read a value, advance to next key-value pair
            if (!_parent.TryGetNextDictionaryKeyValue(out var nextProperty))
            {
                _parent.EndObject();
                return ITypeDeserializer.EndOfType;
            }
            
            _isKey = true;
            return 0; // New entry, but still return 0
        }
    }
    
    public T ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) where T : class?
    {
        // Reading a complex value should never happen when expecting a key
        if (_isKey)
        {
            throw new JsonException("Cannot read complex value as dictionary key - keys must be strings");
        }
        // Reading the value - this is a complex object value
        return deserialize.Deserialize(_parent);
    }
    
    public void SkipValue(ISerdeInfo info, int index)
    {
        // Skipping should not change state as TryReadIndex controls the flow
    }
    
    // For dictionary keys, we need to return the property name, not read from JSON value
    public string ReadString(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            // Reading the dictionary key
            var currentKey = _parent.GetCurrentDictionaryKey();
            if (currentKey == null)
            {
                throw new JsonException("Cannot read dictionary key - no current property available");
            }
            return currentKey;
        }
        else
        {
            // Reading the property value from JSON
            return _parent.ReadString(info, index);
        }
    }
    
    // All the primitive read methods - follow the same pattern as ReadString
    public bool ReadBool(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as boolean - keys must be strings");
        }
        return _parent.ReadBool(info, index);
    }
    
    public char ReadChar(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            var key = _parent.GetCurrentDictionaryKey();
            if (key == null || key.Length != 1)
            {
                throw new JsonException("Cannot read dictionary key as char - key must be single character");
            }
            return key[0];
        }
        return _parent.ReadChar(info, index);
    }
    
    public byte ReadU8(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as byte - keys must be strings");
        }
        return _parent.ReadU8(info, index);
    }
    
    public ushort ReadU16(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as ushort - keys must be strings");
        }
        return _parent.ReadU16(info, index);
    }
    
    public uint ReadU32(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as uint - keys must be strings");
        }
        return _parent.ReadU32(info, index);
    }
    
    public ulong ReadU64(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as ulong - keys must be strings");
        }
        return _parent.ReadU64(info, index);
    }
    
    public sbyte ReadI8(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as sbyte - keys must be strings");
        }
        return _parent.ReadI8(info, index);
    }
    
    public short ReadI16(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as short - keys must be strings");
        }
        return _parent.ReadI16(info, index);
    }
    
    public int ReadI32(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as int - keys must be strings");
        }
        return _parent.ReadI32(info, index);
    }
    
    public long ReadI64(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as long - keys must be strings");
        }
        return _parent.ReadI64(info, index);
    }
    
    public float ReadF32(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as float - keys must be strings");
        }
        return _parent.ReadF32(info, index);
    }
    
    public double ReadF64(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as double - keys must be strings");
        }
        return _parent.ReadF64(info, index);
    }
    
    public decimal ReadDecimal(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as decimal - keys must be strings");
        }
        return _parent.ReadDecimal(info, index);
    }
    
    public DateTime ReadDateTime(ISerdeInfo info, int index)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as DateTime - keys must be strings");
        }
        return _parent.ReadDateTime(info, index);
    }
    
    public void ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer)
    {
        if (_isKey)
        {
            throw new JsonException("Cannot read dictionary key as bytes - keys must be strings");
        }
        _parent.ReadBytes(info, index, writer);
    }
}

/// <summary>
/// Type deserializer for JSON arrays
/// </summary>
internal sealed class ArrayTypeDeserializer : ITypeDeserializer
{
    private readonly CanonicalJsonSerdeReader _parent;
    private bool _initialized;
    private int _currentIndex;
    
    public ArrayTypeDeserializer(CanonicalJsonSerdeReader parent)
    {
        _parent = parent;
    }

    public int? SizeOpt => null;
    
    public int TryReadIndex(ISerdeInfo info, out string? errorName)
    {
        errorName = null;
        
        if (!_initialized)
        {
            _initialized = true;
            _parent.StartArray(_parent._currentElement);
        }
        
        if (!_parent.TryGetNextArrayElement(out var element))
        {
            return ITypeDeserializer.EndOfType;
        }
        
        return _currentIndex++;
    }
    
    public T ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) where T : class?
    {
        return deserialize.Deserialize(_parent);
    }
    
    public void SkipValue(ISerdeInfo info, int index)
    {
        // No-op for JsonDocument approach
    }
    
    // All the primitive read methods - delegate to parent
    public bool ReadBool(ISerdeInfo info, int index) => _parent.ReadBool(info, index);
    public char ReadChar(ISerdeInfo info, int index) => _parent.ReadChar(info, index);
    public byte ReadU8(ISerdeInfo info, int index) => _parent.ReadU8(info, index);
    public ushort ReadU16(ISerdeInfo info, int index) => _parent.ReadU16(info, index);
    public uint ReadU32(ISerdeInfo info, int index) => _parent.ReadU32(info, index);
    public ulong ReadU64(ISerdeInfo info, int index) => _parent.ReadU64(info, index);
    public sbyte ReadI8(ISerdeInfo info, int index) => _parent.ReadI8(info, index);
    public short ReadI16(ISerdeInfo info, int index) => _parent.ReadI16(info, index);
    public int ReadI32(ISerdeInfo info, int index) => _parent.ReadI32(info, index);
    public long ReadI64(ISerdeInfo info, int index) => _parent.ReadI64(info, index);
    public float ReadF32(ISerdeInfo info, int index) => _parent.ReadF32(info, index);
    public double ReadF64(ISerdeInfo info, int index) => _parent.ReadF64(info, index);
    public decimal ReadDecimal(ISerdeInfo info, int index) => _parent.ReadDecimal(info, index);
    public string ReadString(ISerdeInfo info, int index) => _parent.ReadString(info, index);
    public DateTime ReadDateTime(ISerdeInfo info, int index) => _parent.ReadDateTime(info, index);
    public void ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer) => _parent.ReadBytes(info, index, writer);
}
