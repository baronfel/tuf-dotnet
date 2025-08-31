using System.Text;
using Serde;

namespace CanonicalJson.Tests;

// Test models for serialization testing with golden data
[GenerateSerde]
public partial record TestStringContainer(string BoolStr, string IntStr, string StrStr);

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
        var a = CanonicalJsonSerializer.CompareUtf8("1", "A");
        var b = CanonicalJsonSerializer.CompareUtf8("A", "a");
        var c = CanonicalJsonSerializer.CompareUtf8("a", "z");
        
        await Assert.That(a).IsLessThan(0); // "1" < "A"
        await Assert.That(b).IsLessThan(0); // "A" < "a" 
        await Assert.That(c).IsLessThan(0); // "a" < "z"
    }

    [Test]
    public async Task VerifyStringEscaping()
    {
        // Test the internal string escaping functionality
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteString("Test \"quote\" and \\backslash");
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "\"Test \\\"quote\\\" and \\\\backslash\"";
        
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test] 
    public async Task VerifyCanonicalJsonWriter()
    {
        // Test the basic writer functionality
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("name");
        writer.WriteColon();
        writer.WriteString("Alice");
        writer.WriteComma();
        writer.WriteString("age");
        writer.WriteColon();
        writer.WriteI32(30);
        writer.WriteObjectEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"name\":\"Alice\",\"age\":30}";
        
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyArraySerialization()
    {
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteArrayStart();
        writer.WriteString("first");
        writer.WriteComma();
        writer.WriteI32(42);
        writer.WriteComma();
        writer.WriteBool(true);
        writer.WriteArrayEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "[\"first\",42,true]";
        
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyNullAndBooleanValues()
    {
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("isNull");
        writer.WriteColon();
        writer.WriteNull();
        writer.WriteComma();
        writer.WriteString("isTrue");
        writer.WriteColon();
        writer.WriteBool(true);
        writer.WriteComma();
        writer.WriteString("isFalse");
        writer.WriteColon();
        writer.WriteBool(false);
        writer.WriteObjectEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"isNull\":null,\"isTrue\":true,\"isFalse\":false}";
        
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyNumberFormats()
    {
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("int32");
        writer.WriteColon();
        writer.WriteI32(-42);
        writer.WriteComma();
        writer.WriteString("uint64");
        writer.WriteColon();
        writer.WriteU64(123456789UL);
        writer.WriteComma();
        writer.WriteString("float64");
        writer.WriteColon();
        writer.WriteF64(3.14159);
        writer.WriteObjectEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"int32\":-42,\"uint64\":123456789,\"float64\":3.1415899999999999}";
        
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task VerifyUnicodeHandling()
    {
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("japanese");
        writer.WriteColon();
        writer.WriteString("こんにちは");
        writer.WriteComma();
        writer.WriteString("emoji");
        writer.WriteColon();
        writer.WriteString("🚀🌟");
        writer.WriteObjectEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        var expected = "{\"japanese\":\"こんにちは\",\"emoji\":\"🚀🌟\"}";
        
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
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("BoolStr");
        writer.WriteColon();
        writer.WriteString("true");
        writer.WriteComma();
        writer.WriteString("IntStr");
        writer.WriteColon();
        writer.WriteString("42");
        writer.WriteComma();
        writer.WriteString("StrStr");
        writer.WriteColon();
        writer.WriteString("\"xzbit\"");
        writer.WriteObjectEnd();
        
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
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("a");
        writer.WriteColon();
        writer.WriteString("日");  // Japanese character for "day/sun"
        writer.WriteObjectEnd();
        
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
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        writer.WriteString("one");
        writer.WriteColon();
        writer.WriteI32(1);
        writer.WriteComma();
        writer.WriteString("two");
        writer.WriteColon();
        writer.WriteString("Two");
        writer.WriteObjectEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyGoldenTestData_JapaneseCharactersWithNumbers() 
    {
        // Based on cjson golden data: 7d9231d659b4fee07dabfbb4545fb55d66ec05cb44b5610aae31f468a22a1dd3.json
        // Contains: {"本":2,"日":1} - UTF-8 lexicographic ordering test
        var expectedJson = "{\"日\":1,\"本\":2}";
        
        using var ms = new MemoryStream();
        using var writer = new CanonicalJsonSerdeWriter(ms);
        
        writer.WriteObjectStart();
        // According to UTF-8 lexicographic ordering, "日" (65E5 65E5) comes before "本" (672C)
        writer.WriteString("日");
        writer.WriteColon();
        writer.WriteI32(1);
        writer.WriteComma();
        writer.WriteString("本");
        writer.WriteColon();
        writer.WriteI32(2);
        writer.WriteObjectEnd();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(result).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyUtf8OrderingOfJapaneseCharacters()
    {
        // Verify that our UTF-8 comparison correctly orders Japanese characters
        var comparison = CanonicalJsonSerializer.CompareUtf8("日", "本");
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
        
        var serialized = CanonicalJsonSerializer.Serialize<TestStringContainer>(testObj);
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
        
        var serialized = CanonicalJsonSerializer.Serialize(testObj);
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
        
        var serialized = CanonicalJsonSerializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);
        
        // Expected golden data from cjson: 64b198dfda3dbd1c4a8c83f2ce8e9d4474b51e785b61bdb13c219a3e7fd7609c.json
        var expectedJson = "{\"one\":1,\"two\":\"Two\"}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyGoldenData_Serialization_JapaneseCharacters()
    {
        // Test serialization produces the canonical form from golden data with UTF-8 lexicographic ordering
        var testObj = new TestJapaneseContainer(1, 2);
        
        var serialized = CanonicalJsonSerializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);
        
        // Expected golden data from cjson: 7d9231d659b4fee07dabfbb4545fb55d66ec05cb44b5610aae31f468a22a1dd3.json
        var expectedJson = "{\"日\":1,\"本\":2}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }
}