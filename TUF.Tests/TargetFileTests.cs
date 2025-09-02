using System.Text;

using TUF.Repository;

namespace TUF.Tests;

public class TargetFileTests
{
    [Test]
    public async Task Constructor_ValidParameters_CreatesTargetFile()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile = new TargetFile(path, content);

        await Assert.That(targetFile.Path).IsEqualTo(path);
        await Assert.That(targetFile.Content).IsEqualTo(content);
        await Assert.That(targetFile.Custom).IsNull();
    }

    [Test]
    public async Task Constructor_WithCustomMetadata_StoresCustomData()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");
        var custom = new Dictionary<string, object>
        {
            ["description"] = "Test file",
            ["version"] = 1,
            ["critical"] = true
        };

        var targetFile = new TargetFile(path, content, custom);

        await Assert.That(targetFile.Path).IsEqualTo(path);
        await Assert.That(targetFile.Content).IsEqualTo(content);
        await Assert.That(targetFile.Custom).IsEqualTo(custom);
    }

    [Test]
    public async Task Constructor_WithNullCustom_AllowsNullCustom()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile = new TargetFile(path, content, null);

        await Assert.That(targetFile.Custom).IsNull();
    }

    [Test]
    public async Task Equality_SameValues_AreEqual()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");
        var custom = new Dictionary<string, object> { ["key"] = "value" };

        var targetFile1 = new TargetFile(path, content, custom);
        var targetFile2 = new TargetFile(path, content, custom);

        await Assert.That(targetFile1).IsEqualTo(targetFile2);
        await Assert.That(targetFile1.GetHashCode()).IsEqualTo(targetFile2.GetHashCode());
    }

    [Test]
    public async Task Equality_DifferentPath_AreNotEqual()
    {
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile1 = new TargetFile("test1.txt", content);
        var targetFile2 = new TargetFile("test2.txt", content);

        await Assert.That(targetFile1).IsNotEqualTo(targetFile2);
    }

    [Test]
    public async Task Equality_DifferentContent_AreNotEqual()
    {
        var path = "test.txt";

        var targetFile1 = new TargetFile(path, Encoding.UTF8.GetBytes("content1"));
        var targetFile2 = new TargetFile(path, Encoding.UTF8.GetBytes("content2"));

        await Assert.That(targetFile1).IsNotEqualTo(targetFile2);
    }

    [Test]
    public async Task Equality_DifferentCustom_AreNotEqual()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile1 = new TargetFile(path, content, new Dictionary<string, object> { ["key"] = "value1" });
        var targetFile2 = new TargetFile(path, content, new Dictionary<string, object> { ["key"] = "value2" });

        await Assert.That(targetFile1).IsNotEqualTo(targetFile2);
    }

    [Test]
    public async Task ToString_ValidTargetFile_ReturnsReadableString()
    {
        var targetFile = new TargetFile("test.txt", Encoding.UTF8.GetBytes("content"));

        var result = targetFile.ToString();

        await Assert.That(result).Contains("test.txt");
    }

    [Test]
    public async Task Deconstruct_ValidTargetFile_DeconstructsCorrectly()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");
        var custom = new Dictionary<string, object> { ["key"] = "value" };

        var targetFile = new TargetFile(path, content, custom);
        var (deconstructedPath, deconstructedContent, deconstructedCustom) = targetFile;

        await Assert.That(deconstructedPath).IsEqualTo(path);
        await Assert.That(deconstructedContent).IsEqualTo(content);
        await Assert.That(deconstructedCustom).IsEqualTo(custom);
    }

    [Test]
    public async Task Constructor_EmptyPath_AllowsEmptyPath()
    {
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile = new TargetFile("", content);

        await Assert.That(targetFile.Path).IsEqualTo("");
    }

    [Test]
    public async Task Constructor_EmptyContent_AllowsEmptyContent()
    {
        var targetFile = new TargetFile("test.txt", Array.Empty<byte>());

        await Assert.That(targetFile.Content).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Constructor_LargeContent_HandlesLargeFiles()
    {
        var path = "large.bin";
        var content = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(content);

        var targetFile = new TargetFile(path, content);

        await Assert.That(targetFile.Content).HasCount().EqualTo(1024 * 1024);
        await Assert.That(targetFile.Content).IsEqualTo(content);
    }

    [Test]
    public async Task Constructor_ComplexCustomMetadata_StoresComplexData()
    {
        var custom = new Dictionary<string, object>
        {
            ["string_value"] = "test",
            ["int_value"] = 42,
            ["bool_value"] = true,
            ["array_value"] = new[] { "item1", "item2" },
            ["nested_object"] = new Dictionary<string, object>
            {
                ["nested_key"] = "nested_value"
            }
        };

        var targetFile = new TargetFile("complex.json", Encoding.UTF8.GetBytes("{}"), custom);

        await Assert.That(targetFile.Custom).IsNotNull();
        await Assert.That(targetFile.Custom!["string_value"]).IsEqualTo("test");
        await Assert.That(targetFile.Custom["int_value"]).IsEqualTo(42);
        await Assert.That(targetFile.Custom["bool_value"]).IsEqualTo(true);
    }
}