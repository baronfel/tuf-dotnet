using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Roles;

namespace TUF.Serialization.Converters;

/// <summary>
/// We can't directly annotate this on the <see cref="IRole{T}"/> instances because it'll be an infinite loop,
/// so instead we manually register them on the serialization context.
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class RoleTypeJsonConverter<T> : JsonConverter<T> where T : IRole<T>
{
    private JsonTypeInfo<T> _typeInfo => T.JsonTypeInfo(MetadataJsonContext.ConverterInternal);
    private readonly string _typeLabel = T.TypeLabel;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Parse the object into a JsonDocument
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected JSON object for role");

        if (!root.TryGetProperty("_type", out var typeProp)) throw new JsonException("Missing _type property");
        var incoming = typeProp.GetString();
        if (!string.Equals(incoming, _typeLabel, StringComparison.Ordinal))
        {
            throw new JsonException($"Unexpected _type value: '{incoming}', expected '{_typeLabel}'");
        }

        var result = JsonSerializer.Deserialize(root, _typeInfo);
        if (result is null) throw new JsonException("Failed to deserialize role object");
        return result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Serialize to JsonElement using generated type info
        var element = JsonSerializer.SerializeToElement(value, _typeInfo);

        writer.WriteStartObject();
        writer.WriteString("_type", _typeLabel);

        foreach (var prop in element.EnumerateObject())
        {
            // Avoid re-emitting _type if present
            if (string.Equals(prop.Name, "_type", StringComparison.Ordinal)) continue;
            writer.WritePropertyName(prop.Name);
            prop.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
