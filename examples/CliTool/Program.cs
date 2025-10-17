using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CanonicalJson;
using TUF;
using TUF.Models;

namespace CliTool;

public class Program
{
    // Shared options to reduce duplication
    private static readonly Option<string> MetadataUrlOption = new("--metadata-url")
    {
        Description = "URL to the TUF metadata repository",
        Required = true
    };

    private static readonly Option<DirectoryInfo> MetadataDirOption = new("--metadata-dir")
    {
        Description = "Local directory to store metadata",
        Required = true
    };

    private static readonly Option<DirectoryInfo> TargetsDirOption = new("--targets-dir")
    {
        Description = "Local directory to store targets",
        Required = true
    };

    private static readonly Option<FileInfo> TrustedRootOption = new("--trusted-root")
    {
        Description = "Path to trusted root metadata file",
        Required = true
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET CLI Tool - Demonstrates TUF client operations")
        {
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            CreateInfoCommand(),
            CreateInspectCommand()
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static Command CreateRefreshCommand()
    {
        var command = new Command("refresh", "Refresh TUF metadata from repository")
        {
            MetadataUrlOption,
            MetadataDirOption,
            TargetsDirOption,
            TrustedRootOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var targetsDir = parseResult.GetValue(TargetsDirOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption)!;

            await RefreshMetadata(metadataUrl, metadataDir, targetsDir, trustedRoot);
            return 0;
        });

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var targetFileOption = new Option<string>("--target-file")
        {
            Description = "Name of the target file to download",
            Required = true
        };

        var command = new Command("download", "Download a target file from TUF repository")
        {
            MetadataUrlOption,
            MetadataDirOption,
            TargetsDirOption,
            TrustedRootOption,
            targetFileOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var targetsDir = parseResult.GetValue(TargetsDirOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption)!;
            var targetFile = parseResult.GetValue(targetFileOption)!;

            await DownloadTarget(metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile);
            return 0;
        });

        return command;
    }

    private static Command CreateInfoCommand()
    {
        var targetFileOption = new Option<string?>("--target-file")
        {
            Description = "Name of the target file to get info about (optional)",
            Required = false
        };

        var command = new Command("info", "Get information about TUF repository or target file")
        {
            MetadataUrlOption,
            MetadataDirOption,
            TargetsDirOption,
            TrustedRootOption,
            targetFileOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var targetsDir = parseResult.GetValue(TargetsDirOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption)!;
            var targetFile = parseResult.GetValue(targetFileOption);

            await ShowInfo(metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile);
            return 0;
        });

