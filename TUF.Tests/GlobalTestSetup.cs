using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Global test setup and teardown for the TUF.Tests assembly.
/// Manages shared resources like HttpClient pools to improve test performance.
/// </summary>
public static class GlobalTestSetup
{
    /// <summary>
    /// Assembly-level teardown to clean up shared resources.
    /// </summary>
    [After(Assembly)]
    public static void AssemblyCleanup(AssemblyHookContext context)
    {
        // Dispose all HttpClient instances in the pool
        SharedTestHttpClientPool.DisposeAll();
    }
}