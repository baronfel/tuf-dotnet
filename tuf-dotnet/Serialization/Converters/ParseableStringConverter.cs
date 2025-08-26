using System.Text.Json;
using System.Text.Json.Serialization;

using TUF.Serialization;

namespace Tuf.DotNet.Serialization.Converters;

internal class ParseableStringConverter<T> : JsonConverter<T> where T: IParsable<T>, IJsonStringWriteable<T>
{
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToJsonString());
    }

    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected token type {JsonTokenType.PropertyName} but got {reader.TokenType}");
        }

        var str = reader.GetString() ?? throw new JsonException("Expected a string property name, but got null");

        if (!T.TryParse(str, null, out var result))
        {
            throw new JsonException($"Could not parse '{str}' as {typeof(T).Name}");
        }

        return result;
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToJsonString());
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected token type {JsonTokenType.String} but got {reader.TokenType}");
        }

        var str = reader.GetString();
        if (str is null)
        {
            return default;
        }

        if (!T.TryParse(str, null, out var result))
        {
            throw new JsonException($"Could not parse '{str}' as {typeof(T).Name}");
        }

        return result;
    }
}