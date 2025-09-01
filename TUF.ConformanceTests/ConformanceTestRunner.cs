using System.Diagnostics;
using System.Text;
using TUF;
using TUF.Models;
using TUnit.Core;
using TUnit.Assertions;

namespace TUF.ConformanceTests;

/// <summary>
/// Local test runner that simulates the TUF conformance test behavior
/// to help debug signature verification issues
/// </summary>
public class ConformanceTestRunner
{
    [Test]
    public async Task TestBasicRefreshRequests_LocalSimulation()
    {
        // This test simulates the exact failure from the tuf_conformance test:
        // "Signature verification failed for key 2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"
        
        // Sample trusted root from the failing test (truncated for brevity, includes the failing key)
        var trustedRootJson = """
        {
          "signatures": [
            {
              "keyid": "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7",
              "sig": "304502201d77f1efa297539b56c755832691dae9be83ea95c185c10d4c6f3dea1e635d1e022100fa3ca29eb195cf90d95563edc25cfe40a48186b03e2a7ec0c14d2f6ff1f8aa1a"
            }
          ],
          "signed": {
            "_type": "root",
            "consistent_snapshot": true,
            "expires": "2025-10-01T05:26:16Z",
            "keys": {
              "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7": {
                "keytype": "ecdsa",
                "keyval": {
                  "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
                },
                "scheme": "ecdsa-sha2-nistp256"
              }
            },
            "roles": {
              "root": {
                "keyids": [
                  "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"
                ],
                "threshold": 1
              }
            },
            "spec_version": "1.0.31",
            "version": 1
          }
        }
        """;

        Console.WriteLine("Testing signature verification with the failing root metadata...");
        
        try
        {
            // Try to deserialize and verify the root metadata
            var root = CanonicalJson.Serializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(trustedRootJson);
            
            Console.WriteLine($"Root metadata loaded successfully");
            Console.WriteLine($"Key count: {root.Signed.Keys.Count}");
            Console.WriteLine($"Signature count: {root.Signatures.Count}");
            
            var signature = root.Signatures.First();
            Console.WriteLine($"Signature KeyID: {signature.KeyId}");
            Console.WriteLine($"Signature Value: {signature.Sig[..20]}...");
            
            var key = root.Signed.Keys[signature.KeyId];
            Console.WriteLine($"Key Type: {key.KeyType}");
            Console.WriteLine($"Key Scheme: {key.Scheme}");
            Console.WriteLine($"Public Key: {key.KeyVal.Public[..50]}...");
            
            // Try to verify the signature - this should fail with the current implementation
            var signedBytes = root.GetSignedBytes();
            Console.WriteLine($"Signed bytes length: {signedBytes.Length}");
            
            var verificationResult = key.VerifySignature(signature.Sig, signedBytes);
            Console.WriteLine($"Signature verification result: {verificationResult}");
            
            // This assertion will fail, showing us the exact problem
            await Assert.That(verificationResult).IsTrue();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during test: {ex}");
            throw;
        }
    }

    [Test]
    public async Task TestTufConformanceCli_InitAndRefresh()
    {
        // Test the actual CLI commands that are failing
        var tempDir = Path.GetTempPath();
        var testDir = Path.Combine(tempDir, $"tuf_test_{Guid.NewGuid():N}");
        var metadataDir = Path.Combine(testDir, "metadata");
        
        try
        {
            Directory.CreateDirectory(metadataDir);
            
            // Create a minimal trusted root file
            var trustedRootPath = Path.Combine(testDir, "root.json");
            var trustedRootJson = """
            {
              "signatures": [
                {
                  "keyid": "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7",
                  "sig": "304502201d77f1efa297539b56c755832691dae9be83ea95c185c10d4c6f3dea1e635d1e022100fa3ca29eb195cf90d95563edc25cfe40a48186b03e2a7ec0c14d2f6ff1f8aa1a"
                }
              ],
              "signed": {
                "_type": "root",
                "consistent_snapshot": true,
                "expires": "2025-10-01T05:26:16Z",
                "keys": {
                  "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7": {
                    "keytype": "ecdsa",
                    "keyval": {
                      "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
                    },
                    "scheme": "ecdsa-sha2-nistp256"
                  }
                },
                "roles": {
                  "root": {
                    "keyids": [
                      "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"
                    ],
                    "threshold": 1
                  }
                },
                "spec_version": "1.0.31",
                "version": 1
              }
            }
            """;
            
            File.WriteAllText(trustedRootPath, trustedRootJson);
            
            Console.WriteLine($"Created test directory: {testDir}");
            Console.WriteLine($"Metadata directory: {metadataDir}");
            Console.WriteLine($"Trusted root: {trustedRootPath}");
            
            // Test init command
            Console.WriteLine("Testing init command...");
            var initResult = RunCliCommand("init", [
                "--metadata-dir", metadataDir,
                trustedRootPath
            ]);
            
            Console.WriteLine($"Init command exit code: {initResult.exitCode}");
            Console.WriteLine($"Init stdout: {initResult.stdout}");
            Console.WriteLine($"Init stderr: {initResult.stderr}");
            
            await Assert.That(initResult.exitCode).IsEqualTo(0);
            
            // Now test refresh command - this will fail
            Console.WriteLine("Testing refresh command...");
            var refreshResult = RunCliCommand("refresh", [
                "--metadata-dir", metadataDir,
                "--metadata-url", "http://example.com/metadata"  // Won't actually connect, but will try to verify signatures
            ]);
            
            Console.WriteLine($"Refresh command exit code: {refreshResult.exitCode}");
            Console.WriteLine($"Refresh stdout: {refreshResult.stdout}");
            Console.WriteLine($"Refresh stderr: {refreshResult.stderr}");
        }
        finally
        {
            // Clean up
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    private (int exitCode, string stdout, string stderr) RunCliCommand(string command, string[] args)
    {
        // Find the TufConformanceCli DLL - always use dotnet for cross-platform consistency
        var dllPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "examples", "TufConformanceCli", "bin", "Debug", "net10.0", "TufConformanceCli.dll");
        
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"TufConformanceCli.dll not found at: {dllPath}");
        }
        
        var allArgs = new List<string> { dllPath, command };
        allArgs.AddRange(args);
        
        string fileName = "dotnet";
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Join(" ", allArgs.Select(arg => $"\"{arg}\"")),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        
        process.ErrorDataReceived += (sender, e) => {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        
        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}