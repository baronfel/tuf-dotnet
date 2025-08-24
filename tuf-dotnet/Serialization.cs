namespace TUF.Serialization;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanonicalJson;
using TUF.Models;
using TUF.Models.Primitives;
using TUF.Models.Roles;

/// <summary>
/// Helper methods for serializing and deserializing Metadata<T> instances using the
/// TUF model converters defined below. These produce canonical output when required.
/// </summary>
public static class MetadataSerializer
{
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    // Expose the options for use by other serializers (e.g. Metadata.SignedBytes uses canonical serializer)
    public static JsonSerializerOptions JsonOptions => s_options;

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeCanonicalToUTF8Bytes<T>(Metadata<T> metadata) where T : IRole
    {
        return CanonicalJsonSerializer.Serialize(metadata, s_options);
    }

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters,
    /// but return a UTF-8 string instead of bytes.
    /// </summary>
    public static string SerializeCanonicalToUTF8String<T>(Metadata<T> metadata) where T : IRole
    {
        var bytes = SerializeCanonicalToUTF8Bytes(metadata);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from JSON bytes (canonical or not) using the custom converters.
    /// </summary>
    public static Metadata<T>? Deserialize<T>(byte[] utf8JsonBytes) where T : IRole
    {
        return JsonSerializer.Deserialize<Metadata<T>>(utf8JsonBytes, s_options);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from a UTF-8 encoded JSON string using the custom converters.
    /// </summary>
    public static Metadata<T>? DeserializeFromString<T>(string utf8JsonString) where T : IRole
    {
        return JsonSerializer.Deserialize<Metadata<T>>(utf8JsonString, s_options);
    }

    /// <summary>
    /// Produce UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeToUTF8Bytes<T>(Metadata<T> metadata) where T : IRole
    {
        using var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, metadata, s_options);
        return ms.ToArray();
    }

    /// <summary>
    /// Produce a UTF-8 string for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static string SerializeToString<T>(Metadata<T> metadata) where T : IRole
    {
        return JsonSerializer.Serialize(metadata, s_options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = false,
        };

        // Register converters for wrapper types
        options.Converters.Add(new RelativePathJsonConverter());
        options.Converters.Add(new KeyIdJsonConverter());
        options.Converters.Add(new HexDigestJsonConverter());
        options.Converters.Add(new PEMStringJsonConverter());
        options.Converters.Add(new HexStringJsonConverter());

    // Key converter - only allow the three well-known key types/schemes
    options.Converters.Add(new KeyJsonConverter());

        // URI converters
        options.Converters.Add(new AbsoluteUriJsonConverter());
        options.Converters.Add(new RelativeUriJsonConverter());

        // Add a factory that handles Dictionary<TKey, TValue> when TKey is RelativePath or KeyId
        options.Converters.Add(new DictionaryKeyConverterFactory());

        return options;
    }

    // Converter that only permits the three well-known key variants and enforces matching type+scheme
    internal sealed class KeyJsonConverter : JsonConverter<TUF.Models.Keys.Key>
    {
        public override TUF.Models.Keys.Key? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

            string? keytype = null;
            string? scheme = null;
            object? keyval = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
                var prop = reader.GetString();
                if (prop is null) throw new JsonException();
                reader.Read();
                switch (prop)
                {
                    case "keytype":
                        keytype = reader.GetString();
                        break;
                    case "scheme":
                        scheme = reader.GetString();
                        break;
                    case "keyval":
                    {
                        // Read the whole keyval object as JsonDocument then parse according to keytype/scheme
                        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                        using var doc = JsonDocument.ParseValue(ref reader);
                        var el = doc.RootElement;
                        if (keytype == null || scheme == null)
                        {
                            // we need keytype and scheme to determine the shape; but if not yet present, try to infer later
                            keyval = el.Clone();
                        }
                        else
                        {
                            keyval = ParseKeyVal(el, keytype, scheme, options);
                        }
                        break;
                    }
                    default:
                        // skip unknown
                        reader.Skip();
                        break;
                }
            }

            if (keytype is null || scheme is null) throw new JsonException();

            // If keyval was read earlier as JsonElement clone, parse now
            if (keyval is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                keyval = ParseKeyVal(je, keytype, scheme, options);
            }

            if (keyval is null) throw new JsonException();

            // Only allow the three well-known keys
            if (keytype == TUF.Models.Keys.Types.Rsa.Name && scheme == TUF.Models.Keys.Schemes.RSASSA_PSS_SHA256.Name)
            {
                return new TUF.Models.Keys.WellKnown.Rsa((TUF.Models.Keys.Values.RsaKeyValue)keyval);
            }
            else if (keytype == TUF.Models.Keys.Types.Ed25519.Name && scheme == TUF.Models.Keys.Schemes.Ed25519.Name)
            {
                return new TUF.Models.Keys.WellKnown.Ed25519((TUF.Models.Keys.Values.Ed25519KeyValue)keyval);
            }
            else if (keytype == TUF.Models.Keys.Types.Ecdsa.Name && scheme == TUF.Models.Keys.Schemes.ECDSA_SHA2_NISTP256.Name)
            {
                return new TUF.Models.Keys.WellKnown.Ecdsa((TUF.Models.Keys.Values.EcdsaKeyValue)keyval);
            }

            throw new JsonException("Unsupported or mismatched key type/scheme");
        }

