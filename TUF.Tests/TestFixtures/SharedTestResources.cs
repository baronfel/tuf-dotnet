using System.Collections.Concurrent;

namespace TUF.Tests.TestFixtures;

/// <summary>
/// Shared test resources to improve performance by reusing expensive objects
/// </summary>
public static class SharedTestResources
{
    private static readonly Lazy<HttpClient> _sharedHttpClient = new(() => new HttpClient());
    private static readonly ConcurrentBag<string> _tempDirectoriesToCleanup = new();

    /// <summary>
    /// Gets a shared HttpClient instance for tests that don't require mocking
    /// </summary>
    public static HttpClient HttpClient => _sharedHttpClient.Value;

    /// <summary>
    /// Creates a unique temporary directory path and registers it for cleanup
    /// </summary>
    public static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tuf-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempDirectoriesToCleanup.Add(tempDir);
        return tempDir;
    }

    /// <summary>
    /// Cleans up all temporary directories created during tests
    /// </summary>
    public static void CleanupTempDirectories()
    {
        while (_tempDirectoriesToCleanup.TryTake(out var tempDir))
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures - they don't affect test results
            }
        }
    }

    /// <summary>
    /// Disposes shared resources
    /// </summary>
    public static void Dispose()
    {
        if (_sharedHttpClient.IsValueCreated)
        {
            _sharedHttpClient.Value.Dispose();
        }
        CleanupTempDirectories();
    }
}