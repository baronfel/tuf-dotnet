namespace TUF.Models.Keys.Schemes;

public interface IKeyScheme
{
    string Name { get; }
}

public interface IKeyScheme<T> where T : IKeyScheme<T>
{
    static abstract string Name { get; }
}

public record KeyScheme(string Name) : IKeyScheme
{
}

public record KeyScheme<T>() : KeyScheme(T.Name) where T : IKeyScheme<T>
{
}

public sealed record RSASSA_PSS_SHA256() : KeyScheme<RSASSA_PSS_SHA256>(), IKeyScheme<RSASSA_PSS_SHA256>
{
    public static new string Name => "rsassa-pss-sha256";
}

public sealed record Ed25519() : KeyScheme<Ed25519>(), IKeyScheme<Ed25519>
{
    public static new string Name => "ed25519";
}

public sealed record ECDSA_SHA2_NISTP256() : KeyScheme<ECDSA_SHA2_NISTP256>(), IKeyScheme<ECDSA_SHA2_NISTP256>
{
    public static new string Name => "ecdsa-sha2-nistp256";
}