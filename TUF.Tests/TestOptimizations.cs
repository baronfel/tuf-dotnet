using System.Text;
using TUF.Models;
using TUF.Repository;

namespace TUF.Tests;

/// <summary>
/// Shared test optimizations to reduce allocation overhead and expensive operations
/// </summary>
public static class TestOptimizations
{
    /// <summary>
    /// Thread-local counter for generating unique test directories without Guid allocation
    /// </summary>
    private static readonly ThreadLocal<int> DirectoryCounter = new(() => Random.Shared.Next(1000, 9999));

    /// <summary>
    /// Pre-generated signers for reuse across tests to avoid expensive key generation
    /// </summary>
    public static class SharedSigners
    {
        public static readonly Ed25519Signer Root = Ed25519Signer.Generate();
        public static readonly Ed25519Signer Timestamp = Ed25519Signer.Generate();
        public static readonly Ed25519Signer Snapshot = Ed25519Signer.Generate();
        public static readonly Ed25519Signer Targets = Ed25519Signer.Generate();
    }

    /// <summary>
    /// Pre-built test repository to avoid repeated expensive construction
    /// </summary>
    public static readonly Lazy<TufRepository> SharedTestRepository = new(() =>
    {
        var builder = new RepositoryBuilder()
            .AddSigner("root", SharedSigners.Root)
            .AddSigner("timestamp", SharedSigners.Timestamp)
            .AddSigner("snapshot", SharedSigners.Snapshot)
            .AddSigner("targets", SharedSigners.Targets)
            .AddTarget("hello.txt", Encoding.UTF8.GetBytes("Hello, World!"))
            .AddTarget("config/app.json", Encoding.UTF8.GetBytes("{\"version\":\"1.0\"}"));

        return builder.Build();
    });

    /// <summary>
    /// Generates unique test directory paths without Guid allocation overhead
    /// Uses thread-local counter and process/thread IDs for uniqueness
    /// </summary>
    public static string GetUniqueTestDirectory()
    {
        var counter = DirectoryCounter.Value++;
        var processId = Environment.ProcessId;
        var threadId = Environment.CurrentManagedThreadId;
        
        // Format: tuf-test-{processId}-{threadId}-{counter}
        // Much faster than Guid.NewGuid().ToString() and still unique
        return Path.Combine(Path.GetTempPath(), $"tuf-test-{processId}-{threadId}-{counter}");
    }

    /// <summary>
    /// Creates a disposable test directory that automatically cleans up
    /// </summary>
    public static IDisposable CreateManagedTestDirectory(out string path)
    {
        path = GetUniqueTestDirectory();
        Directory.CreateDirectory(path);
        return new DirectoryCleanup(path);
    }

    private sealed class DirectoryCleanup : IDisposable
    {
        private readonly string _path;
        
        public DirectoryCleanup(string path)
        {
            _path = path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                try
                {
                    Directory.Delete(_path, true);
                }
                catch
                {
                    // Best effort cleanup - don't fail tests due to cleanup issues
                }
            }
        }
    }
}