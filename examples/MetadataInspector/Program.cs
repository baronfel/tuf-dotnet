using System.CommandLine;
using System.Text.Json;
using CanonicalJson;

namespace MetadataInspector;

/// <summary>
/// TUF Metadata Inspector - Advanced debugging and analysis tool for TUF metadata files.
/// This tool provides comprehensive inspection capabilities for TUF metadata files,
/// supporting validation, format analysis, and debugging workflows.
/// </summary>
public class Program
{
    private static readonly Option<FileInfo> MetadataFileOption = new("--file", "-f")
    {
        Description = "Path to the TUF metadata file to inspect",
        Required = true
    };

    private static readonly Option<DirectoryInfo> MetadataDirOption = new("--metadata-dir", "-d")
    {
        Description = "Directory containing TUF metadata files"
    };

    private static readonly Option<bool> VerboseOption = new("--verbose", "-v")
    {
        Description = "Enable verbose output with detailed analysis"
    };

    private static readonly Option<string> FormatOption = new("--format")
    {
        Description = "Output format (text, json, summary)"
    };

    private static readonly Option<bool> ValidateSignaturesOption = new("--validate-signatures")
    {
        Description = "Validate cryptographic signatures (requires additional metadata)"
    };

    private static readonly Option<FileInfo> TrustedRootOption = new("--trusted-root")
    {
        Description = "Path to trusted root metadata for signature validation"
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF Metadata Inspector - Advanced debugging and analysis tool for TUF metadata")
        {
            CreateInspectCommand(),
            CreateValidateCommand(),
            CreateCompareCommand(),
            CreateAnalyzeCommand(),
            CreateExportCommand()
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static Command CreateInspectCommand()
    {
        var command = new Command("inspect", "Inspect and analyze a TUF metadata file")
        {
            MetadataFileOption,
            VerboseOption,
            FormatOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataFile = parseResult.GetValue(MetadataFileOption)!;
            var verbose = parseResult.GetValue(VerboseOption);
            var format = parseResult.GetValue(FormatOption) ?? "text";

            await InspectMetadata(metadataFile, verbose, format);
        });

        return command;
    }

    private static Command CreateValidateCommand()
    {
        var command = new Command("validate", "Validate TUF metadata file structure and signatures")
        {
            MetadataFileOption,
            TrustedRootOption,
            ValidateSignaturesOption,
            VerboseOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataFile = parseResult.GetValue(MetadataFileOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption);
            var validateSignatures = parseResult.GetValue(ValidateSignaturesOption);
            var verbose = parseResult.GetValue(VerboseOption);

            await ValidateMetadata(metadataFile, trustedRoot, validateSignatures, verbose);
        });

        return command;
    }

    private static Command CreateCompareCommand()
    {
        var file1Option = new Option<FileInfo>("--file1")
        {
            Description = "First metadata file to compare",
            Required = true
        };
        
        var file2Option = new Option<FileInfo>("--file2")
        {
            Description = "Second metadata file to compare",
            Required = true
        };

        var command = new Command("compare", "Compare two TUF metadata files")
        {
            file1Option,
            file2Option,
            VerboseOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var file1 = parseResult.GetValue(file1Option)!;
            var file2 = parseResult.GetValue(file2Option)!;
            var verbose = parseResult.GetValue(VerboseOption);

            await CompareMetadata(file1, file2, verbose);
        });

