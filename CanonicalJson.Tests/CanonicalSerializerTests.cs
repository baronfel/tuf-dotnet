using System.Reflection;
using System.Text;

using Serde;

namespace CanonicalJson.Tests;

// Test models for serialization testing with golden data
[GenerateSerde]
public partial record TestStringContainer(
    [property: SerdeMemberOptions(Rename = "BoolStr")] string BoolStr,
    [property: SerdeMemberOptions(Rename = "IntStr")] string IntStr,
    [property: SerdeMemberOptions(Rename = "StrStr")] string StrStr);

[GenerateSerde]
public partial record TestUnicodeContainer(string a);

[GenerateSerde]
public partial record TestKeyValueContainer(int one, string two);

[GenerateSerde]
public partial record TestJapaneseContainer(int 日, int 本);

public partial class CanonicalSerializerTests
{
    [Test]
    public async Task VerifyUtf8LexicographicOrdering()
    {
        // Test that UTF-8 byte ordering is correct
        // Numbers come before uppercase, which come before lowercase
        var a = Serializer.CompareUtf8("1", "A");
        var b = Serializer.CompareUtf8("A", "a");
        var c = Serializer.CompareUtf8("a", "z");

        await Assert.That(a).IsLessThan(0); // "1" < "A"
        await Assert.That(b).IsLessThan(0); // "A" < "a" 
        await Assert.That(c).IsLessThan(0); // "a" < "z"
    }

