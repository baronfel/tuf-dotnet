using System.Reflection;
using System.Text.Json;

using Json.Schema;

using Microsoft.Extensions.Logging;

namespace TUF.Validation;

/// <summary>
/// Provides JSON Schema validation for TUF metadata files.
/// Validates TUF metadata against the official JSON schemas to ensure specification compliance.
/// </summary>
/// <remarks>
/// Schema validation is essential for:
/// - Ensuring metadata conforms to TUF specification
/// - Interoperability testing with other TUF implementations
/// - Early detection of metadata format issues
/// - Compliance verification for security audits
/// 
/// All schemas are embedded as resources and loaded on-demand for performance.
/// </remarks>
public static class SchemaValidator
{
    private static readonly Dictionary<string, JsonSchema> _schemaCache = new();
    private static readonly object _cacheLocker = new();

    /// <summary>
    /// Validates a TUF metadata JSON string against the appropriate schema.
    /// Automatically detects the metadata type from the '_type' field.
    /// </summary>
    /// <param name="metadataJson">The JSON string containing TUF metadata to validate</param>
    /// <param name="logger">Optional logger for validation messages</param>
    /// <returns>ValidationResult containing success status and any validation errors</returns>
    /// <exception cref="ArgumentNullException">Thrown when metadataJson is null or empty</exception>
    /// <exception cref="JsonException">Thrown when metadataJson is not valid JSON</exception>
    public static ValidationResult ValidateMetadata(string metadataJson, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new ArgumentNullException(nameof(metadataJson), "Metadata JSON cannot be null or empty");
        }

