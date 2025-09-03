using TUF.Tests.TestFixtures;

namespace TUF.Tests;

/// <summary>
/// Global test assembly cleanup to dispose shared resources
/// </summary>
public sealed class GlobalTestCleanup : IDisposable
{
    private static readonly Lazy<GlobalTestCleanup> Instance = new(() => new GlobalTestCleanup());
    private static bool _disposed = false;

    public static GlobalTestCleanup GetInstance() => Instance.Value;

    static GlobalTestCleanup()
    {
        // Register cleanup when the process is exiting
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeSharedResources();
        AppDomain.CurrentDomain.DomainUnload += (_, _) => DisposeSharedResources();
    }

    private static void DisposeSharedResources()
    {
        if (!_disposed)
        {
            SharedTestResources.Dispose();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        DisposeSharedResources();
        GC.SuppressFinalize(this);
    }
}