    [Test]
    public async Task VerifyStringEscaping()
    {
        // Test the internal string escaping functionality
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        writer.WriteString("""Test "quote" and \backslash""");
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "\"Test \\\"quote\\\" and \\\\backslash\"";

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyArraySerialization()
    {
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        var coll = writer.WriteCollection(DummyArrayInfo(), null);
        coll.WriteString(null!, 0, "first");
        coll.WriteI32(null!, 1, 42);
        coll.WriteBool(null!, 2, true);
        coll.End(DummyArrayInfo());
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "[\"first\",42,true]";

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyNullAndBooleanValues()
    {
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        var inner = writer.WriteType(null!);
        inner.WriteNull(DummyPropertyInfo("isNull"), 0);
        inner.WriteBool(DummyPropertyInfo("isTrue"), 0, true);
        inner.WriteBool(DummyPropertyInfo("isFalse"), 0, false);
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"isFalse\":false,\"isNull\":null,\"isTrue\":true}";

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyNumberFormats()
    {
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        var inner = writer.WriteType(null!);
        inner.WriteI32(DummyPropertyInfo("int32"), 0, -42);
        inner.WriteU64(DummyPropertyInfo("uint64"), 0, 123456789UL);
        inner.WriteF64(DummyPropertyInfo("float64"), 0, 3.14159);
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"float64\":3.14159,\"int32\":-42,\"uint64\":123456789}";

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyUnicodeHandling()
    {
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        var inner = writer.WriteType(null!);
        inner.WriteString(DummyPropertyInfo("japanese"), 0, "こんにちは");
        inner.WriteString(DummyPropertyInfo("emoji"), 0, "🚀🌟");
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"emoji\":\"🚀🌟\",\"japanese\":\"こんにちは\"}";

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyGoldenTestData_StringEscaping()
    {
        // Based on cjson golden data: 22201ed8d05c5260e2b9a873347ff6419e5f27cff74bbe948aaed31ca5709817.json
        // Contains: {"BoolStr":"true","IntStr":"42","StrStr":"\"xzbit\""}
        var expectedJson = "{\"BoolStr\":\"true\",\"IntStr\":\"42\",\"StrStr\":\"\\\"xzbit\\\"\"}";

        // Test that our serializer would produce this canonical form
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        var inner = writer.WriteType(null!);
        inner.WriteString(DummyPropertyInfo("BoolStr"), 0, "true");
        inner.WriteString(DummyPropertyInfo("IntStr"), 0, "42");
        inner.WriteString(DummyPropertyInfo("StrStr"), 0, "\"xzbit\"");
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyGoldenTestData_UnicodeCharacter()
    {
        // Based on cjson golden data: 50ecf5a6d95fecca7fb4259d7d30bcee688ea089329e9d8925e7cb4a4fcde52d.json  
        // Contains: {"a":"日"}
        var expectedJson = "{\"a\":\"日\"}";

        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);
        
        var inner = writer.WriteType(null!);
        inner.WriteString(DummyPropertyInfo("a"), 0, "日"); // Japanese character for "day/sun"
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result).IsEqualTo(expectedJson);
    }

    [Test] 
    public async Task VerifyGoldenTestData_SimpleKeyValue()
    {
        // Based on cjson golden data: 64b198dfda3dbd1c4a8c83f2ce8e9d4474b51e785b61bdb13c219a3e7fd7609c.json
        // Contains: {"one":1,"two":"Two"}
        var expectedJson = "{\"one\":1,\"two\":\"Two\"}";

        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        var inner = writer.WriteType(null!);
        inner.WriteI32(DummyPropertyInfo("one"), 0, 1);
        inner.WriteString(DummyPropertyInfo("two"), 0, "Two");
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result).IsEqualTo(expectedJson);
    }

    ISerdeInfo DummyPropertyInfo(string nameYouWant) => new TestSerdeInfo(nameYouWant);
    ISerdeInfo DummyArrayInfo() => new TestSerdeInfo(null, kind: InfoKind.List);

    [Test]
    public async Task VerifyGoldenTestData_JapaneseCharactersWithNumbers() 
    {
        // Based on cjson golden data: 7d9231d659b4fee07dabfbb4545fb55d66ec05cb44b5610aae31f468a22a1dd3.json
        // Contains: {"本":2,"日":1} - UTF-8 lexicographic ordering test
        var expectedJson = "{\"日\":1,\"本\":2}";

        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);

        // According to UTF-8 lexicographic ordering, "日" (65E5 65E5) comes before "本" (672C)
        // so we need to reorder the writes successfully
        var inner = writer.WriteType(null!);
        inner.WriteI32(DummyPropertyInfo("本"), 0, 2);
        inner.WriteI32(DummyPropertyInfo("日"), 0, 1);
        inner.End(null!);
        writer.Dispose();

        var result = Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyUtf8OrderingOfJapaneseCharacters()
    {
        // Verify that our UTF-8 comparison correctly orders Japanese characters
        var comparison = Serializer.CompareUtf8("日", "本");
        await Assert.That(comparison).IsLessThan(0); // "日" should come before "本"
    }


    // TODO: Implement deserialization tests once Serde.NET deserialization API is working
    //[Test]
    //public async Task VerifyGoldenData_Deserialization_StringEscaping()
    //{
    //    // Golden data from cjson: 22201ed8d05c5260e2b9a873347ff6419e5f27cff74bbe948aaed31ca5709817.json
    //    var goldenJson = "{\"BoolStr\":\"true\",\"IntStr\":\"42\",\"StrStr\":\"\\\"xzbit\\\"\"}";
    //    
    //    var result = CanonicalJsonSerializer.Deserialize<TestStringContainer>(goldenJson);
    //    
    //    await Assert.That(result.BoolStr).IsEqualTo("true");
    //    await Assert.That(result.IntStr).IsEqualTo("42");
    //    await Assert.That(result.StrStr).IsEqualTo("\"xzbit\"");
    //}

    [Test]
    public async Task VerifyGoldenData_Serialization_StringEscaping()
    {
        // Test serialization produces the canonical form from golden data
        var testObj = new TestStringContainer("true", "42", "\"xzbit\"");

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Expected golden data from cjson: 22201ed8d05c5260e2b9a873347ff6419e5f27cff74bbe948aaed31ca5709817.json
        var expectedJson = "{\"BoolStr\":\"true\",\"IntStr\":\"42\",\"StrStr\":\"\\\"xzbit\\\"\"}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyGoldenData_Serialization_UnicodeCharacter()
    {
        // Test serialization produces the canonical form from golden data
        var testObj = new TestUnicodeContainer("日");

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Expected golden data from cjson: 50ecf5a6d95fecca7fb4259d7d30bcee688ea089329e9d8925e7cb4a4fcde52d.json
        var expectedJson = "{\"a\":\"日\"}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyGoldenData_Serialization_SimpleKeyValue()
    {
        // Test serialization produces the canonical form from golden data
        var testObj = new TestKeyValueContainer(1, "Two");

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Expected golden data from cjson: 64b198dfda3dbd1c4a8c83f2ce8e9d4474b51e785b61bdb13c219a3e7fd7609c.json
        var expectedJson = "{\"one\":1,\"two\":\"Two\"}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyGoldenData_Serialization_JapaneseCharacters()
    {
        var testObj = new TestJapaneseContainer(1, 2);
        // Test serialization produces the canonical form from golden data with UTF-8 lexicographic ordering

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Expected golden data from cjson: 7d9231d659b4fee07dabfbb4545fb55d66ec05cb44b5610aae31f468a22a1dd3.json
        var expectedJson = "{\"日\":1,\"本\":2}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }
}

internal class TestSerdeInfo(string nameYouWant, InfoKind? kind = null) : ISerdeInfo
{
    public string Name => nameYouWant;

    public InfoKind Kind => kind ?? InfoKind.Primitive;

    public IList<CustomAttributeData> Attributes => throw new NotImplementedException();

    public int FieldCount => throw new NotImplementedException();

    public IList<CustomAttributeData> GetFieldAttributes(int index)
    {
        throw new NotImplementedException();
    }

    public ISerdeInfo GetFieldInfo(int index)
    {
        throw new NotImplementedException();
    }

    public ReadOnlySpan<byte> GetFieldName(int index) => Encoding.UTF8.GetBytes(nameYouWant);

    public string GetFieldStringName(int index) => nameYouWant;
    
    public int TryGetIndex(ReadOnlySpan<byte> fieldName)
    {
        throw new NotImplementedException();
    }
}