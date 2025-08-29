namespace TUF.Models.Keys.Schemes;

public interface IKeyScheme<T> where T : IKeyScheme<T>
{
    static abstract string Name { get; }
}

public sealed record RSASSA_PSS_SHA256() : IKeyScheme<RSASSA_PSS_SHA256>
{
    public static string Name => "rsassa-pss-sha256";
}

public sealed record Ed25519() : IKeyScheme<Ed25519>
{
    public static string Name => "ed25519";
}

public sealed record ECDSA_SHA2_NISTP256() : IKeyScheme<ECDSA_SHA2_NISTP256>
{
    public static string Name => "ecdsa-sha2-nistp256";
}