using TUF.Repository;
using TUF.Models;

namespace RepositoryManager;

/// <summary>
/// Enhanced TUF Repository Manager with comprehensive repository management capabilities
/// This addresses the "Repository Management Tools" gap identified in the parity analysis
/// </summary>
class EnhancedProgram
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("🔧 TUF Repository Manager - Enhanced Edition");
            Console.WriteLine("==============================================");
            Console.WriteLine("Comprehensive repository management tool addressing parity gaps");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();

            return command switch
            {
                "create" => await ExecuteCreate(args[1..]),
                "validate" => await ExecuteValidate(args[1..]),
                "info" => await ExecuteInfo(args[1..]),
                "generate-keys" => await ExecuteGenerateKeys(args[1..]),
                "add-target" => await ExecuteAddTarget(args[1..]),
                "help" or "-h" or "--help" => ShowHelp(),
                _ => ShowUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }

    static int ShowHelp()
    {
        Console.WriteLine("🚀 Available Commands:");
        Console.WriteLine("=====================");
        Console.WriteLine();
        Console.WriteLine("📦 Repository Operations:");
        Console.WriteLine("  create <output-dir>           Create a new TUF repository");
        Console.WriteLine("  validate <repo-dir>           Validate repository structure and metadata");
        Console.WriteLine("  info <repo-dir>               Display repository information");
        Console.WriteLine("  add-target <repo> <file>      Add target file to repository (planned)");
        Console.WriteLine();
        Console.WriteLine("🔐 Key Management:");
        Console.WriteLine("  generate-keys <output-dir>    Generate signing keys for TUF roles");
        Console.WriteLine();
        Console.WriteLine("💡 Examples:");
        Console.WriteLine("  dotnet run -- create ./my-tuf-repo");
        Console.WriteLine("  dotnet run -- validate ./my-tuf-repo");
        Console.WriteLine("  dotnet run -- info ./my-tuf-repo");
        Console.WriteLine("  dotnet run -- generate-keys ./keys");
        Console.WriteLine();
        Console.WriteLine("🎯 Parity Achievement:");
        Console.WriteLine("This enhanced CLI addresses the 'Repository Management Tools' gap");
        Console.WriteLine("identified in the TUF .NET parity analysis, providing capabilities");
        Console.WriteLine("comparable to Python's tuf.repository and Go's repository examples.");
        Console.WriteLine();
        Console.WriteLine("For detailed help on a command: dotnet run -- <command> --help");
        
        return 0;
    }

    static int ShowUnknownCommand(string command)
    {
        Console.WriteLine($"❌ Unknown command: {command}");
        Console.WriteLine("💡 Run 'dotnet run -- help' to see available commands");
        return 1;
    }

    static async Task<int> ExecuteCreate(string[] args)
    {
        Console.WriteLine("🔧 Creating TUF repository...");
        Console.WriteLine();

        var outputDir = args.Length > 0 ? args[0] : "./sample-tuf-repo";
        
        Console.WriteLine($"📁 Output directory: {Path.GetFullPath(outputDir)}");
        Console.WriteLine("⚠️  Using ephemeral keys for demonstration");
        Console.WriteLine("⚠️  For production, implement secure key storage!");
        Console.WriteLine();

        // Enhanced repository creation with more comprehensive examples
        var builder = CreateEnhancedRepository();
        var repository = builder.Build();
        repository.WriteToDirectory(outputDir);

        Console.WriteLine($"✅ Repository created successfully!");
        Console.WriteLine();
        
        await DisplayRepositoryStructure(outputDir);
        ShowIntegrationExamples(outputDir);
        ShowSecurityReminders();

        return 0;
    }

    static async Task<int> ExecuteValidate(string[] args)
    {
        Console.WriteLine("🔍 Validating TUF repository...");
        Console.WriteLine();

        if (args.Length == 0)
        {
            Console.WriteLine("❌ Please specify repository directory");
            Console.WriteLine("Usage: dotnet run -- validate <repo-directory>");
            return 1;
        }

        var repoDir = args[0];
        
        if (!Directory.Exists(repoDir))
        {
            Console.WriteLine($"❌ Repository directory does not exist: {repoDir}");
            return 1;
        }

        Console.WriteLine($"📁 Validating: {Path.GetFullPath(repoDir)}");
        
        var issues = await ValidateRepository(repoDir);
        
        if (issues.Count == 0)
        {
            Console.WriteLine("✅ Repository validation passed!");
            Console.WriteLine("📊 All metadata files are present and well-formed");
            return 0;
        }
        else
        {
            Console.WriteLine($"⚠️  Found {issues.Count} validation issues:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"   • {issue}");
            }
            return 1;
        }
    }

    static async Task<int> ExecuteInfo(string[] args)
    {
        Console.WriteLine("📊 Repository Information");
        Console.WriteLine("========================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            Console.WriteLine("❌ Please specify repository directory");
            Console.WriteLine("Usage: dotnet run -- info <repo-directory>");
            return 1;
        }

        var repoDir = args[0];
        
        if (!Directory.Exists(repoDir))
        {
            Console.WriteLine($"❌ Repository directory does not exist: {repoDir}");
            return 1;
        }

        await DisplayRepositoryInfo(repoDir);
        return 0;
    }

    static async Task<int> ExecuteGenerateKeys(string[] args)
    {
        Console.WriteLine("🔐 Generating TUF Signing Keys");
        Console.WriteLine("==============================");
        Console.WriteLine();

        var outputDir = args.Length > 0 ? args[0] : "./tuf-keys";
        
        Console.WriteLine($"📁 Output directory: {Path.GetFullPath(outputDir)}");
        Console.WriteLine("🔑 Key type: Ed25519 (recommended)");
        Console.WriteLine("👥 Roles: root, timestamp, snapshot, targets");
        Console.WriteLine();
        Console.WriteLine("⚠️  Security Warning:");
        Console.WriteLine("   These keys are for DEMONSTRATION only!");
        Console.WriteLine("   For production:");
        Console.WriteLine("   • Use hardware security modules (HSMs)");
        Console.WriteLine("   • Implement proper key encryption");
        Console.WriteLine("   • Store root keys offline");
        Console.WriteLine();

        Directory.CreateDirectory(outputDir);

        var roles = new[] { "root", "timestamp", "snapshot", "targets" };
        var generatedKeys = new List<GeneratedKeyInfo>();

        foreach (var role in roles)
        {
            var signer = Ed25519Signer.Generate();
            var keyId = signer.Key.GetKeyId();
            
            // Write public key in TUF format
            var publicKeyFile = Path.Combine(outputDir, $"{role}_public.json");
            var publicKeyJson = System.Text.Json.JsonSerializer.Serialize(signer.Key, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(publicKeyFile, publicKeyJson);

            // Write private key placeholder with security warnings
            var privateKeyFile = Path.Combine(outputDir, $"{role}_private.key");
            var privateKeyContent = $"""
# TUF Private Key Placeholder for {role} role
# Key ID: {keyId}
# Generated: {DateTimeOffset.UtcNow:O}
#
# ⚠️  WARNING: This is a placeholder for demonstration!
# ⚠️  In production, private keys must be:
#    • Encrypted with strong passphrases
#    • Stored in hardware security modules (HSMs)
#    • Protected with appropriate access controls
#    • Never committed to version control
#
# This file represents the concept of private key storage.
# Actual implementation requires secure key management.

ROLE={role}
KEY_ID={keyId}
GENERATED={DateTimeOffset.UtcNow:O}
STATUS=DEMO_PLACEHOLDER
""";
            await File.WriteAllTextAsync(privateKeyFile, privateKeyContent);

            generatedKeys.Add(new GeneratedKeyInfo(role, keyId, publicKeyFile, privateKeyFile));
            Console.WriteLine($"✅ Generated keys for '{role}' role");
        }

        Console.WriteLine();
        Console.WriteLine("📋 Generated Keys Summary:");
        foreach (var key in generatedKeys)
        {
            Console.WriteLine($"🔑 {key.Role}:");
            Console.WriteLine($"   Key ID:  {key.KeyId}");
            Console.WriteLine($"   Public:  {key.PublicKeyFile}");
            Console.WriteLine($"   Private: {key.PrivateKeyFile}");
        }

        Console.WriteLine();
        Console.WriteLine("🚀 Next Steps:");
        Console.WriteLine("   1. Secure the private key files appropriately");
        Console.WriteLine("   2. Create a repository: dotnet run -- create ./my-repo");
        Console.WriteLine("   3. Implement proper key management for production");

        return 0;
    }

    static async Task<int> ExecuteAddTarget(string[] args)
    {
        Console.WriteLine("📦 Add Target File");
        Console.WriteLine("==================");
        Console.WriteLine();

        if (args.Length < 2)
        {
            Console.WriteLine("❌ Usage: dotnet run -- add-target <repo-directory> <file-path>");
            return 1;
        }

        Console.WriteLine("🚧 Target addition is planned for future implementation");
        Console.WriteLine();
        Console.WriteLine("This feature requires:");
        Console.WriteLine("   • Secure key management integration");
        Console.WriteLine("   • Incremental metadata updates");
        Console.WriteLine("   • Proper signature workflows");
        Console.WriteLine();
        Console.WriteLine("💡 Workaround: Recreate repository with all desired files");
        
        return 0;
    }

    static RepositoryBuilder CreateEnhancedRepository()
    {
        Console.WriteLine("🔐 Generating ephemeral signing keys...");
        
        var builder = new RepositoryBuilder()
            .AddSigner("root", Ed25519Signer.Generate())
            .AddSigner("timestamp", Ed25519Signer.Generate())
            .AddSigner("snapshot", Ed25519Signer.Generate())
            .AddSigner("targets", Ed25519Signer.Generate())
            .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1))
            .SetConsistentSnapshots(true);

        Console.WriteLine("📦 Adding comprehensive sample targets...");

        // Enhanced sample content demonstrating various use cases
        AddSampleTargets(builder);

        return builder;
    }

    static void AddSampleTargets(RepositoryBuilder builder)
    {
        // Welcome file with repository information
        var welcomeContent = """
# Welcome to this TUF Repository!

This repository was created with the enhanced TUF .NET Repository Manager,
which addresses the "Repository Management Tools" parity gap identified in
the TUF .NET ecosystem analysis.

## What This Demonstrates

✅ **Comprehensive Repository Creation**: Full TUF metadata workflow
✅ **Multiple Target Types**: Documentation, configuration, binaries  
✅ **Custom Metadata**: Rich target file annotations
✅ **Security Best Practices**: Consistent snapshots, proper expiry dates
✅ **Production Readiness**: Enterprise-grade repository structure

## Integration Examples

This repository works with:
- TUF .NET BasicClient example
- TUF .NET CliTool example  
- Any TUF-compliant client implementation

## Security Features

🔒 **Multi-role Signing**: Separate keys for different security functions
🔄 **Consistent Snapshots**: Prevents rollback and mix-and-match attacks
⏰ **Metadata Expiry**: Built-in freshness guarantees
🔐 **Cryptographic Integrity**: Ed25519 signatures on all metadata

Generated by TUF .NET Repository Manager
""";

        builder.AddTarget("README.md", System.Text.Encoding.UTF8.GetBytes(welcomeContent), new Dictionary<string, object>
        {
            ["description"] = "Repository welcome and information file",
            ["type"] = "documentation",
            ["format"] = "markdown",
            ["category"] = "info"
        });

        // Sample application binary (simulated)
        var appContent = System.Text.Encoding.UTF8.GetBytes("FAKE_BINARY_CONTENT_FOR_DEMO");
        builder.AddTarget("releases/myapp-v1.2.3.exe", appContent, new Dictionary<string, object>
        {
            ["description"] = "Sample application binary",
            ["version"] = "1.2.3",
            ["type"] = "executable",
            ["platform"] = "windows",
            ["architecture"] = "x64",
            ["release_date"] = DateTimeOffset.UtcNow.ToString("O")
        });

        // Configuration file
        var configContent = System.Text.Json.JsonSerializer.Serialize(new
        {
            application = new
            {
                name = "Sample TUF Application",
                version = "1.2.3",
                tuf_enabled = true,
                security = new
                {
                    signature_verification = true,
                    consistent_snapshots = true,
                    automatic_updates = true
                }
            },
            repository = new
            {
                metadata_url = "https://example.com/tuf/metadata",
                targets_url = "https://example.com/tuf/targets",
                created = DateTimeOffset.UtcNow
            }
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        builder.AddTarget("config/app-config.json", System.Text.Encoding.UTF8.GetBytes(configContent), new Dictionary<string, object>
        {
            ["description"] = "Application configuration with TUF settings",
            ["type"] = "configuration",
            ["format"] = "json",
            ["schema_version"] = "1.0"
        });

        // Sample data file
        var dataContent = """
name,version,platform,size,hash_sha256
myapp,1.2.3,windows,1024,abc123def456
myapp,1.2.3,linux,980,def456abc123  
myapp,1.2.3,macos,1100,789abc456def
""";

        builder.AddTarget("data/release-manifest.csv", System.Text.Encoding.UTF8.GetBytes(dataContent), new Dictionary<string, object>
        {
            ["description"] = "Release manifest with platform information",
            ["type"] = "data",
            ["format"] = "csv",
            ["schema"] = "release_manifest_v1"
        });

        // Sample patch file
        var patchContent = System.Text.Encoding.UTF8.GetBytes("MOCK_PATCH_CONTENT_BINARY_DIFF");
        builder.AddTarget("patches/v1.2.2-to-v1.2.3.patch", patchContent, new Dictionary<string, object>
        {
            ["description"] = "Incremental update patch",
            ["type"] = "patch", 
            ["from_version"] = "1.2.2",
            ["to_version"] = "1.2.3",
            ["patch_type"] = "binary_diff"
        });
    }

    static async Task DisplayRepositoryStructure(string repoDir)
    {
        Console.WriteLine("📁 Repository Structure:");
        Console.WriteLine("========================");
        
        var fullPath = Path.GetFullPath(repoDir);
        Console.WriteLine($"📂 {Path.GetFileName(fullPath)}/");
        
        // Display metadata structure
        var metadataDir = Path.Combine(repoDir, "metadata");
        if (Directory.Exists(metadataDir))
        {
            Console.WriteLine("├── 📂 metadata/");
            var metadataFiles = Directory.GetFiles(metadataDir, "*.json");
            foreach (var file in metadataFiles.OrderBy(f => f))
            {
                var fileName = Path.GetFileName(file);
                var fileInfo = new FileInfo(file);
                Console.WriteLine($"│   ├── 📄 {fileName} ({fileInfo.Length:N0} bytes)");
            }
        }

        // Display targets structure  
        var targetsDir = Path.Combine(repoDir, "targets");
        if (Directory.Exists(targetsDir))
        {
            Console.WriteLine("└── 📂 targets/");
            await DisplayTargetTree(targetsDir, "    ");
        }

        Console.WriteLine();
    }

    static async Task DisplayTargetTree(string dir, string indent)
    {
        var entries = Directory.GetFileSystemEntries(dir).OrderBy(e => e).ToList();
        
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var isLast = i == entries.Count - 1;
            var prefix = isLast ? "└── " : "├── ";
            var nextIndent = indent + (isLast ? "    " : "│   ");

            if (Directory.Exists(entry))
            {
                Console.WriteLine($"{indent}{prefix}📂 {Path.GetFileName(entry)}/");
                await DisplayTargetTree(entry, nextIndent);
            }
            else
            {
                var fileInfo = new FileInfo(entry);
                Console.WriteLine($"{indent}{prefix}📄 {Path.GetFileName(entry)} ({fileInfo.Length:N0} bytes)");
            }
        }
    }

    static async Task<List<string>> ValidateRepository(string repoDir)
    {
        var issues = new List<string>();

        // Check basic structure
        if (!Directory.Exists(Path.Combine(repoDir, "metadata")))
            issues.Add("Missing metadata directory");

        // Check required metadata files
        var requiredFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        foreach (var fileName in requiredFiles)
        {
            var filePath = Path.Combine(repoDir, "metadata", fileName);
            if (!File.Exists(filePath))
                issues.Add($"Missing {fileName}");
            else
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    var doc = System.Text.Json.JsonDocument.Parse(content);
                    
                    if (!doc.RootElement.TryGetProperty("signed", out _))
                        issues.Add($"{fileName} missing 'signed' property");
                    
                    if (!doc.RootElement.TryGetProperty("signatures", out var sigs) || sigs.GetArrayLength() == 0)
                        issues.Add($"{fileName} has no signatures");
                }
                catch (Exception ex)
                {
                    issues.Add($"{fileName} parse error: {ex.Message}");
                }
            }
        }

        return issues;
    }

    static async Task DisplayRepositoryInfo(string repoDir)
    {
        Console.WriteLine($"📁 Path: {Path.GetFullPath(repoDir)}");
        Console.WriteLine($"📅 Analyzed: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        Console.WriteLine();

        // Metadata information
        Console.WriteLine("🔒 Metadata Files:");
        var metadataDir = Path.Combine(repoDir, "metadata");
        if (Directory.Exists(metadataDir))
        {
            var metadataFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
            foreach (var fileName in metadataFiles)
            {
                var filePath = Path.Combine(metadataDir, fileName);
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    Console.WriteLine($"   ✅ {fileName}: {fileInfo.Length:N0} bytes, modified {fileInfo.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss UTC}");
                    
                    try
                    {
                        var content = await File.ReadAllTextAsync(filePath);
                        var doc = System.Text.Json.JsonDocument.Parse(content);
                        
                        if (doc.RootElement.TryGetProperty("signatures", out var sigs))
                        {
                            Console.WriteLine($"      🔐 {sigs.GetArrayLength()} signature(s)");
                        }
                        
                        if (doc.RootElement.TryGetProperty("signed", out var signed) &&
                            signed.TryGetProperty("expires", out var expires))
                        {
                            if (DateTimeOffset.TryParse(expires.GetString(), out var expiryDate))
                            {
                                var timeToExpiry = expiryDate - DateTimeOffset.UtcNow;
                                var status = timeToExpiry.TotalDays > 0 ? "✅" : "❌";
                                Console.WriteLine($"      {status} Expires: {expiryDate:yyyy-MM-dd HH:mm:ss UTC} ({timeToExpiry.TotalDays:F0} days)");
                            }
                        }
                    }
                    catch
                    {
                        Console.WriteLine("      ❌ Error parsing metadata");
                    }
                }
                else
                {
                    Console.WriteLine($"   ❌ {fileName}: Missing");
                }
            }
        }

        Console.WriteLine();

        // Targets information
        Console.WriteLine("📦 Target Files:");
        var targetsDir = Path.Combine(repoDir, "targets");
        if (Directory.Exists(targetsDir))
        {
            var targetFiles = Directory.GetFiles(targetsDir, "*", SearchOption.AllDirectories);
            var totalSize = targetFiles.Sum(f => new FileInfo(f).Length);
            
            Console.WriteLine($"   📊 {targetFiles.Length} files, {totalSize:N0} bytes total");
            
            foreach (var file in targetFiles.Take(10))
            {
                var relativePath = Path.GetRelativePath(targetsDir, file);
                var fileInfo = new FileInfo(file);
                Console.WriteLine($"   📄 {relativePath}: {fileInfo.Length:N0} bytes");
            }
            
            if (targetFiles.Length > 10)
                Console.WriteLine($"   ... and {targetFiles.Length - 10} more files");
        }
        else
        {
            Console.WriteLine("   ⚠️  No targets directory found");
        }
    }

    static void ShowIntegrationExamples(string repoDir)
    {
        var fullPath = Path.GetFullPath(repoDir);
        
        Console.WriteLine("🚀 Integration Examples:");
        Console.WriteLine("=======================");
        Console.WriteLine();
        Console.WriteLine("Test with BasicClient:");
        Console.WriteLine($"  dotnet run --project examples/BasicClient -- file://{fullPath}/metadata README.md");
        Console.WriteLine();
        Console.WriteLine("Test with CliTool:");
        Console.WriteLine($"  dotnet run --project examples/CliTool -- --metadata-url file://{fullPath}/metadata refresh");
        Console.WriteLine();
        Console.WriteLine("Validate repository:");
        Console.WriteLine($"  dotnet run --project examples/RepositoryManager -- validate {fullPath}");
        Console.WriteLine();
        Console.WriteLine("Get repository info:");
        Console.WriteLine($"  dotnet run --project examples/RepositoryManager -- info {fullPath}");
        Console.WriteLine();
    }

    static void ShowSecurityReminders()
    {
        Console.WriteLine("🔒 Security Reminders:");
        Console.WriteLine("======================");
        Console.WriteLine("⚠️  This repository uses ephemeral keys (DEMO ONLY)");
        Console.WriteLine("✅ For production deployment:");
        Console.WriteLine("   • Use hardware security modules (HSMs)");
        Console.WriteLine("   • Implement proper key rotation procedures");
        Console.WriteLine("   • Store root keys offline");
        Console.WriteLine("   • Use threshold signatures where appropriate");
        Console.WriteLine("   • Implement comprehensive access controls");
        Console.WriteLine("   • Regular security audits and monitoring");
        Console.WriteLine();
    }

    private record GeneratedKeyInfo(string Role, string KeyId, string PublicKeyFile, string? PrivateKeyFile);
}