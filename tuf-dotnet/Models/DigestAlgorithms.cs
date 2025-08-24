namespace TUF.Models.DigestAlgorithms;

public interface IDigestAlgorithm<T> where T : IDigestAlgorithm<T>
{
    static abstract string Name { get; }
}

public sealed record SHA256 : IDigestAlgorithm<SHA256>
{
    public static string Name => "sha256";
}

public sealed record SHA512 : IDigestAlgorithm<SHA512>
{
    public static string Name => "sha512";
}

public record DigestValue(string Algorithm, string HexEncodedValue);
public sealed record DigestValue<T>(string HexEncodedValue) : DigestValue(T.Name, HexEncodedValue) where T : IDigestAlgorithm<T>;