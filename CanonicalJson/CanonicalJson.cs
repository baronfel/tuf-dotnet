using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CanonicalJson;

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
    /// Convert an object to a detached JsonElement using the provided <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public static JsonElement ToJsonElement<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
        var utf8 = JsonSerializer.SerializeToUtf8Bytes(obj, typeInfo);
        using var doc = JsonDocument.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Serialize an object to canonical JSON as a UTF-8 encoded byte array using the provided serializer options.
    /// This allows custom converters to participate when creating the JSON prior to canonical emission.
    /// </summary>
    public static byte[] Serialize<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
        var element = ToJsonElement(obj, typeInfo);
        using var ms = new MemoryStream();
        var writer = new Utf8JsonWriter(ms);
        WriteElement(element, writer);
        writer.Flush();
        return ms.ToArray();
    }

    // Recursively write a JsonElement to the provided stream in canonical form.
    private static void WriteElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, writer);
                break;
            case JsonValueKind.Array:
                WriteArray(element, writer);
                break;
            case JsonValueKind.String:
                WriteString(element.GetString(), writer);
                break;
            case JsonValueKind.Number:
                WriteNumber(element, writer);
                break;
            case JsonValueKind.True:
                WriteRaw("true", writer);
                break;
            case JsonValueKind.False:
                WriteRaw("false", writer);
                break;
            case JsonValueKind.Null:
                WriteRaw("null", writer);
                break;
            default:
                throw new NotSupportedException($"Unsupported JsonValueKind: {element.ValueKind}");
        }
    }

    private static void WriteObject(JsonElement obj, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        var props = obj.EnumerateObject().ToArray();
        Array.Sort(props, (a, b) => CompareUtf8(a.Name, b.Name));

        for (int i = 0; i < props.Length; i++)
        {
            writer.WritePropertyName(props[i].Name);
            WriteElement(props[i].Value, writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(JsonElement arr, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in arr.EnumerateArray())
        {
            WriteElement(item, writer);
        }
        writer.WriteEndArray();
    }

    private static void WriteString(string? s, Utf8JsonWriter writer)
    {
        // Use System.Text.Json to produce a correctly escaped JSON string (including quotes)
        if (s is null)
        {
            writer.WriteRawValue("null");
            return;
        }

        // otherwise, canonicalize string by escaping only the backslash and double quote characters, and wrapping the escaped string in quotes
        var searchValues = SearchValues.Create(['\\', '\"']);
        var sourceSpan = s.AsSpan();
        var destSpan = new StringBuilder(capacity: s.Length);
        destSpan.Append('\"');
        var found = false;
        while (sourceSpan.IndexOfAny(searchValues) is int idx && idx != -1)
        {
            found = true;
            destSpan.Append(sourceSpan[..idx]);
            destSpan.Append('\\');
            destSpan.Append(sourceSpan[idx]);
            sourceSpan = sourceSpan[(idx + 1)..];
        }
        if (!found)
        {
            destSpan.Append(sourceSpan);
        }
        destSpan.Append('\"');

        writer.WriteRawValue(destSpan.ToString());
    }

    private static void WriteNumber(JsonElement element, Utf8JsonWriter writer)
    {
        // Prefer integral representations when possible
        if (element.TryGetInt64(out long l))
        {
            WriteRaw(l.ToString(CultureInfo.InvariantCulture), writer);
            return;
        }

        if (element.TryGetUInt64(out ulong ul))
        {
            WriteRaw(ul.ToString(CultureInfo.InvariantCulture), writer);
            return;
        }

        if (element.TryGetDecimal(out decimal dec))
        {
            // G29 yields a compact decimal representation for decimal values
            WriteRaw(dec.ToString("G29", CultureInfo.InvariantCulture), writer);
            return;
        }

        if (element.TryGetDouble(out double d))
        {
            // G17 yields a round-trip-safe representation for double
            WriteRaw(d.ToString("G17", CultureInfo.InvariantCulture), writer);
            return;
        }

        // Fallback: use the raw text as parsed
        WriteRaw(element.GetRawText(), writer);
    }

    private static void WriteRaw(string text, Utf8JsonWriter writer) => writer.WriteRawValue(text);

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