using System.Text.Json;
using TUF.Models;

namespace RepositoryValidator;

/// <summary>
/// TUF Repository Validation CLI Tool
/// Provides comprehensive validation and auditing capabilities for TUF repositories.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("🔍 TUF Repository Validator");
        Console.WriteLine("============================");
        
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: RepositoryValidator <repository-path> [--verbose] [--strict]");
            Console.WriteLine("Example: RepositoryValidator ./sample-tuf-repo --verbose");
            return 1;
        }

        var repositoryPath = args[0];
        var verbose = args.Contains("--verbose");
        var strict = args.Contains("--strict");

        if (!Directory.Exists(repositoryPath))
        {
            Console.WriteLine($"❌ Repository path does not exist: {repositoryPath}");
            return 1;
        }

        try
        {
            var validator = new RepositoryValidator(repositoryPath, verbose, strict);
            var result = validator.Validate();
            
            Console.WriteLine();
            Console.WriteLine("📊 Validation Summary");
            Console.WriteLine("====================");
            Console.WriteLine($"✅ Passed: {result.PassedChecks}");
            
            if (result.Warnings.Count > 0)
            {
                Console.WriteLine($"⚠️  Warnings: {result.Warnings.Count}");
                if (verbose || result.Warnings.Count <= 5)
                {
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"   - {warning}");
                    }
                }
            }

            if (result.Errors.Count > 0)
            {
                Console.WriteLine($"❌ Errors: {result.Errors.Count}");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"   - {error}");
                }
            }

            Console.WriteLine();

            var hasErrors = result.Errors.Count > 0;
            var hasWarningsInStrictMode = strict && result.Warnings.Count > 0;

            if (hasErrors || hasWarningsInStrictMode)
            {
                Console.WriteLine("❌ Repository validation FAILED");
                return 1;
            }
            else
            {
                Console.WriteLine("✅ Repository validation PASSED");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Fatal error: {ex.Message}");
            if (verbose)
            {
                Console.WriteLine($"Stack trace: {ex}");
            }
            return 2;
        }
    }
}

/// <summary>
/// Comprehensive TUF repository validator implementing security checks and compliance validation.
/// </summary>
public class RepositoryValidator
{
    private readonly string _repositoryPath;
    private readonly bool _verbose;
    private readonly bool _strict;
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public RepositoryValidator(string repositoryPath, bool verbose, bool strict)
    {
        _repositoryPath = repositoryPath;
        _verbose = verbose;
        _strict = strict;
    }

    /// <summary>
    /// Performs comprehensive validation of the TUF repository.
    /// </summary>
    public ValidationResult Validate()
    {
        var passedChecks = 0;

        LogInfo("Validating repository structure...");
        passedChecks += ValidateRepositoryStructure();

        LogInfo("Validating metadata files...");
        passedChecks += ValidateMetadataFiles();

        LogInfo("Checking metadata expiration dates...");
        passedChecks += ValidateExpirationDates();

        LogInfo("Validating JSON structure...");
        passedChecks += ValidateJsonStructure();

        return new ValidationResult
        {
            PassedChecks = passedChecks,
            Errors = _errors,
            Warnings = _warnings
        };
    }

    /// <summary>
    /// Validates the basic repository directory structure.
    /// </summary>
    private int ValidateRepositoryStructure()
    {
        var metadataPath = Path.Combine(_repositoryPath, "metadata");
        if (!Directory.Exists(metadataPath))
        {
            AddError("Missing metadata directory");
            return 0;
        }

        var requiredFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        var missingFiles = requiredFiles.Where(file => !File.Exists(Path.Combine(metadataPath, file))).ToList();

        if (missingFiles.Count > 0)
        {
            AddError($"Missing required metadata files: {string.Join(", ", missingFiles)}");
            return 0;
        }

        LogVerbose("✓ Repository structure is valid");
        return 1;
    }

