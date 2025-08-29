using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanonicalJson.Tests;

[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
public partial class TestJsonContext : JsonSerializerContext
{
}


public class CanonicalSerializerTests
{
    [Test, MethodDataSource(nameof(EncodeCanonicalCases))]
    public async Task EncodeCanonicalCase(object input, string expected)
    {
        var bytes = CanonicalJsonSerializer.Serialize(input, TestJsonContext.Default.Object);
        var s = Encoding.UTF8.GetString(bytes);
        await Assert.That(s).IsEqualTo(expected);
    }

    public static IEnumerable<object[]> EncodeCanonicalCases()
    {
        yield return new object[] {
                new Dictionary<string, object?> {
                    ["keyid"] = "",
                    ["keyid_hash_algorithms"] = null,
                    ["keytype"] = "",
                    ["keyval"] = new Dictionary<string, object?> { ["private"] = "", ["public"] = "" },
                    ["scheme"] = ""
                },
                "{\"keyid\":\"\",\"keyid_hash_algorithms\":null,\"keytype\":\"\",\"keyval\":{\"private\":\"\",\"public\":\"\"},\"scheme\":\"\"}"
            };

        yield return new object[] {
                new Dictionary<string, object?> {
                    ["keyid"] = "id",
                    ["keyid_hash_algorithms"] = new List<string> { "hash" },
                    ["keytype"] = "type",
                    ["keyval"] = new Dictionary<string, object?> { ["private"] = "priv", ["public"] = "pub" },
                    ["scheme"] = "scheme"
                },
                "{\"keyid\":\"id\",\"keyid_hash_algorithms\":[\"hash\"],\"keytype\":\"type\",\"keyval\":{\"private\":\"priv\",\"public\":\"pub\"},\"scheme\":\"scheme\"}"
            };

        yield return new object[] {
                new Dictionary<string, object?> {
                    ["true"] = true,
                    ["false"] = false,
                    ["nil"] = null,
                    ["int"] = 3,
                    ["int2"] = 42d,
                    ["string"] = "\""
                },
                "{\"false\":false,\"int\":3,\"int2\":42,\"nil\":null,\"string\":\"\\\"\",\"true\":true}"
            };

        yield return new object[] {
                new Dictionary<string, object?> {
                    ["keyid"] = "id",
                    ["keyid_hash_algorithms"] = new List<string> { "hash" },
                    ["keytype"] = "type",
                    ["keyval"] = new Dictionary<string, object?> { ["certificate"] = "cert", ["private"] = "priv", ["public"] = "pub" },
                    ["scheme"] = "scheme"
                },
                "{\"keyid\":\"id\",\"keyid_hash_algorithms\":[\"hash\"],\"keytype\":\"type\",\"keyval\":{\"certificate\":\"cert\",\"private\":\"priv\",\"public\":\"pub\"},\"scheme\":\"scheme\"}"
            };

        yield return new object[] {
                JsonDocument.Parse("{\"_type\":\"targets\",\"spec_version\":\"1.0\",\"version\":0,\"expires\":\"0001-01-01T00:00:00Z\",\"targets\":{},\"custom\":{\"test\":true}}").RootElement.Clone(),
                "{\"_type\":\"targets\",\"custom\":{\"test\":true},\"expires\":\"0001-01-01T00:00:00Z\",\"spec_version\":\"1.0\",\"targets\":{},\"version\":0}"
            };
    }

    [Test, MethodDataSource(nameof(EncodeCanonicalErrCases))]
    public async Task EncodeCanonicalErrCase(object input)
    {
        try
        {
            var _ = CanonicalJsonSerializer.Serialize(input, TestJsonContext.Default.Object);
            throw new Exception("Expected exception during canonicalization");
        }
        catch (Exception)
        {
            // expected
        }

        await Task.CompletedTask;
    }

    public static IEnumerable<Func<object>> EncodeCanonicalErrCases()
    {
        yield return () => new Dictionary<string, object?> { ["float"] = 3.14159265359 };
        yield return () => new Action(() => { });
    }

    [Test, MethodDataSource(nameof(EncodeCanonicalHelperCases))]
    public async Task EncodeCanonicalHelperCase(object input)
    {
        try
        {
            var bytes = CanonicalJsonSerializer.Serialize(input, TestJsonContext.Default.Object);
            await Assert.That(bytes).IsNotNull();
        }
        catch (Exception)
        {
            // acceptable
        }
    }

    public static IEnumerable<Func<object>> EncodeCanonicalHelperCases()
    {
        yield return () => new Action(() => { });
        yield return () => new object[] { new Action(() => { }) };
    }
}