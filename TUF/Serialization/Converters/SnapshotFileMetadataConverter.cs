using System.Text.Json;

using TUF.Models.Primitives;
using TUF.Models.Roles.Timestamp;

namespace Tuf.DotNet.Serialization.Converters;

internal sealed class SnapshotFileMetadataJsonConverter : System.Text.Json.Serialization.JsonConverter<SnapshotFileMetadata>
{
    private const string AllowedKey = "snapshot.json";

    public override SnapshotFileMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected start of object for SnapshotFileMetadata");

        // Read first property name
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected a property name in SnapshotFileMetadata");

        var propName = reader.GetString();
        if (!string.Equals(propName, AllowedKey, StringComparison.Ordinal))
        {
            throw new JsonException($"Only '{AllowedKey}' is allowed as the key for SnapshotFileMetadata");
        }

        // Move to value
        if (!reader.Read()) throw new JsonException();

        // Deserialize the FileMetadata value using generated JsonTypeInfo to be AOT/trimming-safe
        var fileMeta = (FileMetadata)(JsonSerializer.Deserialize(ref reader, options.GetTypeInfo(typeof(FileMetadata))) ?? throw new JsonException("Invalid FileMetadata"));

        // After value, expect end object
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject) throw new JsonException("Expected end of object after SnapshotFileMetadata value");

        return new SnapshotFileMetadata(fileMeta);
    }

    public override void Write(Utf8JsonWriter writer, SnapshotFileMetadata value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        // There should be exactly one entry with key "snapshot.json"
        if (value.Count != 1) throw new JsonException("SnapshotFileMetadata must contain exactly one entry with key 'snapshot.json'");

        foreach (var kvp in value)
        {
            if (!string.Equals(kvp.Key.RelPath, AllowedKey, StringComparison.Ordinal))
            {
                throw new JsonException($"SnapshotFileMetadata may only contain key '{AllowedKey}'");
            }
            writer.WritePropertyName(AllowedKey);
            JsonSerializer.Serialize(writer, kvp.Value, options.GetTypeInfo(typeof(FileMetadata)));
        }

        writer.WriteEndObject();
    }
}