        private static object ParseKeyVal(JsonElement el, string keytype, string scheme, JsonSerializerOptions options)
        {
            if (keytype == TUF.Models.Keys.Types.Rsa.Name && scheme == TUF.Models.Keys.Schemes.RSASSA_PSS_SHA256.Name)
            {
                // rsa keyval: { "public": "...", "private": "..."? }
                var val = JsonSerializer.Deserialize<TUF.Models.Keys.Values.RsaKeyValue>(el.GetRawText(), options);
                if (val is null) throw new JsonException();
                return val;
            }
            else if (keytype == TUF.Models.Keys.Types.Ed25519.Name && scheme == TUF.Models.Keys.Schemes.Ed25519.Name)
            {
                var val = JsonSerializer.Deserialize<TUF.Models.Keys.Values.Ed25519KeyValue>(el.GetRawText(), options);
                if (val is null) throw new JsonException();
                return val;
            }
            else if (keytype == TUF.Models.Keys.Types.Ecdsa.Name && scheme == TUF.Models.Keys.Schemes.ECDSA_SHA2_NISTP256.Name)
            {
                var val = JsonSerializer.Deserialize<TUF.Models.Keys.Values.EcdsaKeyValue>(el.GetRawText(), options);
                if (val is null) throw new JsonException();
                return val;
            }

            throw new JsonException("Unsupported keyval shape for given type/scheme");
        }

