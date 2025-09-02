using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

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
public static class Serializer
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
        var serializer = new CanonicalJsonSerializer(ms);
        TProvider.Instance.Serialize(obj, serializer);
        serializer.Dispose();
        return ms.ToArray();
    }

    /// <summary>
    /// Serialize an object to canonical JSON using Serde.NET's serialization infrastructure.
    /// This provides explicit, low-level control of the JSON output format.
    /// </summary>
    public static T Deserialize<T>(string content) where T : IDeserializeProvider<T> => Deserialize<T, T>(content);

    /// <summary>
    /// Deserialize an object from canonical JSON using Serde.NET's deserialization infrastructure.
    /// This provides explicit, low-level control of the JSON input format.
    /// </summary>
    public static T Deserialize<T, TProvider>(string content) where TProvider : IDeserializeProvider<T>
    {
        using var deserializer = new CanonicalJsonSerdeReader(content);
        var result = TProvider.Instance.Deserialize(deserializer);
        return result;
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

public static class Proxies
{
    public class CanonicalDateTimeOffsetProxy : ISerdeProvider<DateTimeOffset>, ISerde<DateTimeOffset>
    {
        public static string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ssZ";
        public static ISerialize<DateTimeOffset> Instance => new CanonicalDateTimeOffsetProxy();

        static IDeserialize<DateTimeOffset> IDeserializeProvider<DateTimeOffset>.Instance => new CanonicalDateTimeOffsetProxy();

        public ISerdeInfo SerdeInfo => Serde.SerdeInfo.MakePrimitive("expires", PrimitiveKind.DateTime);

        public DateTimeOffset Deserialize(IDeserializer deserializer)
        {
            var dateString = deserializer.ReadString();
            return DateTimeOffset.ParseExact(dateString, DateTimeOffsetFormat, CultureInfo.InvariantCulture);
        }

        public void Serialize(DateTimeOffset value, ISerializer serializer)
        {
            serializer.WriteString(value.ToString(DateTimeOffsetFormat, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Serde proxy for System.Text.Json.Nodes.JsonObject that preserves the canonical JSON structure.
    /// Handles serialization and deserialization of custom metadata in target files.
    /// Preserves actual JSON types (string, number, boolean, etc.) without converting to strings.
    /// </summary>
    public class JsonObjectProxy : ISerdeProvider<JsonObject?>, ISerde<JsonObject?>
    {
        public static ISerialize<JsonObject?> Instance => new JsonObjectProxy();

        static IDeserialize<JsonObject?> IDeserializeProvider<JsonObject?>.Instance => new JsonObjectProxy();

        public ISerdeInfo SerdeInfo => StringProxy.SerdeInfo;

        public JsonObject? Deserialize(IDeserializer deserializer)
        {
            // Read the JSON as a string and parse it back to JsonObject
            var jsonString = StringProxy.Instance.Deserialize(deserializer);
            
            if (jsonString is null)
            {
                return null;
            }
            
            // Parse the JSON string back to JsonObject, preserving all original types
            return JsonNode.Parse(jsonString)?.AsObject();
        }

        public void Serialize(JsonObject? value, ISerializer serializer)
        {
            if (value is null)
            {
                serializer.WriteNull();
                return;
            }

            // Serialize the JsonObject as a JSON string, preserving all original JSON types
            var jsonString = value.ToJsonString();
            ((ISerialize<string>)StringProxy.Instance).Serialize(jsonString, serializer);
        }
    }
}