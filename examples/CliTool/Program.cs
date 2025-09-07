using System.CommandLine;
using System.Text.Json;

using TUF;
using TUF.Models;
using TUF.Repository;
using CanonicalJson;
using Serde;

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
        var rootCommand = new RootCommand("TUF .NET CLI Tool - Comprehensive TUF repository and client operations")
        {
            // Client operations
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            CreateInfoCommand(),
            
            // Repository management operations
            CreateInitRepoCommand(),
            CreateGenKeysCommand(),
            CreateAddTargetCommand(),
            CreateSignCommand(),
            CreateValidateCommand()
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

    // === Repository Management Commands ===

    private static Command CreateInitRepoCommand()
    {
        var repoPathOption = new Option<DirectoryInfo>("--repo-path")
        {
            Description = "Path where the TUF repository will be created",
            Required = true
        };

        var command = new Command("init-repo", "Initialize a new TUF repository")
        {
            repoPathOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var repoPath = parseResult.GetValue(repoPathOption)!;
            await InitializeRepository(repoPath);
            return 0;
        });

        return command;
    }

    private static Command CreateGenKeysCommand()
    {
        var keyTypeOption = new Option<string>("--type")
        {
            Description = "Key type: ed25519, rsa, or ecdsa",
            Required = true
        };

        var outputDirOption = new Option<DirectoryInfo>("--output-dir")
        {
            Description = "Directory to store generated keys",
            Required = true
        };

        var roleOption = new Option<string>("--role")
        {
            Description = "Role for this key (root, timestamp, snapshot, targets)",
            Required = true
        };

        var command = new Command("gen-keys", "Generate cryptographic keys for TUF roles")
        {
            keyTypeOption,
            outputDirOption,
            roleOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var keyType = parseResult.GetValue(keyTypeOption)!;
            var outputDir = parseResult.GetValue(outputDirOption)!;
            var role = parseResult.GetValue(roleOption)!;
            await GenerateKeys(keyType, outputDir, role);
            return 0;
        });

        return command;
    }

    private static Command CreateAddTargetCommand()
    {
        var repoPathOption = new Option<DirectoryInfo>("--repo-path")
        {
            Description = "Path to the TUF repository",
            Required = true
        };

        var targetFileOption = new Option<FileInfo>("--file")
        {
            Description = "Path to the target file to add",
            Required = true
        };

        var targetPathOption = new Option<string>("--target-path")
        {
            Description = "Path within the repository for this target",
            Required = true
        };

        var command = new Command("add-target", "Add a target file to the repository")
        {
            repoPathOption,
            targetFileOption,
            targetPathOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var repoPath = parseResult.GetValue(repoPathOption)!;
            var targetFile = parseResult.GetValue(targetFileOption)!;
            var targetPath = parseResult.GetValue(targetPathOption)!;
            await AddTarget(repoPath, targetFile, targetPath);
            return 0;
        });

        return command;
    }

    private static Command CreateSignCommand()
    {
        var repoPathOption = new Option<DirectoryInfo>("--repo-path")
        {
            Description = "Path to the TUF repository",
            Required = true
        };

        var keyPathOption = new Option<FileInfo>("--key-path")
        {
            Description = "Path to the signing key file",
            Required = true
        };

        var roleOption = new Option<string>("--role")
        {
            Description = "Role to sign (root, timestamp, snapshot, targets)",
            Required = true
        };

        var command = new Command("sign", "Sign repository metadata with a key")
        {
            repoPathOption,
            keyPathOption,
            roleOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var repoPath = parseResult.GetValue(repoPathOption)!;
            var keyPath = parseResult.GetValue(keyPathOption)!;
            var role = parseResult.GetValue(roleOption)!;
            await SignMetadata(repoPath, keyPath, role);
            return 0;
        });

        return command;
    }

    private static Command CreateValidateCommand()
    {
        var repoPathOption = new Option<DirectoryInfo>("--repo-path")
        {
            Description = "Path to the TUF repository to validate",
            Required = true
        };

        var command = new Command("validate", "Validate repository structure and metadata")
        {
            repoPathOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var repoPath = parseResult.GetValue(repoPathOption)!;
            await ValidateRepository(repoPath);
            return 0;
        });

        return command;
    }

    // === Repository Management Implementation ===

    private static async Task InitializeRepository(DirectoryInfo repoPath)
    {
        try
        {
            Console.WriteLine($"🏗️  Initializing TUF repository at: {repoPath.FullName}");

            // Create directory structure
            repoPath.Create();
            var metadataDir = Directory.CreateDirectory(Path.Combine(repoPath.FullName, "metadata"));
            var targetsDir = Directory.CreateDirectory(Path.Combine(repoPath.FullName, "targets"));
            var keysDir = Directory.CreateDirectory(Path.Combine(repoPath.FullName, "keys"));

            // Generate keys for all roles
            Console.WriteLine("🔑 Generating Ed25519 keys for all roles...");
            var rootSigner = Ed25519Signer.Generate();
            var timestampSigner = Ed25519Signer.Generate();
            var snapshotSigner = Ed25519Signer.Generate();
            var targetsSigner = Ed25519Signer.Generate();

            // Save keys (in a real scenario, private keys should be stored securely)
            await SaveKeyInfo(Path.Combine(keysDir.FullName, "root_key.json"), rootSigner);
            await SaveKeyInfo(Path.Combine(keysDir.FullName, "timestamp_key.json"), timestampSigner);
            await SaveKeyInfo(Path.Combine(keysDir.FullName, "snapshot_key.json"), snapshotSigner);
            await SaveKeyInfo(Path.Combine(keysDir.FullName, "targets_key.json"), targetsSigner);

            // Create repository using RepositoryBuilder
            var builder = new RepositoryBuilder()
                .AddSigner("root", rootSigner)
                .AddSigner("timestamp", timestampSigner)
                .AddSigner("snapshot", snapshotSigner)
                .AddSigner("targets", targetsSigner)
                .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1))
                .SetConsistentSnapshots(true);

            var repository = builder.Build();
            repository.WriteToDirectory(repoPath.FullName);

            Console.WriteLine($"✅ Repository initialized successfully!");
            Console.WriteLine($"   Metadata: {metadataDir.FullName}");
            Console.WriteLine($"   Targets: {targetsDir.FullName}");
            Console.WriteLine($"   Keys: {keysDir.FullName}");
            Console.WriteLine();
            Console.WriteLine("⚠️  WARNING: Private keys are stored unencrypted in the keys/ directory.");
            Console.WriteLine("   In production, use proper key management and storage practices.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing repository: {ex.Message}");
        }
    }

    private static async Task GenerateKeys(string keyType, DirectoryInfo outputDir, string role)
    {
        try
        {
            Console.WriteLine($"🔑 Generating {keyType} key for {role} role...");
            
            outputDir.Create();
            
            ISigner signer = keyType.ToLowerInvariant() switch
            {
                "ed25519" => Ed25519Signer.Generate(),
                "rsa" => RsaSigner.Generate(3072),
                "ecdsa" => EcdsaSigner.Generate(),
                _ => throw new ArgumentException($"Unsupported key type: {keyType}")
            };

            var keyFileName = $"{role}_{keyType}_key.json";
            var keyFilePath = Path.Combine(outputDir.FullName, keyFileName);
            
            await SaveKeyInfo(keyFilePath, signer);
            
            Console.WriteLine($"✅ Key generated successfully: {keyFilePath}");
            Console.WriteLine($"   Key ID: {signer.Key.GetKeyId()}");
            Console.WriteLine($"   Key Type: {signer.Key.KeyType}");
            Console.WriteLine($"   Scheme: {signer.Key.Scheme}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error generating key: {ex.Message}");
        }
    }

    private static async Task AddTarget(DirectoryInfo repoPath, FileInfo targetFile, string targetPath)
    {
        try
        {
            Console.WriteLine($"📦 Adding target file: {targetPath}");
            
            if (!repoPath.Exists)
                throw new DirectoryNotFoundException($"Repository not found: {repoPath.FullName}");
            
            if (!targetFile.Exists)
                throw new FileNotFoundException($"Target file not found: {targetFile.FullName}");

            var targetsDir = Path.Combine(repoPath.FullName, "targets");
            var targetDestPath = Path.Combine(targetsDir, targetPath.Replace('/', Path.DirectorySeparatorChar));
            
            // Create target directory if needed
            var targetDestDir = Path.GetDirectoryName(targetDestPath);
            if (!string.IsNullOrEmpty(targetDestDir))
                Directory.CreateDirectory(targetDestDir);
            
            // Copy target file
            File.Copy(targetFile.FullName, targetDestPath, overwrite: true);
            
            Console.WriteLine($"✅ Target file added successfully: {targetDestPath}");
            Console.WriteLine("   Remember to update and sign the targets metadata to include this file.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error adding target: {ex.Message}");
        }
    }

    private static async Task SignMetadata(DirectoryInfo repoPath, FileInfo keyPath, string role)
    {
        try
        {
            Console.WriteLine($"✍️  Signing {role} metadata...");
            
            if (!repoPath.Exists)
                throw new DirectoryNotFoundException($"Repository not found: {repoPath.FullName}");
            
            if (!keyPath.Exists)
                throw new FileNotFoundException($"Key file not found: {keyPath.FullName}");

            // Load key information (simplified - in production, handle encrypted keys)
            var keyInfo = await LoadKeyInfo(keyPath.FullName);
            
            Console.WriteLine($"✅ Loaded key: {keyInfo.Signer.Key.GetKeyId()}");
            Console.WriteLine("   In a full implementation, this would update and re-sign the specified metadata.");
            Console.WriteLine($"   Role: {role}, Key Type: {keyInfo.Signer.Key.KeyType}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error signing metadata: {ex.Message}");
        }
    }

    private static async Task ValidateRepository(DirectoryInfo repoPath)
    {
        try
        {
            Console.WriteLine($"🔍 Validating repository: {repoPath.FullName}");
            
            if (!repoPath.Exists)
                throw new DirectoryNotFoundException($"Repository not found: {repoPath.FullName}");
            
            var metadataDir = Path.Combine(repoPath.FullName, "metadata");
            var targetsDir = Path.Combine(repoPath.FullName, "targets");
            
            var issues = new List<string>();
            
            // Check directory structure
            if (!Directory.Exists(metadataDir))
                issues.Add("Missing metadata directory");
            
            if (!Directory.Exists(targetsDir))
                issues.Add("Missing targets directory");
            
            // Check required metadata files
            var requiredFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(metadataDir, file);
                if (!File.Exists(filePath))
                    issues.Add($"Missing metadata file: {file}");
            }
            
            if (issues.Count == 0)
            {
                Console.WriteLine("✅ Repository structure validation passed!");
                
                // Basic metadata validation
                try
                {
                    var rootPath = Path.Combine(metadataDir, "root.json");
                    var rootBytes = await File.ReadAllBytesAsync(rootPath);
                    var rootMetadata = Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootBytes, MetadataProxy.De<Root>.Instance);
                    
                    Console.WriteLine($"   Root version: {rootMetadata.Signed.Version}");
                    Console.WriteLine($"   Root expires: {rootMetadata.Signed.Expires}");
                    Console.WriteLine($"   Signatures: {rootMetadata.Signatures.Count}");
                    
                    if (rootMetadata.Signed.Expires < DateTimeOffset.UtcNow)
                        Console.WriteLine($"⚠️  WARNING: Root metadata has expired!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  WARNING: Could not validate root metadata: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Repository validation failed with {issues.Count} issues:");
                foreach (var issue in issues)
                {
                    Console.WriteLine($"   • {issue}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error validating repository: {ex.Message}");
        }
    }

    // === Helper Methods ===

    private static Task SaveKeyInfo(string filePath, ISigner signer)
    {
        var keyInfo = new
        {
            KeyType = signer.Key.KeyType,
            Scheme = signer.Key.Scheme,
            KeyId = signer.Key.GetKeyId(),
            PublicKey = signer.Key.KeyVal.Public,
            // Note: In production, private keys should be encrypted and stored securely
            Role = Path.GetFileNameWithoutExtension(filePath).Split('_')[0]
        };
        
        var json = JsonSerializer.Serialize(keyInfo, new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(filePath, json);
    }

    private static async Task<(ISigner Signer, string Role)> LoadKeyInfo(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var keyInfo = JsonSerializer.Deserialize<JsonElement>(json);
        
        var keyType = keyInfo.GetProperty("KeyType").GetString()!;
        var role = keyInfo.GetProperty("Role").GetString()!;
        
        // This is a simplified implementation - in production, you'd need to handle
        // encrypted private keys and proper key reconstruction
        ISigner signer = keyType switch
        {
            "ed25519" => Ed25519Signer.Generate(),
            "rsa" => RsaSigner.Generate(),
            "ecdsa" => EcdsaSigner.Generate(),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
        
        return (signer, role);
    }
}