        return command;
    }

    private static Command CreateAnalyzeCommand()
    {
        var command = new Command("analyze", "Perform comprehensive analysis of TUF metadata directory")
        {
            MetadataDirOption,
            VerboseOption,
            FormatOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            var verbose = parseResult.GetValue(VerboseOption);
            var format = parseResult.GetValue(FormatOption) ?? "text";

            await AnalyzeRepository(metadataDir, verbose, format);
        });

        return command;
    }

    private static Command CreateExportCommand()
    {
        var outputFileOption = new Option<FileInfo>("--output", "-o")
        {
            Description = "Output file path",
            Required = true
        };

        var command = new Command("export", "Export metadata information in various formats")
        {
            MetadataFileOption,
            outputFileOption,
            FormatOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataFile = parseResult.GetValue(MetadataFileOption)!;
            var outputFile = parseResult.GetValue(outputFileOption)!;
            var format = parseResult.GetValue(FormatOption) ?? "json";

            await ExportMetadata(metadataFile, outputFile, format);
        });

        return command;
    }

    private static async Task InspectMetadata(FileInfo metadataFile, bool verbose, string format)
    {
        try
        {
            Console.WriteLine($"🔍 Inspecting TUF metadata file: {metadataFile.Name}");
            Console.WriteLine($"📁 Path: {metadataFile.FullName}");
            Console.WriteLine($"📊 Size: {metadataFile.Length:N0} bytes");
            Console.WriteLine($"📅 Modified: {metadataFile.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();

            var jsonContent = await File.ReadAllTextAsync(metadataFile.FullName);
            
            // Parse as generic JSON first to determine structure
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Analyze JSON structure
            AnalyzeJsonStructure(root, verbose);

            // Determine metadata type
            string? metadataType = null;
            if (root.TryGetProperty("signed", out var signedElement) && 
                signedElement.TryGetProperty("_type", out var typeElement))
            {
                metadataType = typeElement.GetString();
                Console.WriteLine($"📋 Metadata Type: {metadataType?.ToUpperInvariant()}");

                // Analyze specific metadata type
                AnalyzeSpecificMetadata(signedElement, metadataType, verbose);
            }

            // Validate canonical JSON format
            ValidateCanonicalJson(jsonContent, verbose);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error inspecting metadata: {ex.Message}");
            if (verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }

    private static void AnalyzeJsonStructure(JsonElement root, bool verbose)
    {
        Console.WriteLine("📊 JSON Structure Analysis:");

        // Top-level properties
        var properties = new List<string>();
        foreach (var prop in root.EnumerateObject())
        {
            properties.Add(prop.Name);
        }
        Console.WriteLine($"   Top-level properties: {string.Join(", ", properties)}");

        // Signatures analysis
        if (root.TryGetProperty("signatures", out var signatures) && signatures.ValueKind == JsonValueKind.Array)
        {
            Console.WriteLine($"   Number of signatures: {signatures.GetArrayLength()}");
            
            if (verbose)
            {
                int i = 0;
                foreach (var sig in signatures.EnumerateArray())
                {
                    if (sig.TryGetProperty("keyid", out var keyid))
                    {
                        var keyIdStr = keyid.GetString();
                        var truncated = keyIdStr?.Length > 16 ? keyIdStr[..16] + "..." : keyIdStr;
                        Console.WriteLine($"     Signature {i}: {truncated}");
                    }
                    i++;
                }
            }
        }

        // Signed content analysis
        if (root.TryGetProperty("signed", out var signed))
        {
            Console.WriteLine($"   Signed content size: ~{JsonSerializer.Serialize(signed).Length} bytes");
            
            if (verbose && signed.TryGetProperty("expires", out var expires))
            {
                Console.WriteLine($"   Expires: {expires.GetString()}");
            }
            
            if (verbose && signed.TryGetProperty("version", out var version))
            {
                Console.WriteLine($"   Version: {version.GetInt32()}");
            }
        }
    }

    private static void AnalyzeSpecificMetadata(JsonElement signed, string? type, bool verbose)
    {
        Console.WriteLine($"\n🔬 {type?.ToUpperInvariant()} Metadata Analysis:");

        try
        {
            // Common fields
            if (signed.TryGetProperty("spec_version", out var specVersion))
                Console.WriteLine($"   Spec Version: {specVersion.GetString()}");
            
            if (signed.TryGetProperty("version", out var version))
                Console.WriteLine($"   Version: {version.GetInt32()}");
            
            if (signed.TryGetProperty("expires", out var expires))
            {
                var expiryStr = expires.GetString();
                var isExpired = DateTime.TryParse(expiryStr, out var expiryDate) && expiryDate < DateTime.UtcNow;
                Console.WriteLine($"   Expires: {expiryStr} {(isExpired ? "⚠️ EXPIRED" : "✅")}");
            }

            switch (type?.ToLower())
            {
                case "root":
                    AnalyzeRootMetadata(signed, verbose);
                    break;
                
                case "timestamp":
                    AnalyzeTimestampMetadata(signed, verbose);
                    break;
                
                case "snapshot":
                    AnalyzeSnapshotMetadata(signed, verbose);
                    break;
                
                case "targets":
                    AnalyzeTargetsMetadata(signed, verbose);
                    break;
                
                default:
                    Console.WriteLine($"   ⚠️  Unknown metadata type: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Failed to parse as {type} metadata: {ex.Message}");
        }
    }

    private static void AnalyzeRootMetadata(JsonElement root, bool verbose)
    {
        if (root.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Object)
        {
            Console.WriteLine($"   Number of keys: {keys.EnumerateObject().Count()}");
        }

        if (root.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Object)
        {
            Console.WriteLine($"   Number of roles: {roles.EnumerateObject().Count()}");

            if (verbose)
            {
                Console.WriteLine("\n   📋 Roles:");
                foreach (var role in roles.EnumerateObject())
                {
                    var roleData = role.Value;
                    var threshold = roleData.TryGetProperty("threshold", out var th) ? th.GetInt32() : 0;
                    var keyCount = 0;
                    if (roleData.TryGetProperty("keyids", out var keyids) && keyids.ValueKind == JsonValueKind.Array)
                    {
                        keyCount = keyids.GetArrayLength();
                    }
                    Console.WriteLine($"     {role.Name}: threshold={threshold}, keyids={keyCount}");
                }

                if (keys.ValueKind == JsonValueKind.Object)
                {
                    Console.WriteLine("\n   🔑 Keys:");
                    foreach (var key in keys.EnumerateObject())
                    {
                        var keyData = key.Value;
                        var keyType = keyData.TryGetProperty("keytype", out var kt) ? kt.GetString() : "unknown";
                        var scheme = keyData.TryGetProperty("scheme", out var sc) ? sc.GetString() : "unknown";
                        var keyId = key.Name.Length > 16 ? key.Name[..16] + "..." : key.Name;
                        Console.WriteLine($"     {keyId}: {keyType} ({scheme})");
                    }
                }
            }
        }
    }

    private static void AnalyzeTimestampMetadata(JsonElement timestamp, bool verbose)
    {
        if (timestamp.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            if (meta.TryGetProperty("snapshot.json", out var snapshotInfo))
            {
                if (snapshotInfo.TryGetProperty("version", out var version))
                    Console.WriteLine($"   Snapshot version: {version.GetInt32()}");
                
                if (snapshotInfo.TryGetProperty("length", out var length))
                    Console.WriteLine($"   Snapshot length: {length.GetInt32()} bytes");

                if (verbose && snapshotInfo.TryGetProperty("hashes", out var hashes) && hashes.ValueKind == JsonValueKind.Object)
                {
                    Console.WriteLine("   Snapshot hashes:");
                    foreach (var hash in hashes.EnumerateObject())
                    {
                        Console.WriteLine($"     {hash.Name}: {hash.Value.GetString()}");
                    }
                }
            }
        }
    }

    private static void AnalyzeSnapshotMetadata(JsonElement snapshot, bool verbose)
    {
        if (snapshot.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            Console.WriteLine($"   Number of metadata files: {meta.EnumerateObject().Count()}");

            if (verbose)
            {
                Console.WriteLine("\n   📋 Metadata files:");
                foreach (var metaFile in meta.EnumerateObject())
                {
                    var fileData = metaFile.Value;
                    var version = fileData.TryGetProperty("version", out var v) ? v.GetInt32() : 0;
                    var length = fileData.TryGetProperty("length", out var l) ? l.GetInt32() : 0;
                    Console.WriteLine($"     {metaFile.Name}: v{version} ({length} bytes)");
                }
            }
        }
    }

    private static void AnalyzeTargetsMetadata(JsonElement targets, bool verbose)
    {
        int targetCount = 0;
        int delegationCount = 0;

        if (targets.TryGetProperty("targets", out var targetFiles) && targetFiles.ValueKind == JsonValueKind.Object)
        {
            targetCount = targetFiles.EnumerateObject().Count();
            Console.WriteLine($"   Number of targets: {targetCount}");

            if (verbose && targetCount > 0)
            {
                Console.WriteLine("\n   🎯 Target files:");
                var displayCount = Math.Min(10, targetCount); // Limit to first 10 for readability
                var index = 0;
                foreach (var target in targetFiles.EnumerateObject())
                {
                    if (index >= displayCount) break;
                    
                    var targetData = target.Value;
                    var length = targetData.TryGetProperty("length", out var l) ? l.GetInt32() : 0;
                    Console.WriteLine($"     {target.Name}: {length} bytes");
                    
                    if (targetData.TryGetProperty("hashes", out var hashes) && hashes.ValueKind == JsonValueKind.Object)
                    {
                        var hashTypes = hashes.EnumerateObject().Select(h => h.Name);
                        Console.WriteLine($"       Hashes: {string.Join(", ", hashTypes)}");
                    }
                    index++;
                }
                
                if (targetCount > displayCount)
                {
                    Console.WriteLine($"     ... and {targetCount - displayCount} more targets");
                }
            }
        }

        if (targets.TryGetProperty("delegations", out var delegations) && delegations.ValueKind == JsonValueKind.Object)
        {
            if (delegations.TryGetProperty("roles", out var delegationRoles) && delegationRoles.ValueKind == JsonValueKind.Array)
            {
                delegationCount = delegationRoles.GetArrayLength();
                Console.WriteLine($"   Number of delegations: {delegationCount}");

                if (verbose && delegationCount > 0)
                {
                    Console.WriteLine("\n   👥 Delegations:");
                    foreach (var delegation in delegationRoles.EnumerateArray())
                    {
                        var name = delegation.TryGetProperty("name", out var n) ? n.GetString() : "unknown";
                        var threshold = delegation.TryGetProperty("threshold", out var th) ? th.GetInt32() : 0;
                        var keyIdCount = 0;
                        if (delegation.TryGetProperty("keyids", out var keyids) && keyids.ValueKind == JsonValueKind.Array)
                        {
                            keyIdCount = keyids.GetArrayLength();
                        }

                        string pathsInfo = "no paths";
                        if (delegation.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
                        {
                            pathsInfo = $"paths={paths.GetArrayLength()}";
                        }
                        else if (delegation.TryGetProperty("path_hash_prefixes", out var pathHashPrefixes) && pathHashPrefixes.ValueKind == JsonValueKind.Array)
                        {
                            pathsInfo = $"path_hash_prefixes={pathHashPrefixes.GetArrayLength()}";
                        }

                        Console.WriteLine($"     {name}: threshold={threshold}, keyids={keyIdCount}, {pathsInfo}");
                    }
                }
            }
        }
    }

    private static void ValidateCanonicalJson(string jsonContent, bool verbose)
    {
        Console.WriteLine("\n📐 Canonical JSON Validation:");
        
        try
        {
            // Parse and check basic JSON validity
            var parsed = JsonDocument.Parse(jsonContent);
            Console.WriteLine("   ✅ Valid JSON structure");
            
            // Check for canonical JSON characteristics
            var isCompact = !jsonContent.Contains('\n') && !jsonContent.Contains("  ");
            Console.WriteLine($"   Compact format (no extra whitespace): {(isCompact ? "✅ Yes" : "❌ No")}");
            
            // Basic canonical JSON checks
            var hasTrailingComma = jsonContent.Contains(",}") || jsonContent.Contains(",]");
            Console.WriteLine($"   No trailing commas: {(!hasTrailingComma ? "✅ Yes" : "❌ No")}");
            
            if (verbose)
            {
                Console.WriteLine($"   Original length: {jsonContent.Length}");
                Console.WriteLine($"   Character encoding: UTF-8");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ JSON validation failed: {ex.Message}");
        }
    }

    private static async Task ValidateMetadata(FileInfo metadataFile, FileInfo? trustedRoot, bool validateSignatures, bool verbose)
    {
        try
        {
            Console.WriteLine($"🔐 Validating TUF metadata file: {metadataFile.Name}");
            
            var jsonContent = await File.ReadAllTextAsync(metadataFile.FullName);
            var jsonDoc = JsonDocument.Parse(jsonContent);
            
            // Basic structure validation
            Console.WriteLine("\n📋 Structure Validation:");
            ValidateBasicStructure(jsonDoc.RootElement);
            
            // Signature validation if requested
            if (validateSignatures && trustedRoot != null)
            {
                Console.WriteLine("\n🔏 Signature Validation:");
                await ValidateSignatures(jsonContent, trustedRoot, verbose);
            }
            else if (validateSignatures)
            {
                Console.WriteLine("\n⚠️  Signature validation requested but no trusted root provided");
            }
            
            Console.WriteLine("\n✅ Validation completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Validation failed: {ex.Message}");
            if (verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }

    private static void ValidateBasicStructure(JsonElement root)
    {
        // Check required top-level properties
        var requiredProps = new[] { "signed", "signatures" };
        foreach (var prop in requiredProps)
        {
            if (root.TryGetProperty(prop, out _))
            {
                Console.WriteLine($"   ✅ '{prop}' property present");
            }
            else
            {
                Console.WriteLine($"   ❌ Missing required '{prop}' property");
            }
        }
        
        // Check signatures array
        if (root.TryGetProperty("signatures", out var signatures))
        {
            if (signatures.ValueKind == JsonValueKind.Array)
            {
                var sigCount = signatures.GetArrayLength();
                Console.WriteLine($"   ✅ Signatures array has {sigCount} signature(s)");
                
                foreach (var sig in signatures.EnumerateArray())
                {
                    if (!sig.TryGetProperty("keyid", out _))
                    {
                        Console.WriteLine("   ❌ Signature missing 'keyid' property");
                    }
                    if (!sig.TryGetProperty("sig", out _))
                    {
                        Console.WriteLine("   ❌ Signature missing 'sig' property");
                    }
                }
            }
            else
            {
                Console.WriteLine("   ❌ 'signatures' must be an array");
            }
        }
        
        // Check signed content
        if (root.TryGetProperty("signed", out var signed))
        {
            var requiredSignedProps = new[] { "_type", "spec_version", "version", "expires" };
            foreach (var prop in requiredSignedProps)
            {
                if (signed.TryGetProperty(prop, out _))
                {
                    Console.WriteLine($"   ✅ Signed content has '{prop}' property");
                }
                else
                {
                    Console.WriteLine($"   ❌ Signed content missing required '{prop}' property");
                }
            }
        }
    }

    private static async Task ValidateSignatures(string jsonContent, FileInfo trustedRoot, bool verbose)
    {
        try
        {
            Console.WriteLine("   ⚠️  Full cryptographic signature validation requires TUF client implementation");
            Console.WriteLine($"   📁 Trusted root file: {trustedRoot.FullName}");
            
            if (trustedRoot.Exists)
            {
                var rootContent = await File.ReadAllTextAsync(trustedRoot.FullName);
                var rootDoc = JsonDocument.Parse(rootContent);
                
                if (rootDoc.RootElement.TryGetProperty("signed", out var rootSigned) &&
                    rootSigned.TryGetProperty("keys", out var keys))
                {
                    Console.WriteLine($"   🔑 Available keys in root: {keys.EnumerateObject().Count()}");
                    
                    if (verbose)
                    {
                        foreach (var key in keys.EnumerateObject())
                        {
                            var keyData = key.Value;
                            var keyType = keyData.TryGetProperty("keytype", out var kt) ? kt.GetString() : "unknown";
                            var keyId = key.Name.Length > 16 ? key.Name[..16] + "..." : key.Name;
                            Console.WriteLine($"     Key {keyId}: {keyType}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("   ❌ Trusted root file does not exist");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Signature validation error: {ex.Message}");
        }
    }

    private static async Task CompareMetadata(FileInfo file1, FileInfo file2, bool verbose)
    {
        try
        {
            Console.WriteLine($"📊 Comparing metadata files:");
            Console.WriteLine($"   File 1: {file1.Name}");
            Console.WriteLine($"   File 2: {file2.Name}");
            
            var content1 = await File.ReadAllTextAsync(file1.FullName);
            var content2 = await File.ReadAllTextAsync(file2.FullName);
            
            var doc1 = JsonDocument.Parse(content1);
            var doc2 = JsonDocument.Parse(content2);
            
            // Compare basic properties
            CompareBasicProperties(doc1.RootElement, doc2.RootElement);
            
            // Compare signatures
            CompareSignatures(doc1.RootElement, doc2.RootElement);
            
            // Compare signed content
            CompareSignedContent(doc1.RootElement, doc2.RootElement, verbose);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Comparison failed: {ex.Message}");
        }
    }

    private static void CompareBasicProperties(JsonElement root1, JsonElement root2)
    {
        Console.WriteLine("\n📋 Basic Properties Comparison:");
        
        var signed1 = root1.GetProperty("signed");
        var signed2 = root2.GetProperty("signed");
        
        // Compare type
        var type1 = signed1.GetProperty("_type").GetString();
        var type2 = signed2.GetProperty("_type").GetString();
        Console.WriteLine($"   Type: {type1} vs {type2} {(type1 == type2 ? "✅" : "❌")}");
        
        // Compare version
        if (signed1.TryGetProperty("version", out var version1) && 
            signed2.TryGetProperty("version", out var version2))
        {
            var v1 = version1.GetInt32();
            var v2 = version2.GetInt32();
            Console.WriteLine($"   Version: {v1} vs {v2} {(v1 == v2 ? "✅" : (v1 > v2 ? "📈 (newer)" : "📉 (older)"))}");
        }
        
        // Compare expiration
        if (signed1.TryGetProperty("expires", out var expires1) && 
            signed2.TryGetProperty("expires", out var expires2))
        {
            var exp1 = expires1.GetString();
            var exp2 = expires2.GetString();
            Console.WriteLine($"   Expires: {exp1} vs {exp2} {(exp1 == exp2 ? "✅" : "❌")}");
        }
    }

    private static void CompareSignatures(JsonElement root1, JsonElement root2)
    {
        Console.WriteLine("\n🔏 Signatures Comparison:");
        
        var sig1 = root1.GetProperty("signatures");
        var sig2 = root2.GetProperty("signatures");
        
        var count1 = sig1.GetArrayLength();
        var count2 = sig2.GetArrayLength();
        
        Console.WriteLine($"   Signature count: {count1} vs {count2} {(count1 == count2 ? "✅" : "❌")}");
        
        // Get keyids from both
        var keyids1 = new HashSet<string>();
        var keyids2 = new HashSet<string>();
        
        foreach (var sig in sig1.EnumerateArray())
        {
            if (sig.TryGetProperty("keyid", out var keyid))
            {
                keyids1.Add(keyid.GetString() ?? "");
            }
        }
        
        foreach (var sig in sig2.EnumerateArray())
        {
            if (sig.TryGetProperty("keyid", out var keyid))
            {
                keyids2.Add(keyid.GetString() ?? "");
            }
        }
        
        var commonKeys = keyids1.Intersect(keyids2);
        var onlyIn1 = keyids1.Except(keyids2);
        var onlyIn2 = keyids2.Except(keyids1);
        
        Console.WriteLine($"   Common key IDs: {commonKeys.Count()}");
        Console.WriteLine($"   Only in file 1: {onlyIn1.Count()}");
        Console.WriteLine($"   Only in file 2: {onlyIn2.Count()}");
    }

    private static void CompareSignedContent(JsonElement root1, JsonElement root2, bool verbose)
    {
        Console.WriteLine("\n📄 Signed Content Comparison:");
        
        var signed1 = root1.GetProperty("signed");
        var signed2 = root2.GetProperty("signed");
        
        var serialized1 = JsonSerializer.Serialize(signed1);
        var serialized2 = JsonSerializer.Serialize(signed2);
        
        var identical = serialized1 == serialized2;
        Console.WriteLine($"   Signed content identical: {(identical ? "✅" : "❌")}");
        
        if (!identical && verbose)
        {
            Console.WriteLine($"   Content 1 length: {serialized1.Length}");
            Console.WriteLine($"   Content 2 length: {serialized2.Length}");
        }
    }

    private static async Task AnalyzeRepository(DirectoryInfo? metadataDir, bool verbose, string format)
    {
        if (metadataDir == null)
        {
            Console.WriteLine("❌ No metadata directory specified");
            return;
        }
        
        if (!metadataDir.Exists)
        {
            Console.WriteLine($"❌ Metadata directory does not exist: {metadataDir.FullName}");
            return;
        }
        
        Console.WriteLine($"🔍 Analyzing TUF repository: {metadataDir.FullName}");
        
        var metadataFiles = metadataDir.GetFiles("*.json");
        Console.WriteLine($"📁 Found {metadataFiles.Length} JSON files");
        
        var analysis = new Dictionary<string, object>();
        
        foreach (var file in metadataFiles)
        {
            try
            {
                Console.WriteLine($"\n📄 Analyzing {file.Name}:");
                
                var content = await File.ReadAllTextAsync(file.FullName);
                var doc = JsonDocument.Parse(content);
                
                if (doc.RootElement.TryGetProperty("signed", out var signed) &&
                    signed.TryGetProperty("_type", out var typeElement))
                {
                    var type = typeElement.GetString();
                    Console.WriteLine($"   Type: {type}");
                    
                    if (signed.TryGetProperty("version", out var version))
                    {
                        Console.WriteLine($"   Version: {version.GetInt32()}");
                    }
                    
                    if (signed.TryGetProperty("expires", out var expires))
                    {
                        var expiryDate = DateTime.Parse(expires.GetString() ?? "");
                        var isExpired = expiryDate < DateTime.UtcNow;
                        Console.WriteLine($"   Expires: {expires.GetString()} {(isExpired ? "⚠️ EXPIRED" : "✅")}");
                    }
                    
                    // Store for summary analysis
                    analysis[file.Name] = new { Type = type, File = file.Name, Size = file.Length };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Error analyzing {file.Name}: {ex.Message}");
            }
        }
        
        // Repository health summary
        Console.WriteLine("\n🏥 Repository Health Summary:");
        var requiredFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        
        foreach (var required in requiredFiles)
        {
            var exists = metadataFiles.Any(f => f.Name.Equals(required, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"   {required}: {(exists ? "✅ Present" : "❌ Missing")}");
        }
    }

    private static async Task ExportMetadata(FileInfo metadataFile, FileInfo outputFile, string format)
    {
        try
        {
            Console.WriteLine($"📤 Exporting metadata to {format.ToUpper()} format");
            Console.WriteLine($"   Input: {metadataFile.Name}");
            Console.WriteLine($"   Output: {outputFile.Name}");
            
            var content = await File.ReadAllTextAsync(metadataFile.FullName);
            var doc = JsonDocument.Parse(content);
            
            string exportContent = format.ToLower() switch
            {
                "json" => JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }),
                "compact" => JsonSerializer.Serialize(doc),
                "summary" => GenerateSummaryExport(doc),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
            
            await File.WriteAllTextAsync(outputFile.FullName, exportContent);
            Console.WriteLine("✅ Export completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Export failed: {ex.Message}");
        }
    }

    private static string GenerateSummaryExport(JsonDocument doc)
    {
        var summary = new
        {
            AnalyzedAt = DateTime.UtcNow,
            FileSize = JsonSerializer.Serialize(doc).Length,
            Structure = new
            {
                HasSignatures = doc.RootElement.TryGetProperty("signatures", out var sigs) && sigs.ValueKind == JsonValueKind.Array,
                SignatureCount = doc.RootElement.TryGetProperty("signatures", out var sigsCount) ? sigsCount.GetArrayLength() : 0,
                HasSigned = doc.RootElement.TryGetProperty("signed", out _),
            },
            Metadata = doc.RootElement.TryGetProperty("signed", out var signed) ? new
            {
                Type = signed.TryGetProperty("_type", out var type) ? type.GetString() : null,
                Version = signed.TryGetProperty("version", out var version) ? version.GetInt32() : (int?)null,
                Expires = signed.TryGetProperty("expires", out var expires) ? expires.GetString() : null,
                SpecVersion = signed.TryGetProperty("spec_version", out var spec) ? spec.GetString() : null,
            } : null
        };
        
        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }
}