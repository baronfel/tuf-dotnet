using System.Security.Cryptography;

using Serde;

namespace TUF.Models.DigestAlgorithms;

public interface IDigestAlgorithm<T> where T : IDigestAlgorithm<T>
{
    static abstract string Name { get; }
    static abstract Func<byte[], byte[]> Hasher { get; }
}

public sealed record SHA256 : IDigestAlgorithm<SHA256>
{
    public static string Name => "sha256";
    public static Func<byte[], byte[]> Hasher => System.Security.Cryptography.SHA256.HashData;
}

public sealed record SHA512 : IDigestAlgorithm<SHA512>
{
    public static string Name => "sha512";
    public static Func<byte[], byte[]> Hasher => System.Security.Cryptography.SHA512.HashData;
}


public record DigestValue(string Algorithm, string HexEncodedValue)
{
    // should be implemented in child class only
    public virtual void VerifyHash(byte[] data) => throw new NotImplementedException();
}


public record DigestValue<T>(string HexEncodedValue) : DigestValue(T.Name, HexEncodedValue) where T : IDigestAlgorithm<T>
{
    public override void VerifyHash(byte[] data)
    {
        var expectedHash = Convert.FromHexString(HexEncodedValue);
        var actualHash = T.Hasher(data);
        if (!actualHash.SequenceEqual(expectedHash))
        {
            throw new CryptographicException($"{T.Name} verification failed");
        }
    }
}