    /// <summary>
    /// Validates that all metadata files can be parsed as valid JSON.
    /// </summary>
    private int ValidateMetadataFiles()
    {
        var metadataPath = Path.Combine(_repositoryPath, "metadata");
        var files = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        var validFiles = 0;

        foreach (var file in files)
        {
            try
            {
                var filePath = Path.Combine(metadataPath, file);
                var content = File.ReadAllText(filePath);
                
                // Basic JSON validation
                using var document = JsonDocument.Parse(content);
                
                // Check for basic TUF metadata structure
                if (!document.RootElement.TryGetProperty("signed", out var signed))
                {
                    AddError($"{file}: Missing 'signed' property");
                    continue;
                }

                if (!document.RootElement.TryGetProperty("signatures", out var signatures))
                {
                    AddError($"{file}: Missing 'signatures' property");
                    continue;
                }

                if (signatures.GetArrayLength() == 0)
                {
                    AddWarning($"{file}: No signatures present");
                }

                // Check for required fields in signed portion
                if (!signed.TryGetProperty("_type", out var typeElement))
                {
                    AddError($"{file}: Missing '_type' field in signed metadata");
                    continue;
                }

                if (!signed.TryGetProperty("expires", out var expiresElement))
                {
                    AddError($"{file}: Missing 'expires' field in signed metadata");
                    continue;
                }

                if (!signed.TryGetProperty("version", out var versionElement))
                {
                    AddError($"{file}: Missing 'version' field in signed metadata");
                    continue;
                }

                // Validate version is positive integer
                if (versionElement.GetInt32() < 1)
                {
                    AddError($"{file}: Version must be >= 1");
                    continue;
                }

                LogVerbose($"✓ {file} has valid structure");
                validFiles++;
            }
            catch (JsonException ex)
            {
                AddError($"{file}: Invalid JSON - {ex.Message}");
            }
            catch (Exception ex)
            {
                AddError($"{file}: Validation error - {ex.Message}");
            }
        }

        return validFiles == files.Length ? 1 : 0;
    }

