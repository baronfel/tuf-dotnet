using System.Text.Json;
using System.Text.Json.Serialization;

using TUF.Models.Primitives;
using TUF.Models.Roles.Targets;
using TUF.Serialization;

namespace Tuf.DotNet.Serialization.Converters;

public class ArrayToDictionaryConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>>
    where TValue : IAOTSerializable<TValue>, IKeyHolder<TValue, TKey>
    where TKey : notnull
{
    public override Dictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        var dict = new Dictionary<TKey, TValue>();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Read array of elements
            using var arrayDoc = JsonDocument.ParseValue(ref reader);
            var root = arrayDoc.RootElement;
            if (root.ValueKind != JsonValueKind.Array) throw new JsonException("Expected JSON array");
            foreach (var elem in root.EnumerateArray())
            {
                if (elem.ValueKind != JsonValueKind.Object) throw new JsonException("Array elements must be objects");
                var value = JsonSerializer.Deserialize(elem.GetRawText(), TValue.JsonTypeInfo(MetadataJsonContext.DefaultWithAddedOptions));
                if (value is null) throw new JsonException($"Failed to deserialize {typeof(TValue)}");
                var key = TValue.GetKey(value);
                dict[key] = value;
            }
            return dict;
        }
        throw new JsonException("Expected start of object or array for dictionary value");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        // Serialize as an array of objects where each element contains the key property
        writer.WriteStartArray();
        foreach (var kvp in value)
        {
            // Serialize value to JsonElement
            var elem = JsonSerializer.SerializeToElement(kvp.Value, TValue.JsonTypeInfo(MetadataJsonContext.DefaultWithAddedOptions));
            if (elem.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("TValue must serialize to a JSON object");
            }
            elem.WriteTo(writer);
        }
        writer.WriteEndArray();
    }
}