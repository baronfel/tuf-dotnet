using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CanonicalJson;

namespace TUF.Serialization;

public interface IAOTSerializable<T> where T : IAOTSerializable<T>
{
    static abstract JsonTypeInfo<T> JsonTypeInfo { get; }
}

/// <summary>
/// Helper methods for serializing and deserializing Metadata<T> instances using the
/// TUF model converters defined below. These produce canonical output when required.
/// </summary>
public static class MetadataSerializer
{
    
    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeCanonicalToUTF8Bytes<T>(T metadata) where T : IAOTSerializable<T>
    {
        // For common concrete role types we can use the generated context for AOT compatibility.
        return CanonicalJsonSerializer.Serialize(metadata, T.JsonTypeInfo);
    }

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters,
    /// but return a UTF-8 string instead of bytes.
    /// </summary>
    public static string SerializeCanonicalToUTF8String<T>(T metadata) where T : IAOTSerializable<T>
    {
        var bytes = SerializeCanonicalToUTF8Bytes(metadata);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from JSON bytes (canonical or not) using the custom converters.
    /// </summary>
    public static T? Deserialize<T>(byte[] utf8JsonBytes) where T : IAOTSerializable<T>
    {
        ReadOnlySpan<byte> bytes = utf8JsonBytes;
        return JsonSerializer.Deserialize(bytes, T.JsonTypeInfo);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from a stream of UTF8 JSON bytes (canonical or not) using the custom converters.
    /// </summary>
    public static T? Deserialize<T>(Stream utf8JsonBytes) where T : IAOTSerializable<T>
    {
        return JsonSerializer.Deserialize(utf8JsonBytes, T.JsonTypeInfo);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from a UTF-8 encoded JSON string using the custom converters.
    /// </summary>
    public static T? DeserializeFromString<T>(string utf8JsonString) where T : IAOTSerializable<T>
    {
        ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(utf8JsonString);
        return JsonSerializer.Deserialize(bytes, T.JsonTypeInfo);
    }

    public static T? LoadFromFile<T>(string filePath) where T : IAOTSerializable<T>
    {
        using var file = File.OpenRead(filePath);
        return Deserialize<T>(file);
    }

    /// <summary>
    /// Produce UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeToUTF8Bytes<T>(T metadata) where T : IAOTSerializable<T>
    {
        using var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, metadata, T.JsonTypeInfo);
        return ms.ToArray();
    }

    /// <summary>
    /// Produce a UTF-8 string for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static string SerializeToString<T>(T metadata) where T : IAOTSerializable<T>
    {
        return JsonSerializer.Serialize(metadata, T.JsonTypeInfo);
    }
}