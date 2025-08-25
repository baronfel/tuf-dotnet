using System.Text.Json;
using System.Text.Json.Serialization;

using TUF.Models.Primitives;

namespace Tuf.DotNet.Serialization.Converters;

internal class RelativePathJsonConverter : JsonConverter<RelativePath>
{
    private static RelativePath Create(string? s) => string.IsNullOrEmpty(s) ? throw new JsonException("Invalid relative path") : new RelativePath(s);
    public override RelativePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException();
        var s = reader.GetString();
        return Create(s);
    }

    public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.RelPath);
    }

    public override RelativePath ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return Create(s);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.RelPath);
    }
}