using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TUF.Models.Primitives;

public record SemanticVersion(string SemVer);

[JsonConverter(typeof(RelativePathJsonConverter))]
public record struct RelativePath(string RelPath);

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

[JsonConverter(typeof(AbsoluteUriJsonConverter))]
public record struct AbsoluteUri(Uri Uri)
{
    public static AbsoluteUri From([StringSyntax("uri")] string absoluteUri) => new(new Uri(absoluteUri, UriKind.Absolute));
}
[JsonConverter(typeof(RelativeUriJsonConverter))]
public record struct RelativeUri(Uri Uri)
{
    public static RelativeUri From([StringSyntax("uri")] string relativeUri) => new(new Uri(relativeUri, UriKind.Relative));
}

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

/// <summary>
/// A hex-encoded signature of the canonical form of a metadata object
/// </summary>
public record struct Signature();

/// <summary>
/// A wrapper around a value that has been hex-encoded - that is, its SHA-256 hash has been computed and converted into a 
/// hexadecimal string.
/// </summary>
/// <param name="sha256HexDigest"></param>
public record struct HexDigest(string sha256HexDigest);

/// <summary>
/// A wrapper around a string that represents a Unix-shell-style file path pattern.
/// This is always a relative path. It may support *- or ?-based wildcards.
/// It SHOULD always use the forward slash (/) as the path separator.
/// It SHOULD NOT start with a directory separator.
/// Implementation note: A wildcard in this pattern SHOULD NOT match a directory in a candidate path.
/// That is, a pattern like "foo/*" should match "foo/bar" but not "foo/bar/baz".
/// </summary>
/// <param name="pattern"></param>
public record struct PathPattern(string pattern);

/// <summary>
/// PEM format and a string. All RSA keys MUST be at least 2048 bits.
/// </summary>
public record struct PEMString(string PemEncodedValue);

/// <summary>
/// 64-bit hex-encoded string
/// </summary>
public record struct HexString(string HexEncodedValue);

public record struct KeyId(HexDigest digest);

public record SignatureResult(KeyId keyId, Signature Signature);

public record FileMetadata(uint Version, uint? Length, List<DigestAlgorithms.DigestValue>? Hashes);