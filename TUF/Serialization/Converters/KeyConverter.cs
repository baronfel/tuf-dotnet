using System.Text.Json;
using System.Text.Json.Serialization;

using TUF.Models.Keys;

namespace TUF.Serialization.Converters;

internal sealed class KeyConverter : JsonConverter<TUF.Models.Keys.IKey>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(TUF.Models.Keys.IKey)
            || typeToConvert == typeof(TUF.Models.Keys.WellKnown.Ecdsa)
            || typeToConvert == typeof(TUF.Models.Keys.WellKnown.Ed25519)
            || typeToConvert == typeof(TUF.Models.Keys.WellKnown.Rsa);
    }
    public override TUF.Models.Keys.IKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected JSON object for key");

        if (!root.TryGetProperty("keytype", out var keytypeProp)) throw new JsonException("Missing 'keytype' property");
        if (!root.TryGetProperty("scheme", out var schemeProp)) throw new JsonException("Missing 'scheme' property");

        var keytype = keytypeProp.GetString() ?? throw new JsonException("Null 'keytype'");
        var scheme = schemeProp.GetString() ?? throw new JsonException("Null 'scheme'");

        // RSA
        if (string.Equals(keytype, TUF.Models.Keys.Types.Rsa.Name, StringComparison.Ordinal) &&
            string.Equals(scheme, TUF.Models.Keys.Schemes.RSASSA_PSS_SHA256.Name, StringComparison.Ordinal))
        {
            var ti = TUF.Models.Keys.WellKnown.Rsa.JsonTypeInfo(MetadataJsonContext.ConverterInternal);
            var res = JsonSerializer.Deserialize<TUF.Models.Keys.WellKnown.Rsa>(root, ti);
            if (res is null) throw new JsonException("Failed to deserialize Rsa key");
            return res;
        }

        // ECDSA
        if (string.Equals(keytype, TUF.Models.Keys.Types.Ecdsa.Name, StringComparison.Ordinal) &&
            string.Equals(scheme, TUF.Models.Keys.Schemes.ECDSA_SHA2_NISTP256.Name, StringComparison.Ordinal))
        {
            if (!root.TryGetProperty("keyval", out var keyvalProp)) throw new JsonException("Missing 'keyval' property");

            var keyvalTi = MetadataJsonContext.ConverterInternal.EcdsaKeyValue;
            var keyval = JsonSerializer.Deserialize<TUF.Models.Keys.Values.EcdsaKeyValue>(keyvalProp, keyvalTi);

            if (keyval is null) throw new JsonException("Failed to deserialize EcdsaKeyValue");

            var res = new TUF.Models.Keys.WellKnown.Ecdsa(keyval);
            return res;
        }

        // Ed25519
        if (string.Equals(keytype, TUF.Models.Keys.Types.Ed25519.Name, StringComparison.Ordinal) &&
            string.Equals(scheme, TUF.Models.Keys.Schemes.Ed25519.Name, StringComparison.Ordinal))
        {
            var ti = TUF.Models.Keys.WellKnown.Ed25519.JsonTypeInfo(MetadataJsonContext.ConverterInternal);
            var res = JsonSerializer.Deserialize<TUF.Models.Keys.WellKnown.Ed25519>(root, ti);
            if (res is null) throw new JsonException("Failed to deserialize Ed25519 key");
            return res;
        }

        throw new JsonException($"Unrecognized keytype/scheme pair: '{keytype}' / '{scheme}'");
    }

    public override void Write(Utf8JsonWriter writer, TUF.Models.Keys.IKey value, JsonSerializerOptions options)
    {
        JsonElement element;

        if (value is TUF.Models.Keys.WellKnown.Rsa rsa)
        {
            var ti = TUF.Models.Keys.WellKnown.Rsa.JsonTypeInfo(MetadataJsonContext.ConverterInternal);
            element = JsonSerializer.SerializeToElement(rsa, ti);
            ValidateKeyElement<TUF.Models.Keys.Types.Rsa, TUF.Models.Keys.Schemes.RSASSA_PSS_SHA256>(element);
        }
        else if (value is TUF.Models.Keys.WellKnown.Ecdsa ecdsa)
        {
            var ti = TUF.Models.Keys.WellKnown.Ecdsa.JsonTypeInfo(MetadataJsonContext.ConverterInternal);
            element = JsonSerializer.SerializeToElement(ecdsa, ti);
            ValidateKeyElement<TUF.Models.Keys.Types.Ecdsa, TUF.Models.Keys.Schemes.ECDSA_SHA2_NISTP256>(element);
        }
        else if (value is TUF.Models.Keys.WellKnown.Ed25519 ed)
        {
            var ti = TUF.Models.Keys.WellKnown.Ed25519.JsonTypeInfo(MetadataJsonContext.ConverterInternal);
            element = JsonSerializer.SerializeToElement(ed, ti);
            ValidateKeyElement<TUF.Models.Keys.Types.Ed25519, TUF.Models.Keys.Schemes.Ed25519>(element);
        }
        else
        {
            throw new JsonException($"Unsupported key runtime type: {value.GetType().FullName}");
        }

        element.WriteTo(writer);
    }

    private static void ValidateKeyElement<TKeyType, TScheme>(JsonElement element)
        where TKeyType : TUF.Models.Keys.Types.IKeyType<TKeyType>
        where TScheme : TUF.Models.Keys.Schemes.IKeyScheme<TScheme>
    {
        if (!element.TryGetProperty("keytype", out var kt) || kt.GetString() != TKeyType.Name)
        {
            throw new JsonException($"Serialized key has unexpected 'keytype' (expected '{TKeyType.Name}')");
        }
        if (!element.TryGetProperty("scheme", out var sc) || sc.GetString() != TScheme.Name)
        {
            throw new JsonException($"Serialized key has unexpected 'scheme' (expected '{TScheme.Name}')");
        }
    }
}
