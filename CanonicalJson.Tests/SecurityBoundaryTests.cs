using System.Text;
using TUnit.Core;

namespace CanonicalJson.Tests;

/// <summary>
/// Security boundary tests for CanonicalJson functionality.
/// These tests focus on edge cases, attack scenarios, and security-relevant code paths.
/// </summary>
public class SecurityBoundaryTests
{
    [Test]
    public async Task CompareUtf8_Basic_Ordering_Test()
    {
        // Test basic UTF-8 lexicographic ordering
        var result1 = Serializer.CompareUtf8("a", "b");
        var result2 = Serializer.CompareUtf8("b", "a");  
        var result3 = Serializer.CompareUtf8("test", "test");
        
        await Assert.That(result1).IsLessThan(0);  // "a" < "b"
        await Assert.That(result2).IsGreaterThan(0);  // "b" > "a"
        await Assert.That(result3).IsEqualTo(0);  // "test" == "test"
    }
    
    [Test]
    public async Task CompareUtf8_Different_Lengths_Test()
    {
        // Test strings with different lengths
        var result1 = Serializer.CompareUtf8("a", "aa");
        var result2 = Serializer.CompareUtf8("aa", "a");
        var result3 = Serializer.CompareUtf8("abc", "abcd");
        
        await Assert.That(result1).IsLessThan(0);  // shorter string comes first
        await Assert.That(result2).IsGreaterThan(0);  // longer string comes last
        await Assert.That(result3).IsLessThan(0);  // "abc" < "abcd"
    }
    
    [Test]
    public async Task CompareUtf8_Empty_Strings_Test()
    {
        // Test empty strings - important security boundary
        var result1 = Serializer.CompareUtf8("", "");
        var result2 = Serializer.CompareUtf8("", "a");
        var result3 = Serializer.CompareUtf8("a", "");
        
        await Assert.That(result1).IsEqualTo(0);  // both empty
        await Assert.That(result2).IsLessThan(0);  // empty string first
        await Assert.That(result3).IsGreaterThan(0);  // non-empty string last
    }
    
    [Test]
    public async Task CompareUtf8_Unicode_Characters_Test()
    {
        // Test Unicode characters - security relevant for international attacks
        var result1 = Serializer.CompareUtf8("café", "cafe");  // é vs e
        var result2 = Serializer.CompareUtf8("αβγ", "abc");  // Greek vs Latin
        var result3 = Serializer.CompareUtf8("한국어", "中文");  // Korean vs Chinese
        
        // These should all have consistent ordering based on UTF-8 byte values
        await Assert.That(result1).IsNotEqualTo(0);
        await Assert.That(result2).IsNotEqualTo(0);
        await Assert.That(result3).IsNotEqualTo(0);
    }
    
    [Test]
    public async Task CompareUtf8_Control_Characters_Test()
    {
        // Test control characters and special bytes - potential security issue
        var result1 = Serializer.CompareUtf8("test\0", "test");  // null terminator
        var result2 = Serializer.CompareUtf8("test\n", "test");  // newline
        var result3 = Serializer.CompareUtf8("test\t", "test");  // tab
        
        // Note: null byte (0) < 't' (116), so "test\0" has longer length and should be > "test"
        await Assert.That(result1).IsGreaterThan(0);  // "test\0" > "test" (longer)
        await Assert.That(result2).IsGreaterThan(0);  // "test\n" > "test" (longer)
        await Assert.That(result3).IsGreaterThan(0);  // "test\t" > "test" (longer)
    }
    
    [Test]
    public async Task CompareUtf8_Case_Sensitivity_Test()
    {
        // Test case sensitivity - important for security (case-sensitive keys)
        var result1 = Serializer.CompareUtf8("Test", "test");
        var result2 = Serializer.CompareUtf8("TEST", "test");
        var result3 = Serializer.CompareUtf8("AbC", "aBc");
        
        await Assert.That(result1).IsLessThan(0);  // "T" < "t" (uppercase first)
        await Assert.That(result2).IsLessThan(0);  // "TEST" < "test"
        await Assert.That(result3).IsLessThan(0);  // "A" < "a"
    }
    
    [Test]
    public async Task CompareUtf8_Numeric_Strings_Test()
    {
        // Test numeric strings - important for version comparison attacks
        var result1 = Serializer.CompareUtf8("1", "2");
        var result2 = Serializer.CompareUtf8("10", "2");  // lexicographic, not numeric!
        var result3 = Serializer.CompareUtf8("1.0", "1.10");
        
        await Assert.That(result1).IsLessThan(0);  // "1" < "2"
        await Assert.That(result2).IsLessThan(0);  // "10" < "2" lexicographically
        await Assert.That(result3).IsLessThan(0);  // "1.0" < "1.10"
    }
    
    [Test]
    public async Task CompareUtf8_Special_JSON_Characters_Test()
    {
        // Test JSON special characters - security relevant for injection
        var result1 = Serializer.CompareUtf8("{", "}");
        var result2 = Serializer.CompareUtf8("[", "]");
        var result3 = Serializer.CompareUtf8("\"", "'");
        var result4 = Serializer.CompareUtf8("\\", "/");
        
        await Assert.That(result1).IsLessThan(0);  // "{" < "}"
        await Assert.That(result2).IsLessThan(0);  // "[" < "]"
        await Assert.That(result3).IsLessThan(0);  // "\"" < "'"
        await Assert.That(result4).IsGreaterThan(0);  // "\\" > "/"
    }
    
    [Test]
    public async Task CompareUtf8_Very_Long_Strings_Test()
    {
        // Test very long strings - potential DoS attack vector
        var longString1 = new string('a', 1000);
        var longString2 = new string('a', 999) + 'b';
        var longString3 = new string('a', 1000);
        
        var result1 = Serializer.CompareUtf8(longString1, longString2);
        var result2 = Serializer.CompareUtf8(longString1, longString3);
        
        await Assert.That(result1).IsLessThan(0);  // 1000 a's < 999 a's + 'b'
        await Assert.That(result2).IsEqualTo(0);  // identical long strings
    }
    
    [Test]
    public async Task CompareUtf8_Malformed_UTF8_Sequences_Test()
    {
        // Test strings that might contain malformed UTF-8 when converted
        // This is a security boundary test for potential encoding attacks
        var result1 = Serializer.CompareUtf8("valid", "valid");
        var result2 = Serializer.CompareUtf8("test\uFFFD", "test");  // replacement character
        
        await Assert.That(result1).IsEqualTo(0);  // baseline
        await Assert.That(result2).IsNotEqualTo(0);  // replacement char should differ
    }
}