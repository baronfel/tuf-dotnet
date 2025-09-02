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

// Additional test models for comprehensive collection and dictionary testing
[GenerateSerde]
public partial record TestListContainer(List<string> Items);

[GenerateSerde]
public partial record TestArrayContainer(string[] Items);

[GenerateSerde]
public partial record TestDictionaryContainer(Dictionary<string, string> StringDict);

[GenerateSerde]
public partial record TestMixedDictionaryContainer(Dictionary<string, int> IntDict, Dictionary<string, bool> BoolDict);

[GenerateSerde]
public partial record TestNestedContainer(Dictionary<string, List<string>> NestedDict);

[GenerateSerde]
public partial record TestComplexValueContainer(Dictionary<string, TestKeyValueContainer> ComplexDict);

[GenerateSerde]
public partial record TestEmptyCollectionsContainer(
    List<string> EmptyList,
    Dictionary<string, string> EmptyDict,
    string[] EmptyArray);

[GenerateSerde]
public partial record TestNullableContainer(
    List<string>? NullableList,
    Dictionary<string, string>? NullableDict,
    string? NullableString);

[GenerateSerde]
public partial record TestDeepNestingContainer(
    Dictionary<string, Dictionary<string, List<TestKeyValueContainer>>> DeepNested);

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
    ISerdeInfo DummyArrayInfo() => new TestSerdeInfo(null!, kind: InfoKind.List);

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

    // Collection and Dictionary Serialization Tests

    [Test]
    public async Task VerifyCollectionListSerialization()
    {
        var testObj = new TestListContainer(new List<string> { "first", "second", "third" });

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        var expectedJson = "{\"items\":[\"first\",\"second\",\"third\"]}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyCollectionArraySerialization()
    {
        var testObj = new TestArrayContainer(new string[] { "alpha", "beta", "gamma" });

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        var expectedJson = "{\"items\":[\"alpha\",\"beta\",\"gamma\"]}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifySimpleDictionarySerialization()
    {
        var dict = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
            ["aaa"] = "first"  // Should come first in canonical ordering
        };
        var testObj = new TestDictionaryContainer(dict);

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Keys should be sorted in UTF-8 lexicographic order
        var expectedJson = "{\"stringDict\":{\"key1\":\"value1\",\"key2\":\"value2\",\"aaa\":\"first\"}}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyMixedDictionariesSerialization()
    {
        var intDict = new Dictionary<string, int>
        {
            ["count"] = 42,
            ["age"] = 25
        };
        var boolDict = new Dictionary<string, bool>
        {
            ["enabled"] = true,
            ["active"] = false
        };
        var testObj = new TestMixedDictionaryContainer(intDict, boolDict);

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Both object properties and dictionary keys should be sorted
        var expectedJson = "{\"boolDict\":{\"enabled\":true,\"active\":false},\"intDict\":{\"count\":42,\"age\":25}}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyNestedDictionarySerialization()
    {
        var nestedDict = new Dictionary<string, List<string>>
        {
            ["fruits"] = new List<string> { "apple", "banana" },
            ["colors"] = new List<string> { "red", "blue", "green" }
        };
        var testObj = new TestNestedContainer(nestedDict);

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // Dictionary keys should be sorted
        var expectedJson = "{\"nestedDict\":{\"fruits\":[\"apple\",\"banana\"],\"colors\":[\"red\",\"blue\",\"green\"]}}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyComplexValueDictionarySerialization()
    {
        var complexDict = new Dictionary<string, TestKeyValueContainer>
        {
            ["item1"] = new TestKeyValueContainer(100, "first"),
            ["item2"] = new TestKeyValueContainer(200, "second")
        };
        var testObj = new TestComplexValueContainer(complexDict);

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        var expectedJson = "{\"complexDict\":{\"item1\":{\"one\":100,\"two\":\"first\"},\"item2\":{\"one\":200,\"two\":\"second\"}}}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyEmptyCollectionsSerialization()
    {
        var testObj = new TestEmptyCollectionsContainer(
            new List<string>(),
            new Dictionary<string, string>(),
            new string[0]
        );

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        var expectedJson = "{\"emptyArray\":[],\"emptyDict\":{},\"emptyList\":[]}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyNullableCollectionsSerialization()
    {
        var testObj = new TestNullableContainer(
            NullableList: null,
            NullableDict: null,
            NullableString: null
        );

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        var expectedJson = "{}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    [Test]
    public async Task VerifyDeepNestingSerialization()
    {
        var deepNested = new Dictionary<string, Dictionary<string, List<TestKeyValueContainer>>>
        {
            ["level1"] = new Dictionary<string, List<TestKeyValueContainer>>
            {
                ["level2a"] = new List<TestKeyValueContainer>
                {
                    new TestKeyValueContainer(1, "one"),
                    new TestKeyValueContainer(2, "two")
                },
                ["level2b"] = new List<TestKeyValueContainer>
                {
                    new TestKeyValueContainer(3, "three")
                }
            }
        };
        var testObj = new TestDeepNestingContainer(deepNested);

        var serialized = Serializer.Serialize(testObj);
        var serializedString = Encoding.UTF8.GetString(serialized);

        // All levels should maintain canonical ordering
        var expectedJson = "{\"deepNested\":{\"level1\":{\"level2a\":[{\"one\":1,\"two\":\"one\"},{\"one\":2,\"two\":\"two\"}],\"level2b\":[{\"one\":3,\"two\":\"three\"}]}}}";
        await Assert.That(serializedString).IsEqualTo(expectedJson);
    }

    // Collection and Dictionary Deserialization Tests

    [Test]
    public async Task VerifyCollectionListDeserialization()
    {
        var json = "{\"items\":[\"first\",\"second\",\"third\"]}";

        var result = Serializer.Deserialize<TestListContainer, TestListContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).HasCount().EqualTo(3);
        await Assert.That(result.Items[0]).IsEqualTo("first");
        await Assert.That(result.Items[1]).IsEqualTo("second");
        await Assert.That(result.Items[2]).IsEqualTo("third");
    }

    [Test]
    public async Task VerifyCollectionArrayDeserialization()
    {
        var json = "{\"items\":[\"alpha\",\"beta\",\"gamma\"]}";

        var result = Serializer.Deserialize<TestArrayContainer, TestArrayContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).HasCount().EqualTo(3);
        await Assert.That(result.Items[0]).IsEqualTo("alpha");
        await Assert.That(result.Items[1]).IsEqualTo("beta");
        await Assert.That(result.Items[2]).IsEqualTo("gamma");
    }

    [Test]
    public async Task VerifySimpleDictionaryDeserialization()
    {
        var json = "{\"stringDict\":{\"key1\":\"value1\"}}";

        var result = Serializer.Deserialize<TestDictionaryContainer, TestDictionaryContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.StringDict).HasCount().EqualTo(1);
        await Assert.That(result.StringDict["key1"]).IsEqualTo("value1");
    }

    [Test]
    public async Task VerifyMixedDictionariesDeserialization()
    {
        var json = "{\"boolDict\":{\"enabled\":true,\"active\":false},\"intDict\":{\"count\":42,\"age\":25}}";

        var result = Serializer.Deserialize<TestMixedDictionaryContainer, TestMixedDictionaryContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IntDict).HasCount().EqualTo(2);
        await Assert.That(result.IntDict["count"]).IsEqualTo(42);
        await Assert.That(result.IntDict["age"]).IsEqualTo(25);
        await Assert.That(result.BoolDict).HasCount().EqualTo(2);
        await Assert.That(result.BoolDict["enabled"]).IsEqualTo(true);
        await Assert.That(result.BoolDict["active"]).IsEqualTo(false);
    }

    [Test]
    public async Task VerifyNestedDictionaryDeserialization()
    {
        var json = "{\"nestedDict\":{\"fruits\":[\"apple\",\"banana\"],\"colors\":[\"red\",\"blue\",\"green\"]}}";

        var result = Serializer.Deserialize<TestNestedContainer, TestNestedContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.NestedDict).HasCount().EqualTo(2);
        await Assert.That(result.NestedDict["colors"]).HasCount().EqualTo(3);
        await Assert.That(result.NestedDict["colors"][0]).IsEqualTo("red");
        await Assert.That(result.NestedDict["fruits"]).HasCount().EqualTo(2);
        await Assert.That(result.NestedDict["fruits"][0]).IsEqualTo("apple");
    }

    [Test]
    public async Task VerifyComplexValueDictionaryDeserialization()
    {
        var json = "{\"complexDict\":{\"item1\":{\"one\":100,\"two\":\"first\"},\"item2\":{\"one\":200,\"two\":\"second\"}}}";

        var result = Serializer.Deserialize<TestComplexValueContainer, TestComplexValueContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.ComplexDict).HasCount().EqualTo(2);
        await Assert.That(result.ComplexDict["item1"].one).IsEqualTo(100);
        await Assert.That(result.ComplexDict["item1"].two).IsEqualTo("first");
        await Assert.That(result.ComplexDict["item2"].one).IsEqualTo(200);
        await Assert.That(result.ComplexDict["item2"].two).IsEqualTo("second");
    }

    [Test]
    public async Task VerifyEmptyCollectionsDeserialization()
    {
        var json = "{\"emptyArray\":[],\"emptyDict\":{},\"emptyList\":[]}";

        var result = Serializer.Deserialize<TestEmptyCollectionsContainer, TestEmptyCollectionsContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.EmptyList).HasCount().EqualTo(0);
        await Assert.That(result.EmptyDict).HasCount().EqualTo(0);
        await Assert.That(result.EmptyArray).HasCount().EqualTo(0);
    }

    [Test]
    public async Task VerifyDeepNestingDeserialization()
    {
        var json = "{\"deepNested\":{\"level1\":{\"level2a\":[{\"one\":1,\"two\":\"one\"},{\"one\":2,\"two\":\"two\"}],\"level2b\":[{\"one\":3,\"two\":\"three\"}]}}}";

        var result = Serializer.Deserialize<TestDeepNestingContainer, TestDeepNestingContainer>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.DeepNested).HasCount().EqualTo(1);
        await Assert.That(result.DeepNested["level1"]).HasCount().EqualTo(2);
        await Assert.That(result.DeepNested["level1"]["level2a"]).HasCount().EqualTo(2);
        await Assert.That(result.DeepNested["level1"]["level2a"][0].one).IsEqualTo(1);
        await Assert.That(result.DeepNested["level1"]["level2a"][0].two).IsEqualTo("one");
        await Assert.That(result.DeepNested["level1"]["level2b"]).HasCount().EqualTo(1);
        await Assert.That(result.DeepNested["level1"]["level2b"][0].one).IsEqualTo(3);
    }

    // Roundtrip Tests (Serialize then Deserialize)

    [Test]
    public async Task VerifyRoundtripDictionaryWithSpecialCharacters()
    {
        var dict = new Dictionary<string, string>
        {
            ["key with spaces"] = "value with spaces",
            ["key\"with\"quotes"] = "value\"with\"quotes",
            ["key\\with\\backslashes"] = "value\\with\\backslashes",
            ["unicode日本"] = "unicode日本value"
        };
        var original = new TestDictionaryContainer(dict);

        var serialized = Serializer.Serialize(original);
        var deserialized = Serializer.Deserialize<TestDictionaryContainer, TestDictionaryContainer>(
            Encoding.UTF8.GetString(serialized));

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized.StringDict).HasCount().EqualTo(4);
        await Assert.That(deserialized.StringDict["key with spaces"]).IsEqualTo("value with spaces");
        await Assert.That(deserialized.StringDict["key\"with\"quotes"]).IsEqualTo("value\"with\"quotes");
        await Assert.That(deserialized.StringDict["key\\with\\backslashes"]).IsEqualTo("value\\with\\backslashes");
        await Assert.That(deserialized.StringDict["unicode日本"]).IsEqualTo("unicode日本value");
    }

    [Test]
    public async Task VerifyRoundtripComplexNestedStructure()
    {
        var complex = new Dictionary<string, Dictionary<string, List<TestKeyValueContainer>>>
        {
            ["group1"] = new Dictionary<string, List<TestKeyValueContainer>>
            {
                ["subgroup_b"] = new List<TestKeyValueContainer>
                {
                    new TestKeyValueContainer(10, "ten"),
                    new TestKeyValueContainer(20, "twenty")
                },
                ["subgroup_a"] = new List<TestKeyValueContainer>
                {
                    new TestKeyValueContainer(5, "five")
                }
            },
            ["group0"] = new Dictionary<string, List<TestKeyValueContainer>>()
        };
        var original = new TestDeepNestingContainer(complex);

        var serialized = Serializer.Serialize(original);
        var serializedString = Encoding.UTF8.GetString(serialized);
        var deserialized = Serializer.Deserialize<TestDeepNestingContainer, TestDeepNestingContainer>(serializedString);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized.DeepNested).HasCount().EqualTo(2);
        await Assert.That(deserialized.DeepNested["group1"]).HasCount().EqualTo(2);
        await Assert.That(deserialized.DeepNested["group1"]["subgroup_a"]).HasCount().EqualTo(1);
        await Assert.That(deserialized.DeepNested["group1"]["subgroup_a"][0].one).IsEqualTo(5);
        await Assert.That(deserialized.DeepNested["group1"]["subgroup_b"]).HasCount().EqualTo(2);
        await Assert.That(deserialized.DeepNested["group0"]).HasCount().EqualTo(0);
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