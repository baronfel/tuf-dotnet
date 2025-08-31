using System.Text.Json;
using System.Text.Json.Serialization;

using TUF.Models.Roles.Targets;

namespace TUF.Serialization;

[JsonSerializable(typeof(TUF.Models.Keys.Values.RsaKeyValue))]
[JsonSerializable(typeof(TUF.Models.Keys.Values.Ed25519KeyValue))]
[JsonSerializable(typeof(TUF.Models.Keys.Values.EcdsaKeyValue))]
[JsonSerializable(typeof(TUF.Models.Primitives.PEMString))]
[JsonSerializable(typeof(TUF.Models.Primitives.HexString))]
[JsonSerializable(typeof(TUF.Models.Primitives.HexDigest))]
[JsonSerializable(typeof(TUF.Models.Primitives.RelativePath))]
[JsonSerializable(typeof(Dictionary<TUF.Models.Primitives.RelativePath, TUF.Models.Primitives.FileMetadata>))]
[JsonSerializable(typeof(TUF.Models.Primitives.FileMetadata))]
[JsonSerializable(typeof(DelegationData))]
[JsonSerializable(typeof(DelegatedRoleName))]
public partial class MetadataJsonContext : JsonSerializerContext
{
    public static JsonSerializerOptions AddedOptions = CreateOptions();
    public static MetadataJsonContext DefaultWithAddedOptions = new(AddedOptions);
    
    // Context for use inside converters - has proper deserialization support but no custom converters
    public static JsonSerializerOptions ConverterInternalOptions = CreateConverterInternalOptions();
    public static MetadataJsonContext ConverterInternal = new(ConverterInternalOptions);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TUF.Serialization.Converters.RoleTypeJsonConverter<TUF.Models.Roles.Root.Root>());
        options.Converters.Add(new TUF.Serialization.Converters.RoleTypeJsonConverter<TUF.Models.Roles.Snapshot.Snapshot>());
        options.Converters.Add(new TUF.Serialization.Converters.RoleTypeJsonConverter<TUF.Models.Roles.Targets.TargetsRole>());
        options.Converters.Add(new TUF.Serialization.Converters.RoleTypeJsonConverter<TUF.Models.Roles.Timestamp.Timestamp>());
        options.Converters.Add(new TUF.Serialization.Converters.RoleTypeJsonConverter<TUF.Models.Roles.Mirrors.Mirror>());
        return options;
    }
    
    private static JsonSerializerOptions CreateConverterInternalOptions()
    {
        var options = new JsonSerializerOptions();
        // No custom converters added - this prevents recursion
        // Enable support for constructor-based deserialization
        options.AllowTrailingCommas = true;
        options.PropertyNameCaseInsensitive = false;
        return options;
    }
}