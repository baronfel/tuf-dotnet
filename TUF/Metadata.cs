using System.Reflection;

using Serde;

namespace TUF.Models;

/// <summary>
/// Generic wrapper for TUF metadata that combines signed content with cryptographic signatures.
/// This represents the complete structure of any TUF metadata file (root, timestamp, snapshot, targets, mirrors).
/// </summary>
/// <typeparam name="T">The type of signed metadata content (Root, Timestamp, Snapshot, Targets, or Mirrors)</typeparam>
/// <remarks>
/// From TUF specification section 4.1: "All TUF metadata files have the same high-level format:
/// a 'signed' object containing the actual metadata, and a 'signatures' array containing
/// cryptographic signatures of the signed object."
/// 
/// This generic structure provides:
/// 1. Type safety - ensures signed content matches the expected metadata type
/// 2. Signature verification - standardized signature validation across all metadata types  
/// 3. Serialization consistency - uniform JSON structure for all TUF metadata
/// 4. Extensibility - easy to add new metadata types while reusing common signature logic
/// 
/// The two-level structure (signed + signatures) enables:
/// - Canonical serialization of the signed portion for signature verification
/// - Multiple signatures from different keys for the same metadata
/// - Clean separation between content and authentication
/// - Consistent signature verification logic across all metadata types
/// </remarks>
[SerdeTypeOptions(Proxy = typeof(MetadataTProxy))]
public partial record Metadata<T>
{
    /// <summary>
    /// The signed metadata content that is cryptographically protected.
    /// This object is serialized in canonical form for signature generation and verification.
    /// </summary>
    /// <remarks>
    /// The signed object contains all the actual metadata information (keys, roles, targets, etc.).
    /// When verifying signatures, this object is serialized to canonical JSON format and
    /// the resulting bytes are what the signatures protect.
    /// 
    /// Changes to any field in the signed object will invalidate existing signatures,
    /// requiring new signatures from authorized keys.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "signed")]
    public T Signed { get; init; } = default!;

    /// <summary>
    /// Array of cryptographic signatures that authenticate the signed metadata.
    /// Each signature is created by a key authorized for the metadata's role.
    /// </summary>
    /// <remarks>
    /// The signatures array contains one or more signature objects, each pairing:
    /// 1. A key ID identifying which key created the signature
    /// 2. The actual signature value (hex-encoded cryptographic signature)
    /// 
    /// For metadata to be valid:
    /// - At least 'threshold' number of signatures must be present (defined in role configuration)
    /// - Each signature must be from a key authorized for this role
    /// - Each signature must be a valid cryptographic signature of the canonical signed content
    /// - All signing keys must be properly trusted through the TUF trust chain
    /// 
    /// Multiple signatures provide:
    /// - Security against key compromise (requires threshold number of keys)
    /// - Key rotation capabilities (old and new keys can both sign during transitions)
    /// - Distributed signing scenarios (different entities can contribute signatures)
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "signatures")]
    public List<SignatureObject> Signatures { get; init; } = new();
}

public static class MetadataTProxy
{
    public abstract class SerMetadataBase<TSelf, T, TMetadata, TProvider>
        : ISerialize<TMetadata>, ISerializeProvider<TMetadata>
        where T : class, ISerializeProvider<T>
        where TSelf : ISerialize<TMetadata>, new()
        where TMetadata : Metadata<T>
        where TProvider : ISerializeProvider<T>
    {

        public static TSelf Instance { get; } = new();
        static ISerialize<TMetadata> ISerializeProvider<TMetadata>.Instance => Instance;
        public ISerdeInfo SerdeInfo { get; }

        private readonly ITypeSerialize<T> _signedSer;
        private readonly ITypeSerialize<List<SignatureObject>> _sigListSer;

        protected SerMetadataBase(ISerdeInfo serdeInfo)
        {
            _signedSer = TypeSerialize.GetOrBox<T, TProvider>();
            _sigListSer = TypeSerialize.GetOrBox<List<SignatureObject>, ListProxy.Ser<SignatureObject, SignatureObject>>();
            SerdeInfo = serdeInfo;
        }

        void ISerialize<TMetadata>.Serialize(TMetadata value, ISerializer serializer)
        {
            var metadataSerializer = serializer.WriteType(SerdeInfo);
            metadataSerializer.WriteValue(SerdeInfo, 0, value.Signed, TProvider.Instance);
            metadataSerializer.WriteValue(SerdeInfo, 1, value.Signatures, ListProxy.Ser<SignatureObject, SignatureObject>.Instance);
            metadataSerializer.End(ListProxy.Ser<SignatureObject, SignatureObject>.Instance.SerdeInfo);
        }
    }