        public override void Write(Utf8JsonWriter writer, TUF.Models.Keys.Key value, JsonSerializerOptions options)
        {
            // Accept only well-known keys and ensure KeyType and Scheme match
            writer.WriteStartObject();
            writer.WriteString("keytype", value.KeyType);
            writer.WriteString("scheme", value.Scheme);

            switch (value)
            {
                case TUF.Models.Keys.WellKnown.Rsa rsa:
                    // verify type/scheme
                    if (rsa.KeyType != TUF.Models.Keys.Types.Rsa.Name || rsa.Scheme != TUF.Models.Keys.Schemes.RSASSA_PSS_SHA256.Name)
                        throw new JsonException("Mismatched key type/scheme for RSA");
                    writer.WritePropertyName("keyval");
                    JsonSerializer.Serialize(writer, rsa.TypedKeyVal, typeof(TUF.Models.Keys.Values.RsaKeyValue), options);
                    break;
                case TUF.Models.Keys.WellKnown.Ed25519 ed:
                    if (ed.KeyType != TUF.Models.Keys.Types.Ed25519.Name || ed.Scheme != TUF.Models.Keys.Schemes.Ed25519.Name)
                        throw new JsonException("Mismatched key type/scheme for Ed25519");
                    writer.WritePropertyName("keyval");
                    JsonSerializer.Serialize(writer, ed.TypedKeyVal, typeof(TUF.Models.Keys.Values.Ed25519KeyValue), options);
                    break;
                case TUF.Models.Keys.WellKnown.Ecdsa ecd:
                    if (ecd.KeyType != TUF.Models.Keys.Types.Ecdsa.Name || ecd.Scheme != TUF.Models.Keys.Schemes.ECDSA_SHA2_NISTP256.Name)
                        throw new JsonException("Mismatched key type/scheme for ECDSA");
                    writer.WritePropertyName("keyval");
                    JsonSerializer.Serialize(writer, ecd.TypedKeyVal, typeof(TUF.Models.Keys.Values.EcdsaKeyValue), options);
                    break;
                default:
                    throw new JsonException("Only well-known keys may be serialized");
            }

            writer.WriteEndObject();
        }
    }

    // Explicit extractors to avoid broad reflection.
    private static string HexDigestToString(HexDigest digest) => digest.sha256HexDigest;
    private static string KeyIdToString(KeyId keyId) => HexDigestToString(keyId.digest);
    private static string PEMStringToString(PEMString pem) => pem.PemEncodedValue;
    private static string HexStringToString(HexString hs) => hs.HexEncodedValue;

    // Converters
    internal sealed class RelativePathJsonConverter : JsonConverter<RelativePath>
    {
        public override RelativePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new RelativePath(s);
        }

        public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.RelPath);
        }
    }

    internal sealed class KeyIdJsonConverter : JsonConverter<KeyId>
    {
        public override KeyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new KeyId(new HexDigest(s));
        }

        public override void Write(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options)
        {
            var hex = KeyIdToString(value);
            writer.WriteStringValue(hex);
        }
    }

    internal sealed class HexDigestJsonConverter : JsonConverter<HexDigest>
    {
        public override HexDigest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new HexDigest(s);
        }

        public override void Write(Utf8JsonWriter writer, HexDigest value, JsonSerializerOptions options)
        {
            var s = HexDigestToString(value);
            writer.WriteStringValue(s);
        }
    }

    internal sealed class PEMStringJsonConverter : JsonConverter<PEMString>
    {
        public override PEMString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new PEMString(s);
        }

        public override void Write(Utf8JsonWriter writer, PEMString value, JsonSerializerOptions options)
        {
            var s = PEMStringToString(value);
            writer.WriteStringValue(s);
        }
    }

    internal sealed class HexStringJsonConverter : JsonConverter<HexString>
    {
        public override HexString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new HexString(s);
        }

        public override void Write(Utf8JsonWriter writer, HexString value, JsonSerializerOptions options)
        {
            var s = HexStringToString(value);
            writer.WriteStringValue(s);
        }
    }

    internal sealed class AbsoluteUriJsonConverter : JsonConverter<AbsoluteUri>
    {
        public override AbsoluteUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new AbsoluteUri(s);
        }

        public override void Write(Utf8JsonWriter writer, AbsoluteUri value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class RelativeUriJsonConverter : JsonConverter<RelativeUri>
    {
        public override RelativeUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var s = reader.GetString();
            if (s is null) throw new JsonException();
            return new RelativeUri(s);
        }

        public override void Write(Utf8JsonWriter writer, RelativeUri value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    // A JsonConverterFactory that creates a converter for Dictionary<TKey, TValue> when TKey is RelativePath or KeyId.
    internal sealed class DictionaryKeyConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType) return false;
            var def = typeToConvert.GetGenericTypeDefinition();
            if (def != typeof(Dictionary<,>)) return false;
            var keyType = typeToConvert.GetGenericArguments()[0];
            return keyType == typeof(RelativePath) || keyType == typeof(KeyId);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var keyType = typeToConvert.GetGenericArguments()[0];
            var valueType = typeToConvert.GetGenericArguments()[1];
            var convType = typeof(DictionaryKeyConverter<,>).MakeGenericType(keyType, valueType);
            return (JsonConverter)Activator.CreateInstance(convType, new object[] { options })!;
        }
    }

    internal sealed class DictionaryKeyConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>>
        where TKey : notnull
    {
        private readonly Type _valueType;
        private readonly JsonSerializerOptions _options;

        public DictionaryKeyConverter(JsonSerializerOptions? options)
        {
            _valueType = typeof(TValue);
            _options = options ?? new JsonSerializerOptions();
        }

        public override Dictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
            var dict = new Dictionary<TKey, TValue>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
                var propertyName = reader.GetString();
                if (propertyName is null) throw new JsonException();

                TKey key;
                if (typeof(TKey) == typeof(RelativePath))
                {
                    key = (TKey)(object)new RelativePath(propertyName);
                }
                else if (typeof(TKey) == typeof(KeyId))
                {
                    key = (TKey)(object)new KeyId(new HexDigest(propertyName));
                }
                else
                {
                    key = (TKey)Convert.ChangeType(propertyName, typeof(TKey));
                }

                reader.Read(); // move to value
                var value = (TValue)JsonSerializer.Deserialize(ref reader, _valueType, _options)!;
                dict.Add(key, value);
            }
            return dict;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kv in value)
            {
                string propertyName;
                if (typeof(TKey) == typeof(RelativePath))
                {
                    propertyName = ((RelativePath)(object)kv.Key!).RelPath;
                }
                else if (typeof(TKey) == typeof(KeyId))
                {
                    propertyName = KeyIdToString((KeyId)(object)kv.Key!);
                }
                else
                {
                    propertyName = kv.Key?.ToString() ?? string.Empty;
                }

                writer.WritePropertyName(propertyName);
                JsonSerializer.Serialize(writer, kv.Value, _valueType, _options);
            }
            writer.WriteEndObject();
        }
    }
}

