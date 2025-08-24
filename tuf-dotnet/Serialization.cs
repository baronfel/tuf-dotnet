namespace TUF.Serialization;

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CanonicalJson;
using TUF.Models;
using TUF.Models.Roles;

/// <summary>
/// Helper methods for serializing and deserializing Metadata<T> instances using the
/// TUF model converters defined above. These produce canonical output when required.
/// </summary>
public static class MetadataSerializer
{
    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeCanonicalToUTF8Bytes<T>(Metadata<T> metadata) where T : IRole
    {
        return CanonicalJsonSerializer.Serialize(metadata);
    }

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters,
    /// but return a UTF-8 string instead of bytes.
    /// </summary>
    public static string SerializeCanonicalToUTF8String<T>(Metadata<T> metadata) where T : IRole
    {
        var bytes = SerializeCanonicalToUTF8Bytes(metadata);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from JSON bytes (canonical or not) using the custom converters.
    /// </summary>
    public static Metadata<T>? Deserialize<T>(byte[] utf8JsonBytes) where T : IRole
    {
        return JsonSerializer.Deserialize<Metadata<T>>(utf8JsonBytes);
    }

    /// <summary>
    /// Deserialize a Metadata<T> instance from a UTF-8 encoded JSON string using the custom converters.
    /// </summary>
    public static Metadata<T>? DeserializeFromString<T>(string utf8JsonString) where T : IRole
    {
        return JsonSerializer.Deserialize<Metadata<T>>(utf8JsonString);
    }

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters.
    /// </summary>
    public static byte[] SerializeToUTF8Bytes<T>(Metadata<T> metadata) where T : IRole
    {
        using var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, metadata);
        return ms.ToArray();
    }

    /// <summary>
    /// Produce canonical UTF-8 bytes for a Metadata<T> instance using the custom converters,
    /// but return a UTF-8 string instead of bytes.
    /// </summary>
    public static string SerializeToString<T>(Metadata<T> metadata) where T : IRole
    {
        return JsonSerializer.Serialize(metadata);
    }

    
}

