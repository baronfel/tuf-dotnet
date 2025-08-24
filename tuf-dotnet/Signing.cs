namespace TUF.Signing;

using TUF.Models;
using TUF.Models.Roles;

public interface ISigner
{
    public SignatureResult Sign<T>(Metadata<T> metadata) where T : IRole;
}