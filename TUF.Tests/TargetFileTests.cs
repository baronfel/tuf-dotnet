using System.Text;

using TUF.Models;
using TUF.Repository;

namespace TUF.Tests;

public class TargetFileTests
{
    [Test]
    public async Task Constructor_ValidParameters_CreatesTargetFile()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile = new TargetFileInfo(path, content);

        await Assert.That(targetFile.Path).IsEqualTo(path);
        await Assert.That(targetFile.Content).IsEqualTo(content);
        await Assert.That(targetFile.Custom).IsNull();
    }

    [Test]
    public async Task Constructor_WithNullCustom_AllowsNullCustom()
    {
        var path = "test.txt";
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile = new TargetFileInfo(path, content, null);

        await Assert.That(targetFile.Custom).IsNull();
    }

    [Test]
    public async Task Equality_DifferentContent_AreNotEqual()
    {
        var path = "test.txt";

        var targetFile1 = new TargetFileInfo(path, Encoding.UTF8.GetBytes("content1"));
        var targetFile2 = new TargetFileInfo(path, Encoding.UTF8.GetBytes("content2"));

        await Assert.That(targetFile1).IsNotEqualTo(targetFile2);
    }

    [Test]
    public async Task ToString_ValidTargetFile_ReturnsReadableString()
    {
        var targetFile = new TargetFileInfo("test.txt", Encoding.UTF8.GetBytes("content"));

        var result = targetFile.ToString();

        await Assert.That(result).Contains("test.txt");
    }

    [Test]
    public async Task Constructor_EmptyPath_AllowsEmptyPath()
    {
        var content = Encoding.UTF8.GetBytes("test content");

        var targetFile = new TargetFileInfo("", content);

        await Assert.That(targetFile.Path).IsEqualTo("");
    }

    [Test]
    public async Task Constructor_EmptyContent_AllowsEmptyContent()
    {
        var targetFile = new TargetFileInfo("test.txt", Array.Empty<byte>());

        await Assert.That(targetFile.Content).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Constructor_LargeContent_HandlesLargeFiles()
    {
        var path = "large.bin";
        var content = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(content);

        var targetFile = new TargetFileInfo(path, content);

        await Assert.That(targetFile.Content).HasCount().EqualTo(1024 * 1024);
        await Assert.That(targetFile.Content).IsEqualTo(content);
    }
}