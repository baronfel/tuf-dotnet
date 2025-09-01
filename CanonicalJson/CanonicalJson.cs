using System.Text;
using System.Text.Encodings.Web;

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