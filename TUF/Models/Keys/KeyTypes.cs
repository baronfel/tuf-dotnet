namespace TUF.Models.Keys.Types;

public interface IKeyType<T> where T : IKeyType<T>
{
    static abstract string Name { get; }
}

public sealed record Rsa : IKeyType<Rsa>
{
    public static string Name => "rsa";
}

public sealed record Ed25519 : IKeyType<Ed25519>
{
    public static string Name => "ed25519";
}

public sealed record Ecdsa : IKeyType<Ecdsa>
{
    public static string Name => "ecdsa";
}