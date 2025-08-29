using TUF.Models.Primitives;

namespace TUF.Signing;

public interface ISigner
{
    public Models.Keys.Key Key { get; }
    public Signature SignBytes(ReadOnlySpan<byte> data);
}