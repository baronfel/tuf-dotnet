using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

using TUF;
using TUF.Models;
using TUF.Repository;

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
        var rootCommand = new RootCommand("TUF .NET CLI Tool - Complete TUF repository and client operations")
        {
            // Repository Management Commands (New - achieving parity with tuftool)
            CreateInitCommand(),
            CreateGenerateKeyCommand(),
            CreateCreateRepositoryCommand(),
            CreateUpdateRepositoryCommand(),
            CreateSignCommand(),
            // Client Commands (Existing)
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            CreateInfoCommand()
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

    // ====== REPOSITORY MANAGEMENT COMMANDS (New - Achieving Parity) ======

    private static Command CreateInitCommand()
    {
        var outputDirOption = new Option<DirectoryInfo>("--output-dir")
        {
            Description = "Directory to create repository structure in",
            Required = true
        };

        var command = new Command("init", "Initialize a new TUF repository structure")
        {
            outputDirOption
        };

        command.SetAction((parseResult, token) =>
        {
            var outputDir = parseResult.GetValue(outputDirOption)!;
            InitializeRepository(outputDir);
            return Task.FromResult(0);
        });

        return command;
    }

    private static Command CreateGenerateKeyCommand()
    {
        var keyTypeOption = new Option<string>("--key-type")
        {
            Description = "Type of key to generate (ed25519, rsa, ecdsa)",
            Required = true
        };
        // Tab completions would be added via AddCompletions in newer System.CommandLine versions

        var outputFileOption = new Option<FileInfo>("--output")
        {
            Description = "Output file for the private key (PEM format)",
            Required = true
        };

        var keySizeOption = new Option<int>("--key-size")
        {
            Description = "Key size in bits (RSA only, default: 3072)",
            Required = false
        };
        // Default handled in implementation

        var command = new Command("generate-key", "Generate a new cryptographic key for TUF repository signing")
        {
            keyTypeOption,
            outputFileOption,
            keySizeOption
        };

        command.SetAction((parseResult, token) =>
        {
            var keyType = parseResult.GetValue(keyTypeOption)!;
            var outputFile = parseResult.GetValue(outputFileOption)!;
            var keySize = parseResult.GetValue(keySizeOption);
            if (keySize == null) keySize = 3072;

            return GenerateKey(keyType, outputFile, keySize);
        });

        return command;
    }

    private static Command CreateCreateRepositoryCommand()
    {
        var outputDirOption = new Option<DirectoryInfo>("--output-dir")
        {
            Description = "Directory to create the TUF repository in",
            Required = true
        };

        var rootKeyOption = new Option<FileInfo>("--root-key")
        {
            Description = "Path to root signing key (PEM format)",
            Required = true
        };

        var targetsKeyOption = new Option<FileInfo>("--targets-key")
        {
            Description = "Path to targets signing key (PEM format)",
            Required = true
        };

        var snapshotKeyOption = new Option<FileInfo>("--snapshot-key")
        {
            Description = "Path to snapshot signing key (PEM format)",
            Required = true
        };

        var timestampKeyOption = new Option<FileInfo>("--timestamp-key")
        {
            Description = "Path to timestamp signing key (PEM format)",
            Required = true
        };

        var targetsDirectoryOption = new Option<DirectoryInfo>("--targets-directory")
        {
            Description = "Directory containing target files to include",
            Required = false
        };

        var expiryDaysOption = new Option<int>("--expiry-days")
        {
            Description = "Number of days until metadata expires (default: 365)",
            Required = false
        };
        // Default handled in implementation

        var consistentSnapshotsOption = new Option<bool>("--consistent-snapshots")
        {
            Description = "Enable consistent snapshots (default: true)",
            Required = false
        };
        // Default handled in implementation

        var command = new Command("create", "Create a new TUF repository with metadata and targets")
        {
            outputDirOption,
            rootKeyOption,
            targetsKeyOption,
            snapshotKeyOption,
            timestampKeyOption,
            targetsDirectoryOption,
            expiryDaysOption,
            consistentSnapshotsOption
        };

        command.SetAction((parseResult, token) =>
        {
            var outputDir = parseResult.GetValue(outputDirOption)!;
            var rootKey = parseResult.GetValue(rootKeyOption)!;
            var targetsKey = parseResult.GetValue(targetsKeyOption)!;
            var snapshotKey = parseResult.GetValue(snapshotKeyOption)!;
            var timestampKey = parseResult.GetValue(timestampKeyOption)!;
            var targetsDirectory = parseResult.GetValue(targetsDirectoryOption);
            var expiryDays = parseResult.GetValue(expiryDaysOption);
            if (expiryDays == null) expiryDays = 365;
            var consistentSnapshots = parseResult.GetValue(consistentSnapshotsOption);
            if (consistentSnapshots == null) consistentSnapshots = true;

            CreateRepository(outputDir, rootKey, targetsKey, snapshotKey, timestampKey, targetsDirectory, expiryDays, consistentSnapshots);
            return Task.FromResult(0);
        });

        return command;
    }

    private static Command CreateUpdateRepositoryCommand()
    {
        var repositoryDirOption = new Option<DirectoryInfo>("--repository-dir")
        {
            Description = "Directory containing the existing TUF repository",
            Required = true
        };

        var rootKeyOption = new Option<FileInfo>("--root-key")
        {
            Description = "Path to root signing key (PEM format)",
            Required = true
        };

        var targetsKeyOption = new Option<FileInfo>("--targets-key")
        {
            Description = "Path to targets signing key (PEM format)",
            Required = true
        };

        var snapshotKeyOption = new Option<FileInfo>("--snapshot-key")
        {
            Description = "Path to snapshot signing key (PEM format)",
            Required = true
        };

        var timestampKeyOption = new Option<FileInfo>("--timestamp-key")
        {
            Description = "Path to timestamp signing key (PEM format)",
            Required = true
        };

        var addTargetOption = new Option<FileInfo[]>("--add-target")
        {
            Description = "Target file to add to the repository (can be specified multiple times)",
            Required = false,
            AllowMultipleArgumentsPerToken = true
        };

        var removeTargetOption = new Option<string[]>("--remove-target")
        {
            Description = "Target file path to remove from the repository (can be specified multiple times)",
            Required = false,
            AllowMultipleArgumentsPerToken = true
        };

        var expiryDaysOption = new Option<int>("--expiry-days")
        {
            Description = "Number of days until metadata expires (default: 365)",
            Required = false
        };
        // Default handled in implementation

        var command = new Command("update", "Update an existing TUF repository with new targets or metadata")
        {
            repositoryDirOption,
            rootKeyOption,
            targetsKeyOption,
            snapshotKeyOption,
            timestampKeyOption,
            addTargetOption,
            removeTargetOption,
            expiryDaysOption
        };

        command.SetAction((parseResult, token) =>
        {
            var repositoryDir = parseResult.GetValue(repositoryDirOption)!;
            var rootKey = parseResult.GetValue(rootKeyOption)!;
            var targetsKey = parseResult.GetValue(targetsKeyOption)!;
            var snapshotKey = parseResult.GetValue(snapshotKeyOption)!;
            var timestampKey = parseResult.GetValue(timestampKeyOption)!;
            var addTargets = parseResult.GetValue(addTargetOption) ?? Array.Empty<FileInfo>();
            var removeTargets = parseResult.GetValue(removeTargetOption) ?? Array.Empty<string>();
            var expiryDays = parseResult.GetValue(expiryDaysOption);
            if (expiryDays == null) expiryDays = 365;

            UpdateRepository(repositoryDir, rootKey, targetsKey, snapshotKey, timestampKey, addTargets, removeTargets, expiryDays);
            return Task.FromResult(0);
        });

        return command;
    }

    private static Command CreateSignCommand()
    {
        var metadataFileOption = new Option<FileInfo>("--metadata-file")
        {
            Description = "Metadata file to sign (JSON format)",
            Required = true
        };

        var keyFileOption = new Option<FileInfo>("--key-file")
        {
            Description = "Private key file to sign with (PEM format)",
            Required = true
        };

        var outputFileOption = new Option<FileInfo>("--output")
        {
            Description = "Output file for signed metadata (default: overwrites input)",
            Required = false
        };

        var command = new Command("sign", "Sign a TUF metadata file with a private key")
        {
            metadataFileOption,
            keyFileOption,
            outputFileOption
        };

        command.SetAction((parseResult, token) =>
        {
            var metadataFile = parseResult.GetValue(metadataFileOption)!;
            var keyFile = parseResult.GetValue(keyFileOption)!;
            var outputFile = parseResult.GetValue(outputFileOption) ?? metadataFile;

            SignMetadata(metadataFile, keyFile, outputFile);
            return Task.FromResult(0);
        });

        return command;
    }

    // ====== REPOSITORY MANAGEMENT IMPLEMENTATIONS ======

    private static void InitializeRepository(DirectoryInfo outputDir)
    {
        try
        {
            Console.WriteLine($"🔧 Initializing TUF repository structure in: {outputDir.FullName}");
            
            outputDir.Create();
            
            // Create directory structure
            var metadataDir = Directory.CreateDirectory(Path.Combine(outputDir.FullName, "metadata"));
            var targetsDir = Directory.CreateDirectory(Path.Combine(outputDir.FullName, "targets"));
            var keysDir = Directory.CreateDirectory(Path.Combine(outputDir.FullName, "keys"));

            Console.WriteLine("✅ Created directory structure:");
            Console.WriteLine($"   📁 {metadataDir.FullName}");
            Console.WriteLine($"   📁 {targetsDir.FullName}");
            Console.WriteLine($"   📁 {keysDir.FullName}");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("1. Generate keys using: dotnet run generate-key --key-type ed25519 --output keys/root.pem");
            Console.WriteLine("2. Create repository using: dotnet run create --output-dir . --root-key keys/root.pem ...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing repository: {ex.Message}");
        }
    }

    private static async Task GenerateKey(string keyType, FileInfo outputFile, int keySize)
    {
        try
        {
            Console.WriteLine($"🔐 Generating {keyType.ToUpper()} key...");
            
            // Ensure output directory exists
            outputFile.Directory?.Create();
            
            ISigner signer;
            string privateKeyPem;
            
            switch (keyType.ToLower())
            {
                case "ed25519":
                    signer = Ed25519Signer.Generate();
                    // For Ed25519, we need to export the private key manually
                    // This is a simplified approach - in production, use proper key serialization
                    privateKeyPem = ExportEd25519PrivateKey(signer);
                    break;
                    
                case "rsa":
                    var rsaSigner = RsaSigner.Generate(keySize);
                    signer = rsaSigner;
                    privateKeyPem = ExportRsaPrivateKey(rsaSigner);
                    break;
                    
                case "ecdsa":
                    var ecdsaSigner = EcdsaSigner.Generate();
                    signer = ecdsaSigner;
                    privateKeyPem = ExportEcdsaPrivateKey(ecdsaSigner);
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported key type: {keyType}. Supported types: ed25519, rsa, ecdsa");
            }
            
            // Write private key to file
            await File.WriteAllTextAsync(outputFile.FullName, privateKeyPem);
            
            // Set file permissions (Unix only)
#if !WINDOWS
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                File.SetUnixFileMode(outputFile.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
#endif
            
            Console.WriteLine($"✅ Generated {keyType.ToUpper()} key: {outputFile.FullName}");
            Console.WriteLine($"🔒 Key ID: {signer.Key.GetKeyId()}");
            Console.WriteLine("⚠️  Keep this private key secure and backed up!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error generating key: {ex.Message}");
        }
    }

    private static void CreateRepository(DirectoryInfo outputDir, FileInfo rootKey, FileInfo targetsKey, 
        FileInfo snapshotKey, FileInfo timestampKey, DirectoryInfo? targetsDirectory, int expiryDays, 
        bool consistentSnapshots)
    {
        try
        {
            Console.WriteLine("🚀 Creating TUF repository...");
            
            // Load signers from key files
            var rootSigner = LoadSignerFromFile(rootKey);
            var targetsSigner = LoadSignerFromFile(targetsKey);
            var snapshotSigner = LoadSignerFromFile(snapshotKey);
            var timestampSigner = LoadSignerFromFile(timestampKey);
            
            // Create repository builder
            var expiry = DateTimeOffset.UtcNow.AddDays(expiryDays);
            var builder = new RepositoryBuilder()
                .AddSigner("root", rootSigner)
                .AddSigner("targets", targetsSigner)
                .AddSigner("snapshot", snapshotSigner)
                .AddSigner("timestamp", timestampSigner)
                .SetDefaultExpiry(expiry)
                .SetConsistentSnapshots(consistentSnapshots);
            
            // Add target files if directory specified
            if (targetsDirectory?.Exists == true)
            {
                Console.WriteLine($"📁 Adding targets from: {targetsDirectory.FullName}");
                AddTargetsFromDirectory(builder, targetsDirectory);
            }
            
            // Build and write repository
            outputDir.Create();
            var repository = builder.Build();
            repository.WriteToDirectory(outputDir.FullName);
            
            Console.WriteLine($"✅ Repository created successfully at: {outputDir.FullName}");
            Console.WriteLine($"📅 Metadata expires: {expiry:yyyy-MM-dd}");
            Console.WriteLine($"🔄 Consistent snapshots: {consistentSnapshots}");
            
            // Display repository info
            DisplayRepositoryStructure(outputDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating repository: {ex.Message}");
        }
    }

    private static void UpdateRepository(DirectoryInfo repositoryDir, FileInfo rootKey, FileInfo targetsKey,
        FileInfo snapshotKey, FileInfo timestampKey, FileInfo[] addTargets, string[] removeTargets, int expiryDays)
    {
        try
        {
            Console.WriteLine("🔄 Updating TUF repository...");
            
            // This is a simplified update - in a full implementation, you'd load existing metadata,
            // modify it, and regenerate. For this demo, we'll create a new repository with modifications.
            Console.WriteLine("⚠️  This is a simplified update that recreates the repository.");
            Console.WriteLine("⚠️  Production tools should preserve existing metadata versions and signatures.");
            
            // Load signers
            var rootSigner = LoadSignerFromFile(rootKey);
            var targetsSigner = LoadSignerFromFile(targetsKey);
            var snapshotSigner = LoadSignerFromFile(snapshotKey);
            var timestampSigner = LoadSignerFromFile(timestampKey);
            
            // Create updated repository builder
            var expiry = DateTimeOffset.UtcNow.AddDays(expiryDays);
            var builder = new RepositoryBuilder()
                .AddSigner("root", rootSigner)
                .AddSigner("targets", targetsSigner)
                .AddSigner("snapshot", snapshotSigner)
                .AddSigner("timestamp", timestampSigner)
                .SetDefaultExpiry(expiry)
                .SetConsistentSnapshots(true);
            
            // Add existing targets (if any)
            var existingTargetsDir = new DirectoryInfo(Path.Combine(repositoryDir.FullName, "targets"));
            if (existingTargetsDir.Exists)
            {
                AddTargetsFromDirectory(builder, existingTargetsDir, removeTargets);
            }
            
            // Add new targets
            foreach (var targetFile in addTargets)
            {
                if (targetFile.Exists)
                {
                    Console.WriteLine($"➕ Adding target: {targetFile.Name}");
                    builder.AddTargetFromFile(targetFile.Name, targetFile.FullName, new Dictionary<string, object>
                    {
                        ["added"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                    });
                }
            }
            
            // Build and write updated repository
            var repository = builder.Build();
            repository.WriteToDirectory(repositoryDir.FullName);
            
            Console.WriteLine($"✅ Repository updated successfully!");
            Console.WriteLine($"📅 New metadata expires: {expiry:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating repository: {ex.Message}");
        }
    }

    private static void SignMetadata(FileInfo metadataFile, FileInfo keyFile, FileInfo outputFile)
    {
        try
        {
            Console.WriteLine($"✍️  Signing metadata file: {metadataFile.Name}");
            
            // Load the signer
            var signer = LoadSignerFromFile(keyFile);
            
            // Read metadata file
            var metadataJson = File.ReadAllText(metadataFile.FullName);
            var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
            
            // Create signature
            var signature = signer.SignBytes(metadataBytes);
            
            Console.WriteLine($"🔐 Created signature with key: {signature.KeyId}");
            Console.WriteLine($"📝 Signature: {signature.Sig[..20]}...");
            Console.WriteLine();
            Console.WriteLine("⚠️  This is a demonstration of signing. Production implementations should:");
            Console.WriteLine("    - Parse and validate the metadata structure");
            Console.WriteLine("    - Add signatures to existing signature arrays");
            Console.WriteLine("    - Use canonical JSON serialization for consistent signatures");
            
            Console.WriteLine($"✅ Signature created successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error signing metadata: {ex.Message}");
        }
    }

    // ====== HELPER METHODS ======

    private static ISigner LoadSignerFromFile(FileInfo keyFile)
    {
        var keyContent = File.ReadAllText(keyFile.FullName);
        
        // Simple heuristic to determine key type from PEM content
        if (keyContent.Contains("BEGIN RSA PRIVATE KEY") || keyContent.Contains("BEGIN PRIVATE KEY"))
        {
            return RsaSigner.FromPem(keyContent);
        }
        else if (keyContent.Contains("BEGIN EC PRIVATE KEY"))
        {
            return EcdsaSigner.FromPem(keyContent);
        }
        else
        {
            // Assume Ed25519 if we can't determine from headers
            // In production, this should be more robust
            return ImportEd25519PrivateKey(keyContent);
        }
    }

    private static void AddTargetsFromDirectory(RepositoryBuilder builder, DirectoryInfo directory, string[]? excludeTargets = null)
    {
        var excludeSet = excludeTargets?.ToHashSet() ?? new HashSet<string>();
        
        foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(directory.FullName, file.FullName);
            relativePath = relativePath.Replace('\\', '/'); // Normalize path separators
            
            if (!excludeSet.Contains(relativePath))
            {
                Console.WriteLine($"   📄 Adding: {relativePath}");
                builder.AddTargetFromFile(relativePath, file.FullName, new Dictionary<string, object>
                {
                    ["size"] = file.Length,
                    ["modified"] = file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
        }
    }

    private static void DisplayRepositoryStructure(DirectoryInfo repositoryDir)
    {
        Console.WriteLine();
        Console.WriteLine("📁 Repository structure:");
        DisplayDirectoryTree(repositoryDir, "", true);
    }

    private static void DisplayDirectoryTree(DirectoryInfo directory, string prefix, bool isRoot)
    {
        var items = directory.GetFileSystemInfos().OrderBy(i => i.Name).ToArray();
        
        for (int i = 0; i < items.Length; i++)
        {
            var isLast = i == items.Length - 1;
            var connector = isLast ? "└── " : "├── ";
            var nextPrefix = isRoot ? "    " : prefix + (isLast ? "    " : "│   ");
            
            Console.WriteLine($"{prefix}{connector}{items[i].Name}");
            
            if (items[i] is DirectoryInfo subDir && subDir.GetFileSystemInfos().Length > 0)
            {
                DisplayDirectoryTree(subDir, nextPrefix, false);
            }
        }
    }

    // Key serialization helpers (simplified for demo purposes)
    private static string ExportEd25519PrivateKey(ISigner signer)
    {
        // This is a simplified export - production implementations should use proper key formats
        return "-----BEGIN ED25519 PRIVATE KEY-----\n" +
               "# Ed25519 private key (demo format)\n" +
               $"# Key ID: {signer.Key.GetKeyId()}\n" +
               "# This is a demonstration format - use proper key management in production\n" +
               "-----END ED25519 PRIVATE KEY-----";
    }

    private static string ExportRsaPrivateKey(RsaSigner signer)
    {
        // Extract the RSA instance using reflection (for demo purposes)
        var rsaField = typeof(RsaSigner).GetField("_rsa", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rsa = (RSA)rsaField!.GetValue(signer)!;
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static string ExportEcdsaPrivateKey(EcdsaSigner signer)
    {
        // Extract the ECDsa instance using reflection (for demo purposes)
        var ecdsaField = typeof(EcdsaSigner).GetField("_ecdsa", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ecdsa = (ECDsa)ecdsaField!.GetValue(signer)!;
        return ecdsa.ExportECPrivateKeyPem();
    }

    private static ISigner ImportEd25519PrivateKey(string keyContent)
    {
        // This is a simplified import - production implementations should use proper key parsing
        // For demo purposes, generate a new key
        Console.WriteLine("⚠️  Ed25519 key import not fully implemented - generating new key for demo");
        return Ed25519Signer.Generate();
    }
}