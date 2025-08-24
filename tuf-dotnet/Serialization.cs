namespace tuf_dotnet.Serialization;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using tuf_dotnet.Models;
using static tuf_dotnet.Models.Primitives;

/// <summary>
/// The CanonicalJsonSerializer serializes objects to a canonical JSON format
/// following the Canonical JSON reference (see http://wiki.laptop.org/go/Canonical_JSON).
/// This implementation:
/// - converts a generic object of type T to a System.Text.Json.JsonElement
/// - walks the JsonElement and emits a canonical UTF-8 encoded byte[]
///
/// Notes:
/// - Object properties are emitted in lexicographic order by UTF-8 byte sequence.
/// - No insignificant whitespace is emitted.
/// - Strings are emitted using System.Text.Json escaping (including surrounding quotes).
/// - Numbers attempt to use a shortest, round-trip-safe representation.
/// </summary>
public static class CanonicalJsonSerializer
{
    /// <summary>
    /// Convert an object to a detached JsonElement using default serializer options.
    /// The returned JsonElement is safe to use after the internal JsonDocument is disposed.
    /// </summary>
    public static JsonElement ToJsonElement<T>(T obj)
        => ToJsonElement(obj, options: null);

    /// <summary>
    /// Convert an object to a detached JsonElement using the provided <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public static JsonElement ToJsonElement<T>(T obj, JsonSerializerOptions? options)
    {
        var utf8 = JsonSerializer.SerializeToUtf8Bytes(obj, options);
        using var doc = JsonDocument.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Serialize an object to canonical JSON as a UTF-8 encoded byte array using default serializer options.
    /// </summary>
    public static byte[] Serialize<T>(T obj)
        => Serialize(obj, options: null);

    /// <summary>
    /// Serialize an object to canonical JSON as a UTF-8 encoded byte array using the provided serializer options.
    /// This allows custom converters to participate when creating the JSON prior to canonical emission.
    /// </summary>
    public static byte[] Serialize<T>(T obj, JsonSerializerOptions? options)
    {
        var element = ToJsonElement(obj, options);
        using var ms = new MemoryStream();
        WriteElement(element, ms);
        return ms.ToArray();
    }

    // Recursively write a JsonElement to the provided stream in canonical form.
    private static void WriteElement(JsonElement element, Stream stream)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, stream);
                break;
            case JsonValueKind.Array:
                WriteArray(element, stream);
                break;
            case JsonValueKind.String:
                WriteString(element.GetString(), stream);
                break;
            case JsonValueKind.Number:
                WriteNumber(element, stream);
                break;
            case JsonValueKind.True:
                WriteRaw("true", stream);
                break;
            case JsonValueKind.False:
                WriteRaw("false", stream);
                break;
            case JsonValueKind.Null:
                WriteRaw("null", stream);
                break;
            default:
                throw new NotSupportedException($"Unsupported JsonValueKind: {element.ValueKind}");
        }
    }

    private static void WriteObject(JsonElement obj, Stream stream)
    {
        stream.WriteByte((byte)'{');
        var props = obj.EnumerateObject().ToArray();
        Array.Sort(props, (a, b) => CompareUtf8(a.Name, b.Name));

        for (int i = 0; i < props.Length; i++)
        {
            if (i > 0) stream.WriteByte((byte)',');
            WriteString(props[i].Name, stream);
            stream.WriteByte((byte)':');
            WriteElement(props[i].Value, stream);
        }

        stream.WriteByte((byte)'}');
    }

    private static void WriteArray(JsonElement arr, Stream stream)
    {
        stream.WriteByte((byte)'[');
        bool first = true;
        foreach (var item in arr.EnumerateArray())
        {
            if (!first) stream.WriteByte((byte)',');
            first = false;
            WriteElement(item, stream);
        }
        stream.WriteByte((byte)']');
    }

    private static void WriteString(string? s, Stream stream)
    {
        // Use System.Text.Json to produce a correctly escaped JSON string (including quotes)
        if (s is null)
        {
            WriteRaw("null", stream);
            return;
        }

        // JsonSerializer.Serialize will emit the surrounding quotes and proper escaping.
        var serialized = JsonSerializer.Serialize(s);
        var bytes = Encoding.UTF8.GetBytes(serialized);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteNumber(JsonElement element, Stream stream)
    {
        // Prefer integral representations when possible
        if (element.TryGetInt64(out long l))
        {
            WriteRaw(l.ToString(CultureInfo.InvariantCulture), stream);
            return;
        }

        if (element.TryGetUInt64(out ulong ul))
        {
            WriteRaw(ul.ToString(CultureInfo.InvariantCulture), stream);
            return;
        }

        if (element.TryGetDecimal(out decimal dec))
        {
            // G29 yields a compact decimal representation for decimal values
            WriteRaw(dec.ToString("G29", CultureInfo.InvariantCulture), stream);
            return;
        }

        if (element.TryGetDouble(out double d))
        {
            // G17 yields a round-trip-safe representation for double
            WriteRaw(d.ToString("G17", CultureInfo.InvariantCulture), stream);
            return;
        }

        // Fallback: use the raw text as parsed
        WriteRaw(element.GetRawText(), stream);
    }

    private static void WriteRaw(string text, Stream stream)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    // Compare two strings by their UTF-8 byte sequence (lexicographic order). This
    // matches the Canonical JSON requirement to sort object keys by their UTF-8
    // byte sequences.
    private static int CompareUtf8(string a, string b)
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


// --- Converters and helpers for TUF model types ---

internal static class TufJsonConverters
{
    private static readonly JsonSerializerOptions s_options = BuildOptions();

    public static JsonSerializerOptions CreateOptions() => s_options;

    private static JsonSerializerOptions BuildOptions()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            // Preserve default encoder behavior (do not force escaping non-ascii),
            // we will canonicalize bytes separately.
            WriteIndented = false
        };

        // Register converters for custom model types
        opts.Converters.Add(new KeyValuesPemStringConverter());
        opts.Converters.Add(new KeyValuesHexStringConverter());
        opts.Converters.Add(new KeysKeyIdConverter());
        opts.Converters.Add(new RolesRelativePathConverter());
        opts.Converters.Add(new KeysIKeyConverter());
        opts.Converters.Add(new RolesFileMetadataConverter());

        return opts;
    }

    // PEM string wrapper converter
    private sealed class KeyValuesPemStringConverter : JsonConverter<KeyValues.PEMString>
    {
        public override KeyValues.PEMString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            return new KeyValues.PEMString(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, KeyValues.PEMString value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.PemEncodedValue);
        }
    }

    // Hex string wrapper converter
    private sealed class KeyValuesHexStringConverter : JsonConverter<KeyValues.HexString>
    {
        public override KeyValues.HexString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            return new KeyValues.HexString(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, KeyValues.HexString value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.HexEncodedValue);
        }
    }

    // KeyId converter: treat as a plain string for both property names and values
    private sealed class KeysKeyIdConverter : JsonConverter<KeyId>
    {
        public override KeyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            return new KeyId(new(reader.GetString()!));
        }

        public override void Write(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.digest.sha256HexDigest);
        }

        public override KeyId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Property name is already a string
            return new KeyId(new(reader.GetString()!));
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.digest.sha256HexDigest);
        }
    }

    // RelativePath converter: property-name-friendly string
    private sealed class RolesRelativePathConverter : JsonConverter<RelativePath>
    {
        public override RelativePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            return new RelativePath(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.RelPath);
        }

        public override RelativePath ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new RelativePath(reader.GetString()!);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.RelPath);
        }
    }

    // Converter for Keys.IKey - serializes/deserializes the object with fields: keytype, scheme, keyval { public: ... }
    private sealed class KeysIKeyConverter : JsonConverter<Keys.IKey>
    {
        public override Keys.IKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("keytype", out var keytypeProp)) throw new JsonException("Missing 'keytype' in key object");
            if (!root.TryGetProperty("scheme", out var schemeProp)) throw new JsonException("Missing 'scheme' in key object");
            if (!root.TryGetProperty("keyval", out var keyvalProp)) throw new JsonException("Missing 'keyval' in key object");

            var keytype = keytypeProp.GetString()!;
            var scheme = schemeProp.GetString()!;

            // keyval is an object with a 'public' field
            if (!keyvalProp.TryGetProperty("public", out var publicProp)) throw new JsonException("Missing 'public' in keyval");
            var pubString = publicProp.GetString() ?? string.Empty;

            // Try to match well-known (keytype, scheme) pairs first
            if (keytype == "rsa" && scheme == KeySchemes.RSASSA_PSS_SHA256.Name)
            {
                return new Keys.WellKnown.Rsa(new KeyValues.RsaKeyValue(new KeyValues.PEMString(pubString)));
            }

            if (keytype == "ecdsa" && scheme == KeySchemes.ECDSA_SHA2_NISTP256.Name)
            {
                return new Keys.WellKnown.Ecdsa(new KeyValues.EcdsaKeyValue(new KeyValues.PEMString(pubString)));
            }

            if (keytype == "ed25519" && scheme == KeySchemes.Ed25519.Name)
            {
                return new Keys.WellKnown.Ed25519(new KeyValues.Ed25519KeyValue(new KeyValues.HexString(pubString)));
            }

            // If scheme does not match well-known values, fall back to the generic Key record with raw keyval object
            var keyValObj = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["public"] = pubString
            };

            return new Keys.Key(keytype, scheme, keyValObj);
        }

        public override void Write(Utf8JsonWriter writer, Keys.IKey value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("keytype", value.KeyType);
            writer.WriteString("scheme", value.Scheme);
            writer.WritePropertyName("keyval");
            writer.WriteStartObject();

            switch (value)
            {
                case Keys.WellKnown.Rsa rsa:
                    // rsa.Public is KeyValues.RsaKeyValue; its Public property is PEMString
                    writer.WriteString("public", rsa.Public.Public.PemEncodedValue);
                    break;
                case Keys.WellKnown.Ecdsa ecdsa:
                    writer.WriteString("public", ecdsa.Public.Public.PemEncodedValue);
                    break;
                case Keys.WellKnown.Ed25519 ed:
                    writer.WriteString("public", ed.Public.Public.HexEncodedValue);
                    break;
                default:
                    // Attempt to fallback by reflecting on IKey.KeyVal if possible
                    var keyvalObj = value.KeyVal;
                    if (keyvalObj is KeyValues.RsaKeyValue rk)
                    {
                        writer.WriteString("public", rk.Public.PemEncodedValue);
                    }
                    else if (keyvalObj is KeyValues.EcdsaKeyValue ek)
                    {
                        writer.WriteString("public", ek.Public.PemEncodedValue);
                    }
                    else if (keyvalObj is KeyValues.Ed25519KeyValue hk)
                    {
                        writer.WriteString("public", hk.Public.HexEncodedValue);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported runtime key value type: {keyvalObj?.GetType().FullName}");
                    }
                    break;
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }

    // Converter for Roles.FileMetadata - maps the List<DigestAlgorithms.DigestValue> to an object under 'hashes'
    private sealed class RolesFileMetadataConverter : JsonConverter<Roles.FileMetadata>
    {
        public override Roles.FileMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

            uint version = 0;
            uint? length = null;
            List<DigestAlgorithms.DigestValue>? hashes = null;

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            foreach (var prop in root.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case "version":
                        version = prop.Value.GetUInt32();
                        break;
                    case "length":
                        length = prop.Value.GetUInt32();
                        break;
                    case "hashes":
                        if (prop.Value.ValueKind != JsonValueKind.Object) throw new JsonException("Expected 'hashes' to be an object");
                        hashes = new List<DigestAlgorithms.DigestValue>();
                        foreach (var h in prop.Value.EnumerateObject())
                        {
                            hashes.Add(new DigestAlgorithms.DigestValue(h.Name, h.Value.GetString()!));
                        }
                        break;
                    default:
                        // ignore unknown fields
                        break;
                }
            }

            return new Roles.FileMetadata(version, length, hashes);
        }

        public override void Write(Utf8JsonWriter writer, Roles.FileMetadata value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", value.Version);
            if (value.Length.HasValue)
            {
                writer.WriteNumber("length", value.Length.Value);
            }

            if (value.Hashes is { Count: > 0 })
            {
                writer.WritePropertyName("hashes");
                writer.WriteStartObject();
                foreach (var h in value.Hashes)
                {
                    writer.WriteString(h.Algorithm, h.HexEncodedValue);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }
}

/// <summary>
/// Helper methods for serializing and deserializing Metadata<T> instances using the
/// TUF model converters defined above. These produce canonical output when required.
/// </summary>
public static class MetadataSerializer
{
    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeCanonical<T>(Models.Metadata<T> metadata) where T : Roles.IRole
    {
        var opts = TufJsonConverters.CreateOptions();
        return CanonicalJsonSerializer.Serialize(metadata, opts);
    }

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters,
    /// but return a UTF-8 string instead of bytes.
    /// </summary>
    public static string SerializeCanonicalToString<T>(Models.Metadata<T> metadata) where T : Roles.IRole
    {
        var bytes = SerializeCanonical(metadata);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from JSON bytes (canonical or not) using the custom converters.
    /// </summary>
    public static Models.Metadata<T>? Deserialize<T>(byte[] jsonBytes) where T : Roles.IRole
    {
        var opts = TufJsonConverters.CreateOptions();
        return JsonSerializer.Deserialize<Models.Metadata<T>>(jsonBytes, opts);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from a UTF-8 encoded JSON string using the custom converters.
    /// </summary>
    public static Models.Metadata<T>? DeserializeFromString<T>(string json) where T : Roles.IRole
    {
        var opts = TufJsonConverters.CreateOptions();
        return JsonSerializer.Deserialize<Models.Metadata<T>>(json, opts);
    }
}

