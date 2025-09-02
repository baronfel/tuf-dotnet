using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Microsoft.Extensions.FileSystemGlobbing;

using Tuf.DotNet.Serialization.Converters;

using TUF.Serialization;

namespace TUF.Models.Primitives;

[JsonConverter(typeof(ParseableStringConverter<SemanticVersion>))]
public record SemanticVersion(string SemVer) : IParsable<SemanticVersion>, IJsonStringWriteable<SemanticVersion>
{
    public static SemanticVersion Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out SemanticVersion result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(s);
        return true;
    }

    public string ToJsonString() => SemVer;
}

[JsonConverter(typeof(ParseableStringConverter<RelativePath>))]
public record struct RelativePath(string RelPath) : IParsable<RelativePath>, IJsonStringWriteable<RelativePath>
{
    public static RelativePath Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out RelativePath result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(s);
        return true;
    }

    public readonly string ToJsonString() => RelPath;
}

[JsonConverter(typeof(Tuf.DotNet.Serialization.Converters.AbsoluteUriJsonConverter))]
public record struct AbsoluteUri(Uri Uri)
{
    public static AbsoluteUri From([StringSyntax("uri")] string absoluteUri) => new(new Uri(absoluteUri, UriKind.Absolute));
}
[JsonConverter(typeof(Tuf.DotNet.Serialization.Converters.RelativeUriJsonConverter))]
public record struct RelativeUri(Uri Uri)
{
    public static RelativeUri From([StringSyntax("uri")] string relativeUri) => new(new Uri(relativeUri, UriKind.Relative));
}

/// <summary>
/// A hex-encoded signature of the canonical form of a metadata object
/// </summary>
[JsonConverter(typeof(ParseableStringConverter<Signature>))]
public record struct Signature(byte[] Value) : IParsable<Signature>, IJsonStringWriteable<Signature>
{
    public static Signature Parse(string s, IFormatProvider? provider) => new(Convert.FromHexString(s));

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Signature result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(Convert.FromHexString(s));
        return true;
    }

    public string ToJsonString() => Convert.ToHexString(Value).ToLowerInvariant();
}

/// <summary>
/// A wrapper around a value that has been hex-encoded - that is, its SHA-256 hash has been computed and converted into a 
/// hexadecimal string.
/// </summary>
/// <param name="sha256HexDigest"></param>
[JsonConverter(typeof(ParseableStringConverter<HexDigest>))]
public record struct HexDigest(string sha256HexDigest) : IParsable<HexDigest>, IJsonStringWriteable<HexDigest>
{
    public static HexDigest Parse(string s, IFormatProvider? provider) => new(s);
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out HexDigest result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(s);
        return true;
    }

    public string ToJsonString() => sha256HexDigest;

}

/// <summary>
/// A wrapper around a string that represents a Unix-shell-style file path pattern.
/// This is always a relative path. It may support *- or ?-based wildcards.
/// It SHOULD always use the forward slash (/) as the path separator.
/// It SHOULD NOT start with a directory separator.
/// Implementation note: A wildcard in this pattern SHOULD NOT match a directory in a candidate path.
/// That is, a pattern like "foo/*" should match "foo/bar" but not "foo/bar/baz".
/// </summary>
[JsonConverter(typeof(ParseableStringConverter<PathPattern>))]
public record struct PathPattern(string Pattern) : IParsable<PathPattern>, IJsonStringWriteable<PathPattern>
{
    private readonly Matcher _matcher => new Matcher(StringComparison.Ordinal).AddInclude(Pattern);

    public static PathPattern Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out PathPattern result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(s);
        return true;
    }

    public bool IsMatch(string path)
    {
        return _matcher.Match(path).HasMatches;
    }

    public string ToJsonString() => Pattern;
}

/// <summary>
/// PEM format and a string. All RSA keys MUST be at least 2048 bits.
/// </summary>
[JsonConverter(typeof(ParseableStringConverter<PEMString>))]
public record struct PEMString(string PemEncodedValue) : IParsable<PEMString>, IJsonStringWriteable<PEMString>
{
    public static PEMString Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out PEMString result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(s);
        return true;
    }

    public string ToJsonString() => PemEncodedValue;
}

/// <summary>
/// 64-bit hex-encoded string
/// </summary>
[JsonConverter(typeof(ParseableStringConverter<HexString>))]
public record struct HexString(string HexEncodedValue) : IParsable<HexString>, IJsonStringWriteable<HexString>
{
    public static HexString Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out HexString result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(s);
        return true;
    }

    public string ToJsonString() => HexEncodedValue;

}

[JsonConverter(typeof(ParseableStringConverter<KeyId>))]
public record struct KeyId(HexDigest digest) : IParsable<KeyId>, IJsonStringWriteable<KeyId>
{
    public static KeyId Parse(string s, IFormatProvider? provider) => new(new HexDigest(s));

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out KeyId result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        result = new(new HexDigest(s));
        return true;
    }

    public string ToJsonString() => digest.ToJsonString();
}

public static class HashVerificationExtensions
{
    extension<T>(T hasHashesAndLength)
        where T : IVerifyHashes, IVerifyLength
    {
        public void VerifyLengthHashes(byte[] data)
        {
            if (hasHashesAndLength.Hashes is { Count: > 0 })
            {
                foreach (var hash in hasHashesAndLength.Hashes)
                {
                    hash.VerifyHash(data);
                }
            }
            if (hasHashesAndLength.Length is uint l)
            {
                if (data.Length != l)
                {
                    throw new Exception($"Data length {data.Length} does not match expected length {l}");
                }
            }
        }
    }
}
public record FileMetadata(uint Version, uint? Length, List<DigestAlgorithms.DigestValue>? Hashes) :
    IVerifyHashes, IVerifyLength;

public interface IVerifyHashes
{
    List<DigestAlgorithms.DigestValue>? Hashes { get; }
}

public interface IVerifyLength
{
    uint? Length { get; }
}