    /// <summary>
    /// Validates metadata expiration dates.
    /// </summary>
    private int ValidateExpirationDates()
    {
        var metadataPath = Path.Combine(_repositoryPath, "metadata");
        var files = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        var now = DateTimeOffset.UtcNow;
        var validFiles = 0;

        foreach (var file in files)
        {
            try
            {
                var filePath = Path.Combine(metadataPath, file);
                var content = File.ReadAllText(filePath);
                
                using var document = JsonDocument.Parse(content);
                
                if (document.RootElement.TryGetProperty("signed", out var signed) &&
                    signed.TryGetProperty("expires", out var expiresElement))
                {
                    if (DateTimeOffset.TryParse(expiresElement.GetString(), out var expires))
                    {
                        if (expires <= now)
                        {
                            AddError($"{file} has expired: {expires:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                        else if (expires <= now.AddDays(7))
                        {
                            AddWarning($"{file} expires soon: {expires:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                        else
                        {
                            validFiles++;
                        }
                    }
                    else
                    {
                        AddError($"{file}: Invalid expiration date format");
                    }
                }
                else
                {
                    AddError($"{file}: Missing expiration date");
                }
            }
            catch (Exception ex)
            {
                AddError($"{file}: Could not check expiration - {ex.Message}");
            }
        }

        if (validFiles == files.Length)
        {
            LogVerbose("✓ All metadata expiration dates are valid");
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Validates JSON structure against TUF specification requirements.
    /// </summary>
    private int ValidateJsonStructure()
    {
        var metadataPath = Path.Combine(_repositoryPath, "metadata");
        var validStructures = 0;

        // Validate root.json structure
        if (ValidateRootStructure(Path.Combine(metadataPath, "root.json")))
            validStructures++;

        // Validate timestamp.json structure  
        if (ValidateTimestampStructure(Path.Combine(metadataPath, "timestamp.json")))
            validStructures++;

        // Validate snapshot.json structure
        if (ValidateSnapshotStructure(Path.Combine(metadataPath, "snapshot.json")))
            validStructures++;

        // Validate targets.json structure
        if (ValidateTargetsStructure(Path.Combine(metadataPath, "targets.json")))
            validStructures++;

        return validStructures == 4 ? 1 : 0;
    }

    private bool ValidateRootStructure(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(content);
            
            if (!document.RootElement.TryGetProperty("signed", out var signed))
                return false;

            // Check for required root fields
            if (!signed.TryGetProperty("keys", out var keys) ||
                !signed.TryGetProperty("roles", out var roles))
            {
                AddError("root.json: Missing keys or roles");
                return false;
            }

            // Validate roles structure
            var requiredRoles = new[] { "root", "timestamp", "snapshot", "targets" };
            foreach (var role in requiredRoles)
            {
                if (!roles.TryGetProperty(role, out var roleElement))
                {
                    AddError($"root.json: Missing required role '{role}'");
                    return false;
                }

                if (!roleElement.TryGetProperty("keyids", out var keyids) ||
                    !roleElement.TryGetProperty("threshold", out var threshold))
                {
                    AddError($"root.json: Role '{role}' missing keyids or threshold");
                    return false;
                }

                if (threshold.GetInt32() < 1)
                {
                    AddError($"root.json: Role '{role}' has invalid threshold");
                    return false;
                }
            }

            LogVerbose("✓ root.json structure is valid");
            return true;
        }
        catch (Exception ex)
        {
            AddError($"root.json: Structure validation failed - {ex.Message}");
            return false;
        }
    }

    private bool ValidateTimestampStructure(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(content);
            
            if (!document.RootElement.TryGetProperty("signed", out var signed))
                return false;

            if (!signed.TryGetProperty("meta", out var meta))
            {
                AddError("timestamp.json: Missing meta field");
                return false;
            }

            if (!meta.TryGetProperty("snapshot.json", out var _))
            {
                AddError("timestamp.json: Missing snapshot.json reference");
                return false;
            }

            LogVerbose("✓ timestamp.json structure is valid");
            return true;
        }
        catch (Exception ex)
        {
            AddError($"timestamp.json: Structure validation failed - {ex.Message}");
            return false;
        }
    }

    private bool ValidateSnapshotStructure(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(content);
            
            if (!document.RootElement.TryGetProperty("signed", out var signed))
                return false;

            if (!signed.TryGetProperty("meta", out var meta))
            {
                AddError("snapshot.json: Missing meta field");
                return false;
            }

            if (!meta.TryGetProperty("targets.json", out var _))
            {
                AddError("snapshot.json: Missing targets.json reference");
                return false;
            }

            LogVerbose("✓ snapshot.json structure is valid");
            return true;
        }
        catch (Exception ex)
        {
            AddError($"snapshot.json: Structure validation failed - {ex.Message}");
            return false;
        }
    }

    private bool ValidateTargetsStructure(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(content);
            
            if (!document.RootElement.TryGetProperty("signed", out var signed))
                return false;

            if (!signed.TryGetProperty("targets", out var targets))
            {
                AddWarning("targets.json: Missing targets field (empty repository)");
            }

            // Check for delegations if present
            if (signed.TryGetProperty("delegations", out var delegations))
            {
                if (!delegations.TryGetProperty("keys", out var _) ||
                    !delegations.TryGetProperty("roles", out var roles))
                {
                    AddError("targets.json: Invalid delegations structure");
                    return false;
                }

                // Validate each delegation role
                foreach (var role in roles.EnumerateArray())
                {
                    if (!role.TryGetProperty("name", out var _) ||
                        !role.TryGetProperty("keyids", out var _) ||
                        !role.TryGetProperty("threshold", out var threshold) ||
                        !role.TryGetProperty("paths", out var _))
                    {
                        AddError("targets.json: Invalid delegation role structure");
                        return false;
                    }

                    if (threshold.GetInt32() < 1)
                    {
                        AddError("targets.json: Delegation role has invalid threshold");
                        return false;
                    }
                }
            }

            LogVerbose("✓ targets.json structure is valid");
            return true;
        }
        catch (Exception ex)
        {
            AddError($"targets.json: Structure validation failed - {ex.Message}");
            return false;
        }
    }

    private void AddError(string message) => _errors.Add(message);
    private void AddWarning(string message) => _warnings.Add(message);
    
    private void LogInfo(string message)
    {
        Console.WriteLine($"🔍 {message}");
    }
    
    private void LogVerbose(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"   {message}");
        }
    }
}

/// <summary>
/// Result of repository validation containing pass/fail counts and issue details.
/// </summary>
public class ValidationResult
{
    public int PassedChecks { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}