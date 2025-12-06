# Conformance Testing

This guide explains how to validate TUF .NET against the official TUF specification using the TUF conformance test suite and built-in testing tools.

## Overview

Conformance testing ensures that TUF .NET correctly implements the TUF specification. The official TUF conformance test suite provides standardized tests that validate client behavior across all TUF implementations.

## TUF Conformance Test Suite

The official conformance test suite is maintained at [theupdateframework/tuf-conformance](https://github.com/theupdateframework/tuf-conformance) and provides language-agnostic testing for TUF clients.

### Running Conformance Tests

TUF .NET includes a CLI tool that implements the required conformance interface:

```bash
# Build the conformance CLI
cd examples/TufConformanceCli
dotnet build --configuration Release

# Run a single conformance test
dotnet run --configuration Release -- \
  --metadata-url "file://$(pwd)/test-data/metadata/" \
  --trusted-root "$(pwd)/test-data/root.json" \
  --targets-url "file://$(pwd)/test-data/targets/" \
  --target-name "example-target.txt"
```

### Integration with pytest

The conformance CLI follows the standard interface defined in `CLIENT-CLI.md`:

```bash
# Install the TUF conformance test suite
git clone https://github.com/theupdateframework/tuf-conformance
cd tuf-conformance

# Run tests against TUF .NET
pytest test_conformance.py \
  --client-runner "dotnet run --project /path/to/TufConformanceCli --" \
  --client-xfails-file /path/to/tuf-dotnet/.tuf-conformance-xfails
```

### Expected Failures

TUF .NET tracks expected failures during development in `.tuf-conformance-xfails`:

```bash
# View current expected failures
cat .tuf-conformance-xfails

# Example content:
test_refresh_root_expired_non_persistent_repository
test_refresh_root_expired_persistent_repository  
test_timestamp_rollback_attack
```

These failures are removed as features are implemented and bugs are fixed.

## Built-in Conformance Testing

TUF .NET includes comprehensive conformance testing as part of the test suite:

### Golden Test Validation

```csharp
[TestFixture]
public class ConformanceGoldenTests
{
    [Test]
    public async Task ValidateSignatureVerificationGolden()
    {
        // Load golden test data from the conformance test suite
        var testCases = LoadGoldenTestCases("signature-verification");
        
        foreach (var testCase in testCases)
        {
            // Parse metadata from golden test case
            var metadata = CanonicalJsonDeserializer.Deserialize<Metadata<Root>>(testCase.Metadata);
            
            // Verify signature matches expected result
            bool expectedValid = testCase.ShouldPass;
            bool actualValid = await metadata.VerifySignatureAsync(testCase.TrustedKeys);
            
            Assert.AreEqual(expectedValid, actualValid, 
                $"Signature verification mismatch for test case: {testCase.Name}");
        }
    }
}
```

### Metadata Refresh Conformance

```csharp
[TestFixture] 
public class MetadataRefreshConformanceTests
{
    [Test]
    public async Task TestBasicRefreshConformance()
    {
        // Test data from conformance test suite
        var scenario = LoadConformanceScenario("test_basic_refresh_requests");
        
        var config = new UpdaterConfig(scenario.TrustedRoot, scenario.MetadataUrl)
        {
            LocalMetadataDir = scenario.MetadataDir,
            LocalTargetsDir = scenario.TargetsDir,
            Client = new MockHttpClient(scenario.Responses)
        };
        
        var updater = new Updater(config);
        
        // Should complete refresh successfully
        await updater.RefreshAsync();
        
        // Verify expected metadata was downloaded and verified
        Assert.IsTrue(File.Exists(Path.Combine(scenario.MetadataDir, "root.json")));
        Assert.IsTrue(File.Exists(Path.Combine(scenario.MetadataDir, "timestamp.json")));
        Assert.IsTrue(File.Exists(Path.Combine(scenario.MetadataDir, "snapshot.json")));
        Assert.IsTrue(File.Exists(Path.Combine(scenario.MetadataDir, "targets.json")));
    }
    
    [Test]
    public async Task TestRollbackAttackPrevention()
    {
        var scenario = LoadConformanceScenario("test_timestamp_rollback_attack");
        
        var config = new UpdaterConfig(scenario.TrustedRoot, scenario.MetadataUrl);
        var updater = new Updater(config);
        
        // First refresh should succeed
        await updater.RefreshAsync();
        
        // Second refresh with rolled-back timestamp should fail
        scenario.SetupRollbackAttack();
        
        await Assert.ThrowsAsync<TufRollbackAttackException>(
            async () => await updater.RefreshAsync());
    }
}
```

### Attack Scenario Testing

TUF .NET includes tests for all attack scenarios defined in the TUF specification:

```csharp
[TestFixture]
public class AttackScenarioConformanceTests
{
    [Test]
    public async Task TestReplayAttackPrevention()
    {
        // Simulate replay attack scenario from conformance suite
        var scenario = LoadAttackScenario("replay_attack");
        
        var updater = new Updater(scenario.Config);
        await updater.RefreshAsync();
        
        // Attacker replays old metadata
        scenario.SimulateReplayAttack();
        
        // Should detect and reject replayed metadata
        await Assert.ThrowsAsync<TufReplayAttackException>(
            async () => await updater.RefreshAsync());
    }
    
    [Test] 
    public async Task TestSignatureThresholdEnforcement()
    {
        var scenario = LoadAttackScenario("insufficient_signatures");
        
        var updater = new Updater(scenario.Config);
        
        // Metadata with insufficient signatures should be rejected
        await Assert.ThrowsAsync<TufSignatureException>(
            async () => await updater.RefreshAsync());
    }
    
    [Test]
    public async Task TestMetadataSubstitutionPrevention()
    {
        var scenario = LoadAttackScenario("metadata_substitution");
        
        var updater = new Updater(scenario.Config);
        
        // Attacker substitutes malicious metadata
        scenario.SubstituteMetadata();
        
        // Should detect hash mismatch and reject
        await Assert.ThrowsAsync<TufHashMismatchException>(
            async () => await updater.RefreshAsync());
    }
}
```

## Algorithm Conformance

### Cryptographic Algorithm Testing

```csharp
[TestFixture]
public class CryptographicConformanceTests
{
    [TestCase("ed25519")]
    [TestCase("rsa-pss-sha256")]
    [TestCase("ecdsa-sha2-nistp256")]
    public async Task TestSignatureAlgorithmConformance(string algorithm)
    {
        // Load test vectors from conformance suite
        var testVectors = LoadAlgorithmTestVectors(algorithm);
        
        foreach (var vector in testVectors)
        {
            // Create signer for this algorithm
            var signer = CreateSigner(algorithm, vector.PrivateKey);
            
            // Sign test data
            var signature = await signer.SignAsync(vector.TestData);
            
            // Verify signature matches expected format
            Assert.AreEqual(vector.ExpectedSignature.Length, signature.Length);
            
            // Verify signature can be verified
            var verifier = CreateVerifier(algorithm, vector.PublicKey);
            bool isValid = await verifier.VerifyAsync(vector.TestData, signature);
            Assert.IsTrue(isValid);
        }
    }
    
    [TestCase("sha256")]
    [TestCase("sha512")]
    public void TestHashAlgorithmConformance(string algorithm)
    {
        var testVectors = LoadHashTestVectors(algorithm);
        
        foreach (var vector in testVectors)
        {
            byte[] hash = ComputeHash(algorithm, vector.Input);
            
            CollectionAssert.AreEqual(vector.ExpectedHash, hash,
                $"Hash mismatch for {algorithm} with input: {Convert.ToHexString(vector.Input)}");
        }
    }
}
```

### JSON Canonicalization Conformance

```csharp
[TestFixture]
public class JsonCanonicalizationConformanceTests
{
    [Test]
    public void TestCanonicalJsonConformance()
    {
        // Test cases from RFC 8785 and TUF specification
        var testCases = new[]
        {
            new { Input = @"{""z"":3,""a"":1,""b"":2}", Expected = @"{""a"":1,""b"":2,""z"":3}" },
            new { Input = @"{""unicode"":""\u03A9""}", Expected = @"{""unicode"":""\u03A9""}" },
            new { Input = @"{""number"":123.456}", Expected = @"{""number"":123.456}" }
        };
        
        foreach (var testCase in testCases)
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(testCase.Input);
            string canonical = CanonicalJsonSerializer.Serialize(parsed);
            
            Assert.AreEqual(testCase.Expected, canonical,
                $"Canonical JSON mismatch for input: {testCase.Input}");
        }
    }
}
```

## Continuous Conformance Testing

### GitHub Actions Integration

```yaml
name: Conformance Testing

on: [push, pull_request]

jobs:
  conformance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
          
      - name: Setup Python for conformance tests
        uses: actions/setup-python@v4
        with:
          python-version: '3.11'
          
      - name: Install conformance test suite
        run: |
          git clone https://github.com/theupdateframework/tuf-conformance
          cd tuf-conformance
          pip install -r requirements.txt
          
      - name: Build TUF .NET conformance CLI
        run: |
          cd examples/TufConformanceCli
          dotnet build --configuration Release
          
      - name: Run conformance tests
        run: |
          cd tuf-conformance
          pytest test_conformance.py \
            --client-runner "dotnet run --project ../examples/TufConformanceCli --configuration Release --" \
            --client-xfails-file ../.tuf-conformance-xfails \
            -v
            
      - name: Upload conformance results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: conformance-results
          path: tuf-conformance/pytest-results.xml
```

### Automated Conformance Monitoring

```csharp
[TestFixture]
public class ConformanceMonitoringTests
{
    [Test, Category("Nightly")]
    public async Task MonitorConformanceDrift()
    {
        // Download latest conformance test suite
        await DownloadLatestConformanceTests();
        
        // Run all conformance tests
        var results = await RunConformanceTests();
        
        // Check for new failures
        var previousResults = LoadPreviousConformanceResults();
        var newFailures = results.Failures.Except(previousResults.Failures).ToList();
        
        if (newFailures.Any())
        {
            var message = $"New conformance failures detected:\n{string.Join("\n", newFailures)}";
            await NotifyDevelopers(message);
        }
        
        // Save current results for next run
        await SaveConformanceResults(results);
    }
}
```

## Custom Conformance Testing

### Repository-Specific Testing

```csharp
public class CustomRepositoryConformanceTests
{
    [Test]
    public async Task TestAgainstProductionRepository()
    {
        // Test against your production TUF repository
        var productionRoot = await LoadProductionTrustedRoot();
        
        var config = new UpdaterConfig(productionRoot, 
            new Uri("https://your-tuf-repo.example.com/metadata/"))
        {
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath()
        };
        
        var updater = new Updater(config);
        
        // Should successfully refresh from production
        await updater.RefreshAsync();
        
        // Test target retrieval
        var targets = await updater.GetAllTargetInfo();
        Assert.IsTrue(targets.Any(), "No targets found in production repository");
        
        // Verify a sample of targets
        var sampleTarget = targets.First();
        var downloaded = await updater.DownloadTarget(
            sampleTarget.Value, sampleTarget.Key, null, null);
        
        Assert.IsNotNull(downloaded.Data);
        Assert.IsTrue(downloaded.Data.Length > 0);
    }
}
```

### Performance Conformance

```csharp
[TestFixture]
public class PerformanceConformanceTests
{
    [Test]
    public async Task MetadataRefreshPerformanceBenchmark()
    {
        var scenario = LoadLargeRepositoryScenario();
        var config = new UpdaterConfig(scenario.TrustedRoot, scenario.MetadataUrl);
        var updater = new Updater(config);
        
        var stopwatch = Stopwatch.StartNew();
        await updater.RefreshAsync();
        stopwatch.Stop();
        
        // Should complete within reasonable time even for large repositories
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, 
            $"Refresh took too long: {stopwatch.ElapsedMilliseconds}ms");
        
        Console.WriteLine($"Metadata refresh completed in {stopwatch.ElapsedMilliseconds}ms");
    }
    
    [Test]
    public async Task LargeTargetDownloadPerformanceBenchmark()
    {
        var scenario = LoadLargeTargetScenario();
        var updater = new Updater(scenario.Config);
        
        await updater.RefreshAsync();
        
        var largeTarget = await updater.GetTargetInfo("large-file.bin"); // 100MB test file
        Assert.IsTrue(largeTarget.HasValue);
        
        var progress = new Progress<(long downloaded, long total)>();
        var stopwatch = Stopwatch.StartNew();
        
        var result = await updater.DownloadTarget(
            largeTarget.Value.targetInfo, largeTarget.Value.path, null, progress);
            
        stopwatch.Stop();
        
        // Should achieve reasonable download performance
        var mbPerSecond = (result.Data.Length / 1_000_000.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
        Console.WriteLine($"Download performance: {mbPerSecond:F2} MB/s");
        
        Assert.IsTrue(mbPerSecond > 10, "Download performance below threshold");
    }
}
```

## Conformance Reporting

### Generating Conformance Reports

```csharp
public class ConformanceReporter
{
    public async Task GenerateConformanceReport()
    {
        var results = await RunAllConformanceTests();
        
        var report = new ConformanceReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            TufDotNetVersion = GetVersion(),
            SpecificationVersion = "1.0.0",
            TestResults = results.ToList()
        };
        
        // Generate HTML report
        var htmlReport = GenerateHtmlReport(report);
        await File.WriteAllTextAsync("conformance-report.html", htmlReport);
        
        // Generate JSON report for CI
        var jsonReport = JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync("conformance-report.json", jsonReport);
        
        Console.WriteLine($"Conformance report generated: {report.PassedTests}/{report.TotalTests} tests passed");
    }
}

public class ConformanceReport
{
    public DateTimeOffset Timestamp { get; set; }
    public string TufDotNetVersion { get; set; } = "";
    public string SpecificationVersion { get; set; } = "";
    public List<TestResult> TestResults { get; set; } = new();
    
    public int TotalTests => TestResults.Count;
    public int PassedTests => TestResults.Count(r => r.Passed);
    public int FailedTests => TestResults.Count(r => !r.Passed);
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests : 0;
}
```

## Troubleshooting Conformance Issues

### Common Conformance Failures

1. **Signature Verification Failures**
   ```csharp
   // Debug signature verification issues
   try
   {
       var isValid = await metadata.VerifySignatureAsync(trustedKeys);
   }
   catch (TufSignatureException ex)
   {
       Console.WriteLine($"Signature verification failed: {ex.Message}");
       Console.WriteLine($"Expected keyids: {string.Join(", ", trustedKeys.Select(k => k.KeyId))}");
       Console.WriteLine($"Metadata keyids: {string.Join(", ", metadata.Signatures.Select(s => s.KeyId))}");
   }
   ```

2. **JSON Canonicalization Issues**
   ```csharp
   // Compare canonical JSON with reference implementation
   var metadata = new Root();
   var canonical = CanonicalJsonSerializer.Serialize(metadata);
   
   // Should match python json.dumps(separators=(',', ':'), sort_keys=True)
   Console.WriteLine($"Canonical JSON: {canonical}");
   ```

3. **Metadata Parsing Failures**
   ```csharp
   try
   {
       var metadata = CanonicalJsonDeserializer.Deserialize<Metadata<Root>>(json);
   }
   catch (JsonException ex)
   {
       Console.WriteLine($"JSON parsing failed: {ex.Message}");
       Console.WriteLine($"Invalid JSON: {json}");
   }
   ```

### Debugging Tools

```bash
# Run specific conformance test with verbose output
cd examples/TufConformanceCli
dotnet run --configuration Release -- \
  --verbose \
  --metadata-url "file://$(pwd)/test-data/metadata/" \
  --trusted-root "$(pwd)/test-data/root.json" \
  --targets-url "file://$(pwd)/test-data/targets/" \
  --target-name "example-target.txt"
  
# Enable detailed logging
export TUF_LOG_LEVEL=Debug
dotnet run --configuration Release -- [arguments]
```

## Best Practices

1. **Run conformance tests regularly**: Include in CI/CD pipeline
2. **Monitor specification updates**: Track changes to TUF specification
3. **Test against multiple scenarios**: Cover success and failure cases
4. **Validate performance**: Ensure reasonable performance characteristics
5. **Document expected failures**: Maintain `.tuf-conformance-xfails` file
6. **Test interoperability**: Validate against other TUF implementations

## Next Steps

- **Interoperability Testing**: See [Interoperability](interoperability.md) for cross-implementation testing
- **Migration Guide**: Use [Migration](migration.md) for moving from other implementations
- **API Documentation**: Check [API Reference](../api/README.md) for detailed usage
- **Security Testing**: Review [Security Model](../security/security-model.md) for security validation

TUF .NET maintains strict conformance with the TUF specification while providing excellent performance and developer experience.