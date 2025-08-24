using TUF.Models.Primitives;
using TUF.Serialization;
using TUF.Signing;

namespace TUF.Models;

public record Metadata<T>(
    T Signed,
    Dictionary<KeyId, Signature> Signatures,
    Dictionary<string, object>? UnrecognizedFields)
    where T: Roles.IRole
{
    public byte[] SignedBytes => CanonicalJsonSerializer.Serialize(Signed);

    /// <summary>
    /// Signs the metadata using the provided signer.
    /// </summary>
    /// <param name="signer"></param>
    /// <param name="replaceExisting"></param>
    /// <returns></returns>
    public SignatureResult Sign(ISigner signer, bool replaceExisting = true)
    {
        var sig = signer.Sign(this);

        if (replaceExisting)
        {
            Signatures.Clear();
        }

        Signatures[sig.keyId] = sig.Signature;
        return sig;
    }

    public bool IsExpired(DateTimeOffset reference)
    {
        return reference > Signed.Expires;
    }
}

