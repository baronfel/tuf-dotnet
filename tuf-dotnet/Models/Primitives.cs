using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace TUF.Models.Primitives;

public record SemanticVersion(string SemVer);

[JsonConverter(typeof(Tuf.DotNet.Serialization.Converters.RelativePathJsonConverter))]
public record struct RelativePath(string RelPath);

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
public record struct Signature(byte[] Value);

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
/// <param name="Pattern"></param>
public record struct PathPattern(string Pattern);

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