    public abstract class DeMetadataBase<TSelf, T, TMetadata, TProvider, TBothProvider> : IDeserialize<TMetadata>, IDeserializeProvider<TMetadata>
        where TSelf : IDeserialize<TMetadata>, new()
        where TMetadata : Metadata<T>
        where T : class, IDeserializeProvider<T>, ISerializeProvider<T>
        where TProvider : IDeserializeProvider<T>
        where TBothProvider : IDeserializeProvider<T>, ISerializeProvider<T>
    {
        public static IDeserialize<TMetadata> Instance => new TSelf();
        public ISerdeInfo SerdeInfo => MetadataSerdeInfo<T, TBothProvider>.Instance;

        private readonly ITypeDeserialize<T> _signedDe;
        private readonly ITypeDeserialize<List<SignatureObject>> _sigListDe;

        protected DeMetadataBase()
        {
            _signedDe = TypeDeserialize.GetOrBox<T, TProvider>();
            _sigListDe = TypeDeserialize.GetOrBox<List<SignatureObject>, ListProxy.De<SignatureObject, SignatureObject>>();
        }

        public TMetadata Deserialize(IDeserializer deserializer)
        {
            var metadataReader = deserializer.ReadType(SerdeInfo);
            T signed = default!;
            List<SignatureObject> sigList = default!;
            while (metadataReader.TryReadIndex(SerdeInfo, out var errorName) is int fieldIdx && fieldIdx is not ITypeDeserializer.EndOfType)
            {
                switch (fieldIdx)
                {
                    case 0:
                        signed = metadataReader.ReadValue(SerdeInfo, 0, TProvider.Instance);
                        break;
                    case 1:
                        sigList = metadataReader.ReadValue(SerdeInfo, 1, ListProxy.De<SignatureObject, SignatureObject>.Instance);
                        break;
                }
            }

            return Create(signed, sigList);
        }

        protected abstract TMetadata Create(T signed, List<SignatureObject> sigs);
    }

    internal static class MetadataSerdeInfo<T, TProvider>
        where T : ISerializeProvider<T>, IDeserializeProvider<T>
        where TProvider : ISerializeProvider<T>
    {
        public static readonly ISerdeInfo Instance = SerdeInfo.MakeCustom("Metadata", [], [
            ("signed", TProvider.Instance.SerdeInfo, typeof(Metadata<T>).GetProperty("Signed")),
        ("signatures", ListProxy.Ser<SignatureObject, SignatureObject>.Instance.SerdeInfo, typeof(Metadata<T>).GetProperty("Signatures")),
    ]);
    }

}
public static class MetadataProxy
{
    public sealed class Ser<T>() : MetadataTProxy.SerMetadataBase<Ser<T>, T, Metadata<T>, T>(MetadataTProxy.MetadataSerdeInfo<T, T>.Instance)
        where T : class, ISerializeProvider<T>, IDeserializeProvider<T>
    {
    }

    public sealed class De<T> : MetadataTProxy.DeMetadataBase<De<T>, T, Metadata<T>, T, T>
        where T : class, ISerializeProvider<T>, IDeserializeProvider<T>
    {
        protected override Metadata<T> Create(T signed, List<SignatureObject> sigs)
        {
            return new Metadata<T>()
            {
                Signed = signed,
                Signatures = sigs
            };
        }
    }
}