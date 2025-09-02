using System.Text;
using System.Text.Json;

using TUF.Models;
using TUF.Repository;

namespace TUF.Tests;

public class TufRepositoryTests
{
    private TufRepository CreateTestRepository()
    {
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate())
            .AddTarget("hello.txt", Encoding.UTF8.GetBytes("Hello, World!"))
            .AddTarget("config/app.json", Encoding.UTF8.GetBytes("{\"version\":\"1.0\"}"));

        return builder.Build();
    }

    [Test]
    public async Task WriteToDirectory_ValidPath_CreatesCorrectStructure()
    {
        var repository = CreateTestRepository();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            repository.WriteToDirectory(tempDir);

            // Check that directories are created
            await Assert.That(Directory.Exists(tempDir)).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(tempDir, "metadata"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(tempDir, "targets"))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task WriteToDirectory_ValidPath_CreatesAllMetadataFiles()
    {
        var repository = CreateTestRepository();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            repository.WriteToDirectory(tempDir);

            var metadataDir = Path.Combine(tempDir, "metadata");
            var expectedFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };

            foreach (var expectedFile in expectedFiles)
            {
                var filePath = Path.Combine(metadataDir, expectedFile);
                await Assert.That(File.Exists(filePath)).IsTrue();

                // Verify file is valid JSON
                var content = await File.ReadAllTextAsync(filePath);
                await Assert.That(content).IsNotEmpty();

                // Should be valid JSON
                var _ = JsonDocument.Parse(content);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task WriteToDirectory_ValidPath_CreatesAllTargetFiles()
    {
        var repository = CreateTestRepository();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            repository.WriteToDirectory(tempDir);

            var targetsDir = Path.Combine(tempDir, "targets");

            // Check hello.txt
            var helloPath = Path.Combine(targetsDir, "hello.txt");
            await Assert.That(File.Exists(helloPath)).IsTrue();

            var helloContent = await File.ReadAllTextAsync(helloPath);
            await Assert.That(helloContent).IsEqualTo("Hello, World!");

            // Check config/app.json (nested directory)
            var configPath = Path.Combine(targetsDir, "config", "app.json");
            await Assert.That(File.Exists(configPath)).IsTrue();

            var configContent = await File.ReadAllTextAsync(configPath);
            await Assert.That(configContent).IsEqualTo("{\"version\":\"1.0\"}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task WriteToDirectory_ExistingDirectory_OverwritesFiles()
    {
        var repository = CreateTestRepository();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);

            // Create an existing file that should be overwritten
            var existingFile = Path.Combine(tempDir, "metadata", "root.json");
            Directory.CreateDirectory(Path.GetDirectoryName(existingFile)!);
            await File.WriteAllTextAsync(existingFile, "old content");

            repository.WriteToDirectory(tempDir);

            // File should be overwritten with new content
            var newContent = await File.ReadAllTextAsync(existingFile);
            await Assert.That(newContent).IsNotEqualTo("old content");

            // Should be valid JSON
            var _ = JsonDocument.Parse(newContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task WriteToDirectory_NestedTargetPaths_CreatesNestedDirectories()
    {
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate())
            .AddTarget("level1/level2/deep.txt", Encoding.UTF8.GetBytes("deep content"));

        var repository = builder.Build();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            repository.WriteToDirectory(tempDir);

            var deepFile = Path.Combine(tempDir, "targets", "level1", "level2", "deep.txt");
            await Assert.That(File.Exists(deepFile)).IsTrue();

            var content = await File.ReadAllTextAsync(deepFile);
            await Assert.That(content).IsEqualTo("deep content");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task WriteToDirectory_EmptyRepository_CreatesBasicStructure()
    {
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate());

        var repository = builder.Build();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            repository.WriteToDirectory(tempDir);

            // Should still create all metadata files even without targets
            var metadataDir = Path.Combine(tempDir, "metadata");
            await Assert.That(File.Exists(Path.Combine(metadataDir, "root.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(metadataDir, "timestamp.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(metadataDir, "snapshot.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(metadataDir, "targets.json"))).IsTrue();

            // Targets directory should exist but be empty
            var targetsDir = Path.Combine(tempDir, "targets");
            await Assert.That(Directory.Exists(targetsDir)).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task WriteToDirectory_MetadataFilesContainValidTufStructure()
    {
        var repository = CreateTestRepository();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            repository.WriteToDirectory(tempDir);

            var rootFile = Path.Combine(tempDir, "metadata", "root.json");
            var rootContent = await File.ReadAllTextAsync(rootFile);
            var rootJson = JsonDocument.Parse(rootContent);

            // Check that root.json has required TUF structure
            await Assert.That(rootJson.RootElement.TryGetProperty("signed", out _)).IsTrue();
            await Assert.That(rootJson.RootElement.TryGetProperty("signatures", out _)).IsTrue();

            var signed = rootJson.RootElement.GetProperty("signed");
            await Assert.That(signed.TryGetProperty("spec_version", out _)).IsTrue();
            await Assert.That(signed.TryGetProperty("roles", out _)).IsTrue();
            await Assert.That(signed.TryGetProperty("keys", out _)).IsTrue();

            var targetsFile = Path.Combine(tempDir, "metadata", "targets.json");
            var targetsContent = await File.ReadAllTextAsync(targetsFile);
            var targetsJson = JsonDocument.Parse(targetsContent);

            // Check that targets.json has targets information
            var targetsSigned = targetsJson.RootElement.GetProperty("signed");
            await Assert.That(targetsSigned.TryGetProperty("spec_version", out _)).IsTrue();
            await Assert.That(targetsSigned.TryGetProperty("targets", out _)).IsTrue();
            await Assert.That(targetsSigned.TryGetProperty("version", out _)).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Constructor_ValidMetadata_CreatesRepository()
    {
        var repository = CreateTestRepository();

        await Assert.That(repository.Root).IsNotNull();
        await Assert.That(repository.Timestamp).IsNotNull();
        await Assert.That(repository.Snapshot).IsNotNull();
        await Assert.That(repository.Targets).IsNotNull();
        await Assert.That(repository.TargetFiles).IsNotNull();
    }

    [Test]
    public async Task TargetFiles_Dictionary_ContainsAllAddedTargets()
    {
        var repository = CreateTestRepository();

        await Assert.That(repository.TargetFiles.ContainsKey("hello.txt")).IsTrue();
        await Assert.That(repository.TargetFiles.ContainsKey("config/app.json")).IsTrue();

        var helloFile = repository.TargetFiles["hello.txt"];
        await Assert.That(Encoding.UTF8.GetString(helloFile.Content)).IsEqualTo("Hello, World!");

        var configFile = repository.TargetFiles["config/app.json"];
        await Assert.That(Encoding.UTF8.GetString(configFile.Content)).IsEqualTo("{\"version\":\"1.0\"}");
    }
}