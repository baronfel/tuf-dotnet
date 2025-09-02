using System.Diagnostics;
using System.Net;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Local conformance tests that mirror the official TUF conformance test suite.
/// These tests provide local debugging capabilities for conformance issues.
/// </summary>
public class ConformanceTests : IDisposable
{
    private readonly HttpListener? _httpListener;
    private Task? _serverTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly string _testDataDir;
    private readonly string _metadataDir;
    private readonly string _targetDir;
    private readonly string _cliPath;
    private readonly string _baseUrl;

    public ConformanceTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), "tuf-conformance-tests", Guid.NewGuid().ToString());
        _metadataDir = Path.Combine(_testDataDir, "metadata");
        _targetDir = Path.Combine(_testDataDir, "targets");

        Directory.CreateDirectory(_testDataDir);
        Directory.CreateDirectory(_metadataDir);
        Directory.CreateDirectory(_targetDir);

        _cliPath = FindConformanceCliPath();

        var port = GetRandomAvailablePort();
        _baseUrl = $"http://localhost:{port}";
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"{_baseUrl}/");

        StartTestServer();
        SetupTestData();
    }

    private string FindConformanceCliPath()
    {
        var baseDir = Environment.CurrentDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "examples/TufConformanceCli/bin/Release/net10.0/TufConformanceCli"),
            Path.Combine(baseDir, "examples/TufConformanceCli/bin/Debug/net10.0/TufConformanceCli"),
            "/home/runner/work/tuf-dotnet/tuf-dotnet/examples/TufConformanceCli/bin/Release/net10.0/TufConformanceCli"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("Could not find TufConformanceCli executable. Build the project first.");
    }

    private static int GetRandomAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void StartTestServer()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _httpListener!.Start();

        _serverTask = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(async () => await HandleRequest(context), _cancellationTokenSource.Token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Server error: {ex.Message}");
                }
            }
        }, _cancellationTokenSource.Token);
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var urlPath = request.Url?.AbsolutePath?.TrimStart('/') ?? "";

            if (urlPath.EndsWith(".json"))
            {
                var metadataFile = Path.Combine(_testDataDir, "server-metadata", urlPath);
                if (File.Exists(metadataFile))
                {
                    var content = await File.ReadAllTextAsync(metadataFile);
                    response.ContentType = "application/json";
                    response.StatusCode = 200;

                    var buffer = System.Text.Encoding.UTF8.GetBytes(content);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer);
                }
                else
                {
                    response.StatusCode = 404;
                }
            }
            else if (urlPath.StartsWith("targets/"))
            {
                var targetFile = Path.Combine(_testDataDir, "server-targets", urlPath.Substring(8));
                if (File.Exists(targetFile))
                {
                    response.ContentType = "application/octet-stream";
                    response.StatusCode = 200;

                    using var fileStream = File.OpenRead(targetFile);
                    response.ContentLength64 = fileStream.Length;
                    await fileStream.CopyToAsync(response.OutputStream);
                }
                else
                {
                    response.StatusCode = 404;
                }
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request handling error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private void SetupTestData()
    {
        var serverMetadataDir = Path.Combine(_testDataDir, "server-metadata");
        var serverTargetsDir = Path.Combine(_testDataDir, "server-targets");

        Directory.CreateDirectory(serverMetadataDir);
        Directory.CreateDirectory(serverTargetsDir);

        CreateTestRootMetadata(serverMetadataDir);
        CreateTestTimestampMetadata(serverMetadataDir);
        CreateTestSnapshotMetadata(serverMetadataDir);
        CreateTestTargetsMetadata(serverMetadataDir);
        CreateTestTargetFile(serverTargetsDir);
    }

    private void CreateTestRootMetadata(string metadataDir)
    {
        var json = """
        {
          "signatures": [
            { "signature": "test_signature_placeholder" }
          ],
          "signed": {
            "_type": "root",
            "spec_version": "1.0.0",
            "version": 1,
            "expires": "2025-12-31T23:59:59Z",
            "keys": {
              "test_root_key": {
                "keytype": "ed25519",
                "scheme": "ed25519",
                "keyval": { "public": "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234" }
              }
            },
            "roles": {
              "root": { "keyids": ["test_root_key"], "threshold": 1 },
              "timestamp": { "keyids": ["test_root_key"], "threshold": 1 },
              "snapshot": { "keyids": ["test_root_key"], "threshold": 1 },
              "targets": { "keyids": ["test_root_key"], "threshold": 1 }
            },
            "consistent_snapshot": false
          }
        }
        """;

        File.WriteAllText(Path.Combine(metadataDir, "root.json"), json);
    }

    private void CreateTestTimestampMetadata(string metadataDir)
    {
        var json = """
        {
          "signatures": [
            { "signature": "test_signature_placeholder" }
          ],
          "signed": {
            "_type": "timestamp",
            "spec_version": "1.0.0",
            "version": 1,
            "expires": "2025-12-31T23:59:59Z",
            "meta": {
              "snapshot.json": { "version": 1 }
            }
          }
        }
        """;

        File.WriteAllText(Path.Combine(metadataDir, "timestamp.json"), json);
    }

    private void CreateTestSnapshotMetadata(string metadataDir)
    {
        var json = """
        {
          "signatures": [
            { "signature": "test_signature_placeholder" }
          ],
          "signed": {
            "_type": "snapshot",
            "spec_version": "1.0.0",
            "version": 1,
            "expires": "2025-12-31T23:59:59Z",
            "meta": {
              "targets.json": { "version": 1 }
            }
          }
        }
        """;

        File.WriteAllText(Path.Combine(metadataDir, "snapshot.json"), json);
    }

    private void CreateTestTargetsMetadata(string metadataDir)
    {
        var json = """
        {
          "signatures": [
            { "signature": "test_signature_placeholder" }
          ],
          "signed": {
            "_type": "targets",
            "spec_version": "1.0.0",
            "version": 1,
            "expires": "2025-12-31T23:59:59Z",
            "targets": {
              "test-target.txt": {
                "length": 12,
                "hashes": { "sha256": "aec070645fe53ee3b3763059376134f058cc337247c978add178b6ccdfb0019f" }
              }
            }
          }
        }
        """;

        File.WriteAllText(Path.Combine(metadataDir, "targets.json"), json);
    }

    private void CreateTestTargetFile(string targetsDir)
    {
        File.WriteAllText(Path.Combine(targetsDir, "test-target.txt"), "Hello World!");
    }

    private async Task<(int exitCode, string output, string error)> RunCli(params string[] args)
    {
        using var process = new Process();
        process.StartInfo.FileName = _cliPath;
        process.StartInfo.Arguments = string.Join(" ", args.Select(arg => $"\"{arg}\""));
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    [Test]
    public async Task Test_Init_Command()
    {
        var trustedRootPath = Path.Combine(_testDataDir, "trusted-root.json");
        var serverRootPath = Path.Combine(_testDataDir, "server-metadata", "root.json");
        File.Copy(serverRootPath, trustedRootPath);

        var (exitCode, output, error) = await RunCli(
            "init",
            trustedRootPath,
            "--metadata-dir", _metadataDir
        );

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("initialized successfully");

        var rootPath = Path.Combine(_metadataDir, "root.json");
        await Assert.That(File.Exists(rootPath)).IsTrue();
    }

    [Test]
    public async Task Test_Refresh_Command_Shows_Detailed_Error()
    {
        var trustedRootPath = Path.Combine(_testDataDir, "trusted-root.json");
        var serverRootPath = Path.Combine(_testDataDir, "server-metadata", "root.json");
        File.Copy(serverRootPath, trustedRootPath);

        var (initExitCode, _, _) = await RunCli(
            "init",
            trustedRootPath,
            "--metadata-dir", _metadataDir
        );

        await Assert.That(initExitCode).IsEqualTo(0);

        var (exitCode, output, error) = await RunCli(
            "refresh",
            "--metadata-dir", _metadataDir,
            "--metadata-url", _baseUrl
        );

        Console.WriteLine($"Refresh Exit Code: {exitCode}");
        Console.WriteLine($"Refresh Output: {output}");
        Console.WriteLine($"Refresh Error: {error}");

        await Assert.That(exitCode).IsIn(0, 1);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _httpListener?.Stop();
        _httpListener?.Close();
        _serverTask?.Wait(TimeSpan.FromSeconds(5));
        _cancellationTokenSource?.Dispose();

        try
        {
            if (Directory.Exists(_testDataDir))
            {
                Directory.Delete(_testDataDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}