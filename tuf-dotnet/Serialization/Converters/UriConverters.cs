using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using TUF.Models.Primitives;

namespace Tuf.DotNet.Serialization.Converters;
internal static class UriReader
{
    public static bool Read(ref Utf8JsonReader reader, bool isAbsolute, [NotNullWhen(true)] out Uri? uri)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException();
        var s = reader.GetString();
        if (s is null) throw new JsonException();
        uri = null;
        if (!isAbsolute && !s.StartsWith("/"))
        {
            s = "/" + s; // dotnet uri parsing needs something to hook on to for relative paths
        }
        var tempUri = new Uri(s, isAbsolute ? UriKind.Absolute : UriKind.Relative);
        if (isAbsolute && !tempUri.IsAbsoluteUri) return false;
        if (!isAbsolute && tempUri.IsAbsoluteUri) return false;
        uri = tempUri;
        return true;
    }
}

internal sealed class AbsoluteUriJsonConverter : JsonConverter<AbsoluteUri>
{
    public override AbsoluteUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (UriReader.Read(ref reader, true, out var uri))
        {
            return new AbsoluteUri(uri);
        }
        throw new JsonException("Expected an absolute URI");
    }

    public override void Write(Utf8JsonWriter writer, AbsoluteUri value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Uri.ToString());
    }
}

internal sealed class RelativeUriJsonConverter : JsonConverter<RelativeUri>
{
    public override RelativeUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (UriReader.Read(ref reader, false, out var uri))
        {
            return new RelativeUri(uri);
        }
        throw new JsonException("Expected a relative URI");
    }

    public override void Write(Utf8JsonWriter writer, RelativeUri value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Uri.ToString());
    }
}
