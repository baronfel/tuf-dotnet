using TUF.Repository;
using TUF.Signing;

namespace RepositoryManager;

/// <summary>
/// Simple test to verify RepositoryBuilder functionality
/// </summary>
public static class RepositoryBuilderTest
{
    public static void RunTests()
    {
        Console.WriteLine("🧪 Running RepositoryBuilder tests...");
        
        try
        {
            TestBasicRepositoryCreation();
            TestMultipleTargets();
            TestCustomExpiry();
            Console.WriteLine("✅ All tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            throw;
        }
    }
    
    static void TestBasicRepositoryCreation()
    {
        Console.WriteLine("   Testing basic repository creation...");
        
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate())
            .AddTarget("test.txt", System.Text.Encoding.UTF8.GetBytes("test content"));

        var repository = builder.Build();
        
        if (repository.Root == null) throw new Exception("Root metadata not created");
        if (repository.Timestamp == null) throw new Exception("Timestamp metadata not created");
        if (repository.Snapshot == null) throw new Exception("Snapshot metadata not created");
        if (repository.Targets == null) throw new Exception("Targets metadata not created");
        if (repository.TargetFiles.Count != 1) throw new Exception("Target files not added correctly");
        
        Console.WriteLine("   ✅ Basic repository creation");
    }
    
    static void TestMultipleTargets()
    {
        Console.WriteLine("   Testing multiple target files...");
        
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate())
            .AddTarget("file1.txt", System.Text.Encoding.UTF8.GetBytes("content 1"))
            .AddTarget("file2.txt", System.Text.Encoding.UTF8.GetBytes("content 2"))
            .AddTarget("subdir/file3.txt", System.Text.Encoding.UTF8.GetBytes("content 3"));

        var repository = builder.Build();
        
        if (repository.TargetFiles.Count != 3) throw new Exception($"Expected 3 targets, got {repository.TargetFiles.Count}");
        if (!repository.TargetFiles.ContainsKey("file1.txt")) throw new Exception("file1.txt not found");
        if (!repository.TargetFiles.ContainsKey("file2.txt")) throw new Exception("file2.txt not found");
        if (!repository.TargetFiles.ContainsKey("subdir/file3.txt")) throw new Exception("subdir/file3.txt not found");
        
        Console.WriteLine("   ✅ Multiple target files");
    }
    
    static void TestCustomExpiry()
    {
        Console.WriteLine("   Testing custom expiry dates...");
        
        var customExpiry = DateTimeOffset.UtcNow.AddMonths(6);
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate())
            .SetDefaultExpiry(customExpiry)
            .AddTarget("test.txt", System.Text.Encoding.UTF8.GetBytes("test content"));

        var repository = builder.Build();
        
        // Check that expiry dates are set correctly (within a reasonable range)
        var rootExpiry = repository.Root.Signed.Expires;
        if (Math.Abs((rootExpiry - customExpiry).TotalMinutes) > 5) 
            throw new Exception($"Root expiry not set correctly: expected ~{customExpiry}, got {rootExpiry}");
            
        Console.WriteLine("   ✅ Custom expiry dates");
    }
}