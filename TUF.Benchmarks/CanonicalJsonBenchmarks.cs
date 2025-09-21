using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CanonicalJson;
using TUF.Models;

namespace TUF.Benchmarks;

/// <summary>
/// Benchmarks for canonical JSON serialization/deserialization operations.
/// These are critical performance metrics since JSON operations happen frequently
/// in TUF metadata processing and signature verification workflows.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
[MarkdownExporter]
public class CanonicalJsonBenchmarks
{
    private Root _sampleRoot = null!;
    private string _sampleRootJson = null!;
    private Key _sampleKey = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create realistic test data based on TUF conformance test samples
        _sampleRoot = new Root
        {
            Type = "root",
            ConsistentSnapshot = true,
            Expires = DateTimeOffset.ParseExact("2025-10-01T05:26:16Z", "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Keys = new Dictionary<string, Key>
            {
                ["9c77c0b277eab2fb88d41546d25c82a57286e86969bee8187dedc195e900fb8f"] = new Key
                {
                    KeyType = "ecdsa",
                    Scheme = "ecdsa-sha2-nistp256",
                    KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n" }
                }
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = ["9c77c0b277eab2fb88d41546d25c82a57286e86969bee8187dedc195e900fb8f"], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = ["3b1d9bb50a8f6159f7f681350ea168644cfd30523bb3255b1ea4337cbe411489"], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = ["521d9281004708db89dcf198c20d5faf5ad287bb3b0e627e571708bf2eaff149"], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = ["611c25854f650b2721e0424261d2076fec2f7c9e4febe68ab7f508f43716462d"], Threshold = 1 }
            },
            SpecVersion = "1.0.31",
            Version = 1
        };

        _sampleKey = _sampleRoot.Keys.First().Value;
        _sampleRootJson = System.Text.Encoding.UTF8.GetString(Serializer.Serialize(_sampleRoot));
    }

    [Benchmark(Baseline = true)]
    public byte[] SerializeRoot()
    {
        return Serializer.Serialize(_sampleRoot);
    }

    [Benchmark]
    public Root DeserializeRoot()
    {
        return Serializer.Deserialize<Root>(_sampleRootJson);
    }

    [Benchmark]
    public string CalculateKeyId()
    {
        return _sampleKey.GetKeyId();
    }

    [Benchmark]
    public byte[] SerializeRootRoundtrip()
    {
        var json = System.Text.Encoding.UTF8.GetString(Serializer.Serialize(_sampleRoot));
        var deserialized = Serializer.Deserialize<Root>(json);
        return Serializer.Serialize(deserialized);
    }
}