        try
        {
            // Parse JSON to determine metadata type
            var jsonDocument = JsonDocument.Parse(metadataJson);
            var metadataType = DetermineMetadataType(jsonDocument);
            
            logger?.LogDebug("Validating {MetadataType} metadata against JSON schema", metadataType);

            // Get appropriate schema and validate
            var schema = GetSchema(metadataType);
            var jsonElement = jsonDocument.RootElement;
            
            var result = schema.Evaluate(jsonElement);
            
            if (result.IsValid)
            {
                logger?.LogDebug("Metadata validation successful for {MetadataType}", metadataType);
                return ValidationResult.Success();
            }

            var errors = CollectValidationErrors(result);
            logger?.LogWarning("Metadata validation failed for {MetadataType}: {ErrorCount} errors found", 
                metadataType, errors.Count);
            
            foreach (var error in errors)
            {
                logger?.LogWarning("Validation error: {Error}", error);
            }

            return ValidationResult.Failure(errors);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to parse metadata JSON");
            throw new JsonException("Invalid JSON format in metadata", ex);
        }
    }

    /// <summary>
    /// Validates TUF metadata against a specific schema type.
    /// Use this method when you know the specific metadata type to validate against.
    /// </summary>
    /// <param name="metadataJson">The JSON string containing TUF metadata to validate</param>
    /// <param name="schemaType">The specific schema type to validate against</param>
    /// <param name="logger">Optional logger for validation messages</param>
    /// <returns>ValidationResult containing success status and any validation errors</returns>
    public static ValidationResult ValidateMetadata(string metadataJson, SchemaType schemaType, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new ArgumentNullException(nameof(metadataJson), "Metadata JSON cannot be null or empty");
        }

        try
        {
            logger?.LogDebug("Validating metadata against {SchemaType} schema", schemaType);

            var schema = GetSchema(schemaType);
            var jsonElement = JsonDocument.Parse(metadataJson).RootElement;
            
            var result = schema.Evaluate(jsonElement);
            
            if (result.IsValid)
            {
                logger?.LogDebug("Metadata validation successful for {SchemaType}", schemaType);
                return ValidationResult.Success();
            }

            var errors = CollectValidationErrors(result);
            logger?.LogWarning("Metadata validation failed for {SchemaType}: {ErrorCount} errors found", 
                schemaType, errors.Count);

            return ValidationResult.Failure(errors);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to parse metadata JSON");
            throw new JsonException("Invalid JSON format in metadata", ex);
        }
    }

    /// <summary>
    /// Determines the TUF metadata type from a JSON document by examining the '_type' field.
    /// </summary>
    private static SchemaType DetermineMetadataType(JsonDocument document)
    {
        // Check if this is a signed metadata wrapper
        if (document.RootElement.TryGetProperty("signed", out var signedElement))
        {
            if (signedElement.TryGetProperty("_type", out var typeElement))
            {
                var typeValue = typeElement.GetString();
                return typeValue switch
                {
                    "root" => SchemaType.Root,
                    "targets" => SchemaType.Targets,
                    "snapshot" => SchemaType.Snapshot,
                    "timestamp" => SchemaType.Timestamp,
                    _ => throw new ArgumentException($"Unknown metadata type: {typeValue}")
                };
            }
        }
        // Check if this is raw signed content
        else if (document.RootElement.TryGetProperty("_type", out var directTypeElement))
        {
            var typeValue = directTypeElement.GetString();
            return typeValue switch
            {
                "root" => SchemaType.Root,
                "targets" => SchemaType.Targets,
                "snapshot" => SchemaType.Snapshot,
                "timestamp" => SchemaType.Timestamp,
                _ => throw new ArgumentException($"Unknown metadata type: {typeValue}")
            };
        }

        throw new ArgumentException("Unable to determine metadata type - '_type' field not found");
    }

    /// <summary>
    /// Gets the JSON schema for the specified metadata type, loading from embedded resources if needed.
    /// </summary>
    private static JsonSchema GetSchema(SchemaType schemaType)
    {
        var schemaName = schemaType.ToString().ToLowerInvariant();
        
        lock (_cacheLocker)
        {
            if (_schemaCache.TryGetValue(schemaName, out var cachedSchema))
            {
                return cachedSchema;
            }

            var schema = LoadSchemaFromResources(schemaName);
            _schemaCache[schemaName] = schema;
            return schema;
        }
    }

    /// <summary>
    /// Loads a JSON schema from embedded resources.
    /// </summary>
    private static JsonSchema LoadSchemaFromResources(string schemaName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"TUF.Schemas.{schemaName}.schema.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Schema resource not found: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        var schemaJson = reader.ReadToEnd();
        
        return JsonSchema.FromText(schemaJson);
    }

    /// <summary>
    /// Recursively collects all validation errors from a schema evaluation result.
    /// </summary>
    private static List<string> CollectValidationErrors(EvaluationResults result)
    {
        var errors = new List<string>();
        
        if (!result.IsValid)
        {
            var errorMsg = $"Validation failed at path '{result.InstanceLocation}'";
            errors.Add(errorMsg);
        }

        // Collect errors from nested results
        foreach (var detail in result.Details)
        {
            errors.AddRange(CollectValidationErrors(detail));
        }

        return errors;
    }
}

/// <summary>
/// Represents the types of TUF metadata schemas available for validation.
/// </summary>
public enum SchemaType
{
    /// <summary>Root metadata schema</summary>
    Root,
    /// <summary>Targets metadata schema</summary>
    Targets,
    /// <summary>Snapshot metadata schema</summary>
    Snapshot,
    /// <summary>Timestamp metadata schema</summary>
    Timestamp,
    /// <summary>General metadata wrapper schema</summary>
    Metadata
}

/// <summary>
/// Represents the result of a JSON schema validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful.
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Gets the list of validation errors, if any.
    /// Empty if IsValid is true.
    /// </summary>
    public IReadOnlyList<string> Errors { get; private set; }

    private ValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult(true, Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static ValidationResult Failure(IReadOnlyList<string> errors)
    {
        return new ValidationResult(false, errors ?? Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string error)
    {
        return new ValidationResult(false, new[] { error });
    }
}