        return command;
    }

    private static Command CreateInspectCommand()
    {
        var metadataFileOption = new Option<FileInfo>("--metadata-file")
        {
            Description = "Path to TUF metadata file to inspect",
            Required = true
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Show detailed metadata information including signatures",
            Required = false
        };

        var validateSignaturesOption = new Option<bool>("--validate-signatures")
        {
            Description = "Validate metadata signatures (requires trusted root for non-root metadata)",
            Required = false
        };

        var trustedRootOptionInspect = new Option<FileInfo?>("--trusted-root")
        {
            Description = "Path to trusted root metadata file (required for signature validation of non-root metadata)",
            Required = false
        };

        var command = new Command("inspect", "Inspect and validate TUF metadata files")
        {
            metadataFileOption,
            verboseOption,
            validateSignaturesOption,
            trustedRootOptionInspect
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataFile = parseResult.GetValue(metadataFileOption)!;
            var verbose = parseResult.GetValue(verboseOption);
            var validateSignatures = parseResult.GetValue(validateSignaturesOption);
            var trustedRoot = parseResult.GetValue(trustedRootOptionInspect);

            await InspectMetadata(metadataFile, verbose, validateSignatures, trustedRoot);
            return 0;
        });

        return command;
    }

    private static async Task RefreshMetadata(string metadataUrl, DirectoryInfo metadataDir, DirectoryInfo targetsDir, FileInfo trustedRoot)
    {
        try
        {
            Console.WriteLine("Refreshing TUF metadata...");
            Console.WriteLine($"Metadata URL: {metadataUrl}");
            Console.WriteLine($"Metadata directory: {metadataDir.FullName}");
            Console.WriteLine($"Targets directory: {targetsDir.FullName}");

            metadataDir.Create();
            targetsDir.Create();

            byte[] rootBytes = await File.ReadAllBytesAsync(trustedRoot.FullName);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetsDir.FullName,
                Client = new HttpClient()
            };

            var updater = new Updater(config);
            await updater.RefreshAsync();

            Console.WriteLine("✅ Metadata refresh completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error refreshing metadata: {ex.Message}");
        }
    }

    private static async Task DownloadTarget(string metadataUrl, DirectoryInfo metadataDir, DirectoryInfo targetsDir, FileInfo trustedRoot, string targetFile)
    {
        try
        {
            Console.WriteLine($"Downloading target file: {targetFile}");

            metadataDir.Create();
            targetsDir.Create();

            byte[] rootBytes = await File.ReadAllBytesAsync(trustedRoot.FullName);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetsDir.FullName,
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            Console.WriteLine("Refreshing metadata...");
            await updater.RefreshAsync();

            Console.WriteLine("Getting target information...");
            if (await updater.GetTargetInfo(targetFile) is not (var targetPath, var targetInfo))
            {
                throw new Exception($"Target '{targetFile}' not found in repository.");
            }

            Console.WriteLine($"Target found: {targetPath}");
            Console.WriteLine($"Length: {targetInfo.Length} bytes");

            // Check for cached version first
            var cached = await updater.FindCachedTarget(targetInfo, targetPath, null);
            if (cached.HasValue)
            {
                Console.WriteLine($"✅ Found cached version at: {cached.Value.FilePath}");
            }
            else
            {
                Console.WriteLine("Downloading...");
                var result = await updater.DownloadTarget(targetInfo, targetPath, null, null);
                Console.WriteLine($"✅ Downloaded to: {result.FilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error downloading target: {ex.Message}");
        }
    }

    private static async Task ShowInfo(string metadataUrl, DirectoryInfo metadataDir, DirectoryInfo targetsDir, FileInfo trustedRoot, string? targetFile)
    {
        try
        {
            metadataDir.Create();
            targetsDir.Create();

            byte[] rootBytes = await File.ReadAllBytesAsync(trustedRoot.FullName);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetsDir.FullName,
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            Console.WriteLine("Refreshing metadata...");
            await updater.RefreshAsync();

            var trustedMetadata = updater.GetTrustedMetadataSet();

            Console.WriteLine("=== TUF Repository Information ===");
            Console.WriteLine($"Root version: {trustedMetadata.Root.Signed.Version}");
            Console.WriteLine($"Root expires: {trustedMetadata.Root.Signed.Expires}");
            Console.WriteLine($"Spec version: {trustedMetadata.Root.Signed.SpecVersion}");

            if (targetFile != null)
            {
                Console.WriteLine($"\n=== Target File Information: {targetFile} ===");
                if (await updater.GetTargetInfo(targetFile) is not (var targetPath, var targetInfo))
                {
                    throw new Exception($"Target '{targetFile}' not found in repository.");
                }
                Console.WriteLine($"Path: {targetPath}");
                Console.WriteLine($"Length: {targetInfo.Length} bytes");
                Console.WriteLine("Hashes:");
                foreach (var hash in targetInfo.Hashes)
                {
                    Console.WriteLine($"  {hash.Key}: {hash.Value}");
                }

                if (targetInfo.Custom != null && targetInfo.Custom.Count > 0)
                {
                    Console.WriteLine("Custom metadata:");
                    foreach (var kvp in targetInfo.Custom)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n=== Available Targets ===");
                var targets = updater.GetTopLevelTargets();
                if (targets.Count > 0)
                {
                    foreach (var target in targets)
                    {
                        Console.WriteLine($"  {target.Key} ({target.Value.Length} bytes)");
                    }
                }
                else
                {
                    Console.WriteLine("  No targets found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting repository info: {ex.Message}");
        }
    }

    private static async Task InspectMetadata(FileInfo metadataFile, bool verbose, bool validateSignatures, FileInfo? trustedRoot)
    {
        try
        {
            if (!metadataFile.Exists)
            {
                throw new FileNotFoundException($"Metadata file not found: {metadataFile.FullName}");
            }

            Console.WriteLine($"🔍 Inspecting TUF metadata file: {metadataFile.Name}");
            Console.WriteLine($"File path: {metadataFile.FullName}");
            Console.WriteLine($"File size: {metadataFile.Length} bytes");
            Console.WriteLine($"Last modified: {metadataFile.LastWriteTime}");
            Console.WriteLine();

            // Read and parse the metadata file
            string jsonContent = await File.ReadAllTextAsync(metadataFile.FullName);
            
            // Validate JSON format
            JsonDocument jsonDoc;
            try
            {
                jsonDoc = JsonDocument.Parse(jsonContent);
                Console.WriteLine("✅ JSON format validation: PASSED");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ JSON format validation: FAILED - {ex.Message}");
                return;
            }

            using (jsonDoc)
            {
                // Determine metadata type
                string metadataType = DetermineMetadataType(jsonContent);
                Console.WriteLine($"Metadata type: {metadataType}");

                // Display basic metadata information
                DisplayBasicMetadataInfo(jsonDoc, metadataType);

                if (verbose)
                {
                    Console.WriteLine("\n=== Verbose Details ===");
                    DisplayVerboseMetadataInfo(jsonDoc, metadataType);
                }

                // Basic validation checks
                Console.WriteLine("\n=== Basic Validation ===");
                PerformBasicValidation(jsonDoc, metadataType);

                if (validateSignatures)
                {
                    Console.WriteLine("\n=== Signature Information ===");
                    DisplaySignatureInfo(jsonDoc);
                    Console.WriteLine("⚠️  Full signature validation requires integration with TUF updater workflow");
                }
            }

            Console.WriteLine("\n🎉 Inspection completed!");
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

    private static string DetermineMetadataType(string jsonContent)
    {
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        if (jsonDoc.RootElement.TryGetProperty("signed", out var signedElement) &&
            signedElement.TryGetProperty("_type", out var typeElement))
        {
            return typeElement.GetString() ?? "unknown";
        }
        return "unknown";
    }

    private static void DisplayBasicMetadataInfo(JsonDocument jsonDoc, string metadataType)
    {
        Console.WriteLine($"\n=== {metadataType.ToUpperInvariant()} Metadata Information ===");
        
        var root = jsonDoc.RootElement;
        if (root.TryGetProperty("signed", out var signed))
        {
            if (signed.TryGetProperty("version", out var version))
                Console.WriteLine($"Version: {version.GetInt32()}");
            
            if (signed.TryGetProperty("expires", out var expires))
                Console.WriteLine($"Expires: {expires.GetString()}");
            
            if (signed.TryGetProperty("spec_version", out var specVersion))
                Console.WriteLine($"Spec version: {specVersion.GetString()}");

            // Type-specific information
            switch (metadataType.ToLowerInvariant())
            {
                case "root":
                    if (signed.TryGetProperty("consistent_snapshot", out var consistentSnapshot))
                        Console.WriteLine($"Consistent snapshot: {consistentSnapshot.GetBoolean()}");
                    
                    if (signed.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Object)
                        Console.WriteLine($"Number of keys: {keys.EnumerateObject().Count()}");
                    
                    if (signed.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Object)
                        Console.WriteLine($"Number of roles: {roles.EnumerateObject().Count()}");
                    break;

                case "timestamp":
                    if (signed.TryGetProperty("meta", out var timestampMeta) &&
                        timestampMeta.TryGetProperty("snapshot.json", out var snapshotInfo))
                    {
                        if (snapshotInfo.TryGetProperty("version", out var snapshotVersion))
                            Console.WriteLine($"Snapshot version: {snapshotVersion.GetInt32()}");
                        
                        if (snapshotInfo.TryGetProperty("length", out var snapshotLength))
                            Console.WriteLine($"Snapshot length: {snapshotLength.GetInt64()} bytes");
                    }
                    break;

                case "snapshot":
                    if (signed.TryGetProperty("meta", out var snapshotMeta))
                        Console.WriteLine($"Number of metadata files: {snapshotMeta.EnumerateObject().Count()}");
                    break;

                case "targets":
                    if (signed.TryGetProperty("targets", out var targets))
                        Console.WriteLine($"Number of targets: {targets.EnumerateObject().Count()}");
                    
                    if (signed.TryGetProperty("delegations", out var delegations) &&
                        delegations.TryGetProperty("keys", out var delegationKeys))
                        Console.WriteLine($"Number of delegation keys: {delegationKeys.EnumerateObject().Count()}");
                    break;
            }
        }

        if (root.TryGetProperty("signatures", out var signatures) && signatures.ValueKind == JsonValueKind.Array)
        {
            Console.WriteLine($"Number of signatures: {signatures.GetArrayLength()}");
        }
    }

    private static void DisplayVerboseMetadataInfo(JsonDocument jsonDoc, string metadataType)
    {
        var root = jsonDoc.RootElement;
        if (root.TryGetProperty("signed", out var signed))
        {
            switch (metadataType.ToLowerInvariant())
            {
                case "root":
                    if (signed.TryGetProperty("keys", out var keys))
                    {
                        Console.WriteLine("Keys:");
                        foreach (var key in keys.EnumerateObject())
                        {
                            Console.WriteLine($"  {key.Name}:");
                            if (key.Value.TryGetProperty("keytype", out var keytype))
                                Console.WriteLine($"    Type: {keytype.GetString()}");
                            
                            if (key.Value.TryGetProperty("scheme", out var scheme))
                                Console.WriteLine($"    Scheme: {scheme.GetString()}");
                            
                            if (key.Value.TryGetProperty("keyval", out var keyval) &&
                                keyval.TryGetProperty("public", out var publicKey))
                                Console.WriteLine($"    Public key length: {publicKey.GetString()?.Length ?? 0} chars");
                        }
                    }

                    if (signed.TryGetProperty("roles", out var roles))
                    {
                        Console.WriteLine("Roles:");
                        foreach (var role in roles.EnumerateObject())
                        {
                            Console.WriteLine($"  {role.Name}:");
                            if (role.Value.TryGetProperty("threshold", out var threshold))
                                Console.WriteLine($"    Threshold: {threshold.GetInt32()}");
                            
                            if (role.Value.TryGetProperty("keyids", out var keyids))
                            {
                                var keyIdList = keyids.EnumerateArray().Select(x => x.GetString()).ToArray();
                                Console.WriteLine($"    Key IDs: [{string.Join(", ", keyIdList)}]");
                            }
                        }
                    }
                    break;

                case "targets":
                    if (signed.TryGetProperty("targets", out var targets))
                    {
                        var targetsList = targets.EnumerateObject().Take(10).ToList(); // Show first 10
                        if (targetsList.Count > 0)
                        {
                            Console.WriteLine("Targets:");
                            foreach (var target in targetsList)
                            {
                                Console.WriteLine($"  {target.Name}:");
                                if (target.Value.TryGetProperty("length", out var length))
                                    Console.WriteLine($"    Length: {length.GetInt64()} bytes");
                                
                                if (target.Value.TryGetProperty("hashes", out var hashes))
                                {
                                    var hashAlgorithms = hashes.EnumerateObject().Select(h => h.Name).ToArray();
                                    Console.WriteLine($"    Hashes: {string.Join(", ", hashAlgorithms)}");
                                }
                            }
                            
                            var totalTargets = targets.EnumerateObject().Count();
                            if (totalTargets > 10)
                            {
                                Console.WriteLine($"  ... and {totalTargets - 10} more targets");
                            }
                        }
                    }
                    break;
            }
        }

        // Show signatures for all metadata types
        DisplaySignatureInfo(jsonDoc);
    }

    private static void DisplaySignatureInfo(JsonDocument jsonDoc)
    {
        var root = jsonDoc.RootElement;
        if (root.TryGetProperty("signatures", out var signatures))
        {
            Console.WriteLine("Signatures:");
            int index = 1;
            foreach (var signature in signatures.EnumerateArray())
            {
                Console.WriteLine($"  [{index++}]:");
                if (signature.TryGetProperty("keyid", out var keyId))
                    Console.WriteLine($"    Key ID: {keyId.GetString()}");
                
                if (signature.TryGetProperty("sig", out var sig))
                    Console.WriteLine($"    Signature length: {sig.GetString()?.Length ?? 0} chars");
            }
        }
    }

    private static void PerformBasicValidation(JsonDocument jsonDoc, string metadataType)
    {
        var root = jsonDoc.RootElement;
        bool hasErrors = false;

        // Check required fields
        if (!root.TryGetProperty("signed", out var signed))
        {
            Console.WriteLine("❌ Missing required 'signed' field");
            hasErrors = true;
        }

        if (!root.TryGetProperty("signatures", out var signatures))
        {
            Console.WriteLine("❌ Missing required 'signatures' field");
            hasErrors = true;
        }
        else if (signatures.ValueKind == JsonValueKind.Array && signatures.GetArrayLength() == 0)
        {
            Console.WriteLine("⚠️  No signatures found");
        }

        if (signed.ValueKind != JsonValueKind.Undefined)
        {
            // Check common signed fields
            if (!signed.TryGetProperty("_type", out var typeField))
            {
                Console.WriteLine("❌ Missing required '_type' field in signed section");
                hasErrors = true;
            }
            else if (typeField.GetString()?.ToLowerInvariant() != metadataType.ToLowerInvariant())
            {
                Console.WriteLine($"❌ Type mismatch: expected '{metadataType}', found '{typeField.GetString()}'");
                hasErrors = true;
            }

            if (!signed.TryGetProperty("version", out _))
            {
                Console.WriteLine("❌ Missing required 'version' field");
                hasErrors = true;
            }

            if (!signed.TryGetProperty("expires", out var expires))
            {
                Console.WriteLine("❌ Missing required 'expires' field");
                hasErrors = true;
            }
            else
            {
                // Check if expiration date is in the future
                if (DateTime.TryParse(expires.GetString(), out var expirationDate))
                {
                    if (expirationDate < DateTime.UtcNow)
                    {
                        Console.WriteLine($"⚠️  Metadata has expired: {expirationDate:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                }
            }

            // Type-specific validation
            switch (metadataType.ToLowerInvariant())
            {
                case "root":
                    if (!signed.TryGetProperty("keys", out _))
                    {
                        Console.WriteLine("❌ Root metadata missing required 'keys' field");
                        hasErrors = true;
                    }
                    if (!signed.TryGetProperty("roles", out _))
                    {
                        Console.WriteLine("❌ Root metadata missing required 'roles' field");
                        hasErrors = true;
                    }
                    break;

                case "targets":
                    if (!signed.TryGetProperty("targets", out _))
                    {
                        Console.WriteLine("❌ Targets metadata missing required 'targets' field");
                        hasErrors = true;
                    }
                    break;

                case "snapshot":
                    if (!signed.TryGetProperty("meta", out _))
                    {
                        Console.WriteLine("❌ Snapshot metadata missing required 'meta' field");
                        hasErrors = true;
                    }
                    break;

                case "timestamp":
                    if (!signed.TryGetProperty("meta", out var timestampMeta) ||
                        !timestampMeta.TryGetProperty("snapshot.json", out _))
                    {
                        Console.WriteLine("❌ Timestamp metadata missing required 'meta.snapshot.json' field");
                        hasErrors = true;
                    }
                    break;
            }
        }

        if (!hasErrors)
        {
            Console.WriteLine("✅ Basic structure validation: PASSED");
        }
    }
}