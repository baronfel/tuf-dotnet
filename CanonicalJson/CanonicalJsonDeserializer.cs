using System.Buffers;

using Serde;

namespace CanonicalJson;

/// <summary>
/// Custom IDeserializer implementation that reads canonical JSON implementing its unusual escaping rules.
/// This implements Serde.NET's IDeserializer and ITypeDeserializer interfaces to provide 
/// direct control over JSON deserialization.
/// </summary>
public sealed class CanonicalJsonSerdeReader : IDeserializer, ITypeDeserializer, IDisposable
{
    public CanonicalJsonSerdeReader(string content)
    {
        throw new NotImplementedException();
    }

    public int? SizeOpt => throw new NotImplementedException();

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public bool ReadBool()
    {
        throw new NotImplementedException();
    }

    public bool ReadBool(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public void ReadBytes(IBufferWriter<byte> writer)
    {
        throw new NotImplementedException();
    }

    public void ReadBytes(ISerdeInfo info, int index, IBufferWriter<byte> writer)
    {
        throw new NotImplementedException();
    }

    public char ReadChar()
    {
        throw new NotImplementedException();
    }

    public char ReadChar(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public DateTime ReadDateTime()
    {
        throw new NotImplementedException();
    }

    public DateTime ReadDateTime(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public decimal ReadDecimal()
    {
        throw new NotImplementedException();
    }

    public decimal ReadDecimal(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public float ReadF32()
    {
        throw new NotImplementedException();
    }

    public float ReadF32(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public double ReadF64()
    {
        throw new NotImplementedException();
    }

    public double ReadF64(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public short ReadI16()
    {
        throw new NotImplementedException();
    }

    public short ReadI16(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public int ReadI32()
    {
        throw new NotImplementedException();
    }

    public int ReadI32(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public long ReadI64()
    {
        throw new NotImplementedException();
    }

    public long ReadI64(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public sbyte ReadI8()
    {
        throw new NotImplementedException();
    }

    public sbyte ReadI8(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public T? ReadNullableRef<T>(IDeserialize<T> deserialize) where T : class
    {
        throw new NotImplementedException();
    }

    public string ReadString()
    {
        throw new NotImplementedException();
    }

    public string ReadString(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public ITypeDeserializer ReadType(ISerdeInfo typeInfo)
    {
        throw new NotImplementedException();
    }

    public ushort ReadU16()
    {
        throw new NotImplementedException();
    }

    public ushort ReadU16(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public uint ReadU32()
    {
        throw new NotImplementedException();
    }

    public uint ReadU32(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public ulong ReadU64()
    {
        throw new NotImplementedException();
    }

    public ulong ReadU64(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public byte ReadU8()
    {
        throw new NotImplementedException();
    }

    public byte ReadU8(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    T ITypeDeserializer.ReadValue<T>(ISerdeInfo info, int index, IDeserialize<T> deserialize) where T : class
    {
        throw new NotImplementedException();
    }

    public void SkipValue(ISerdeInfo info, int index)
    {
        throw new NotImplementedException();
    }

    public int TryReadIndex(ISerdeInfo info, out string? errorName)
    {
        throw new NotImplementedException();
    }
}