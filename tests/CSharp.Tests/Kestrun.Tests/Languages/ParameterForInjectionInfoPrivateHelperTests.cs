using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using Kestrun.Forms;
using Kestrun.Languages;
using Microsoft.OpenApi;
using MongoDB.Bson;
using PeterO.Cbor;
using Serilog;
using Xunit;

namespace Kestrun.Tests.Languages;

public sealed class ParameterForInjectionInfoPrivateHelperTests
{
    private static readonly Type Sut = typeof(ParameterForInjectionInfo);

    [Fact]
    public void BuildFieldPropertyValue_AndBuildFilePropertyValue_HandleSupportedShapes()
    {
        var values = new[] { "one", "two" };
        Assert.Equal("one", Assert.IsType<string>(Invoke("BuildFieldPropertyValue", typeof(string), values)));
        Assert.Equal(values, Assert.IsType<string[]>(Invoke("BuildFieldPropertyValue", typeof(string[]), values)));

        var objectSingle = Assert.IsType<string>(Invoke("BuildFieldPropertyValue", typeof(object), new[] { "solo" }));
        Assert.Equal("solo", objectSingle);

        var objectMulti = Assert.IsType<string[]>(Invoke("BuildFieldPropertyValue", typeof(object), values));
        Assert.Equal(values, objectMulti);

        Assert.Null(Invoke("BuildFieldPropertyValue", typeof(int[]), values));

        var files = new[]
        {
            new KrFilePart { Name = "file", OriginalFileName = "a.txt", TempPath = "tmp-a" },
            new KrFilePart { Name = "file", OriginalFileName = "b.txt", TempPath = "tmp-b" },
        };

        Assert.Equal("a.txt", Assert.IsType<KrFilePart>(Invoke("BuildFilePropertyValue", typeof(KrFilePart), files)).OriginalFileName);
        Assert.Equal(files, Assert.IsType<KrFilePart[]>(Invoke("BuildFilePropertyValue", typeof(KrFilePart[]), files)));

        var fileObject = Assert.IsAssignableFrom<KrFilePart[]>(Invoke("BuildFilePropertyValue", typeof(object), files));
        Assert.Equal(2, fileObject.Length);

        Assert.Null(Invoke("BuildFilePropertyValue", typeof(string), files));
    }

    [Fact]
    public void TryPopulateFormDataObjectProperties_BindsFieldsAndFiles()
    {
        var payload = new KrFormData();
        payload.Fields["note"] = ["hello"];
        payload.Fields["tags"] = ["alpha", "beta"];
        payload.Files["file"] =
        [
            new KrFilePart { Name = "file", OriginalFileName = "demo.txt", TempPath = "tmp-file" }
        ];

        var target = new FormDataTarget();
        using var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

        _ = Invoke("TryPopulateFormDataObjectProperties", target, payload, typeof(FormDataTarget), logger);

        Assert.Equal("hello", target.Note);
        Assert.NotNull(target.Tags);
        Assert.Equal(["alpha", "beta"], target.Tags);
        Assert.NotNull(target.File);
        Assert.Equal("demo.txt", target.File.OriginalFileName);
    }

    [Fact]
    public void ConvertFormHelpers_ReturnExpectedValues()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = "42",
            ["name"] = "demo"
        };

        var table = Assert.IsType<Hashtable>(Invoke("ConvertFormToHashtable", form));
        Assert.Equal("42", table["item"]);
        Assert.Equal("demo", table["name"]);

        var stringParam = CreateParameter("item", JsonSchemaType.String);
        var simpleValue = Invoke("ConvertFormToValue", new Dictionary<string, string> { ["item"] = "42" }, stringParam);
        Assert.Equal("42", Assert.IsType<string>(simpleValue));

        var objectParam = CreateParameter("item", JsonSchemaType.Object);
        var objectValue = Assert.IsType<Hashtable>(Invoke("ConvertFormToValue", form, objectParam));
        Assert.Equal("42", objectValue["item"]);

        Assert.Null(Invoke("ConvertFormToHashtable", (object?)null));
        Assert.Null(Invoke("ConvertFormToValue", null, objectParam));
    }

    [Fact]
    public void DecodeHelpers_HandleBase64HexAndUtf8Fallback()
    {
        var fromBase64 = Assert.IsType<byte[]>(Invoke("DecodeBodyStringToBytes", "base64:SGVsbG8="));
        Assert.Equal("Hello", Encoding.UTF8.GetString(fromBase64));

        var fromHex = Assert.IsType<byte[]>(Invoke("DecodeBodyStringToBytes", "48656c6c6f"));
        Assert.Equal("Hello", Encoding.UTF8.GetString(fromHex));

        var fromUtf8 = Assert.IsType<byte[]>(Invoke("DecodeBodyStringToBytes", "plain-text"));
        Assert.Equal("plain-text", Encoding.UTF8.GetString(fromUtf8));

        var invalidBase64Args = new object?[] { "bad", null };
        Assert.False(InvokeBool("TryDecodeBase64", invalidBase64Args));

        var invalidHexArgs = new object?[] { "0xGG", null };
        Assert.False(InvokeBool("TryDecodeHex", invalidHexArgs));
    }

    [Fact]
    public void BsonAndCborConverters_ReturnNestedClrValues()
    {
        var bsonDoc = new BsonDocument
        {
            { "name", "demo" },
            { "count", 7 },
            { "enabled", true },
            { "items", new BsonArray { "a", "b" } },
            { "nested", new BsonDocument { { "key", "value" } } }
        };
        var bsonBytes = bsonDoc.ToBson();
        var bsonValue = Assert.IsType<Hashtable>(Invoke("ConvertBsonToHashtable", "base64:" + Convert.ToBase64String(bsonBytes)));
        Assert.Equal("demo", bsonValue["name"]);
        Assert.Equal(7, Convert.ToInt32(bsonValue["count"], CultureInfo.InvariantCulture));
        Assert.Equal(true, bsonValue["enabled"]);

        var cborMap = CBORObject.NewMap()
            .Add("name", "demo")
            .Add("count", 9)
            .Add("enabled", true)
            .Add("items", CBORObject.NewArray().Add("x").Add("y"));
        var cborBytes = cborMap.EncodeToBytes();
        var cborValue = Assert.IsType<Hashtable>(Invoke("ConvertCborToHashtable", "base64:" + Convert.ToBase64String(cborBytes)));
        Assert.Equal("demo", cborValue["name"]);
        Assert.Equal(9L, Assert.IsType<long>(cborValue["count"]));
        Assert.Equal(true, cborValue["enabled"]);
    }

    [Fact]
    public void ScalarConversionHelpers_ParsePrimitiveAndEnumValues()
    {
        var intArgs = new object?[] { "42", typeof(int), null };
        Assert.True(InvokeBool("TryConvertPrimitiveScalar", intArgs));
        Assert.Equal(42, Assert.IsType<int>(intArgs[2]));

        var boolArgs = new object?[] { "true", typeof(bool), null };
        Assert.True(InvokeBool("TryConvertPrimitiveScalar", boolArgs));
        Assert.True(Assert.IsType<bool>(boolArgs[2]));

        var enumArgs = new object?[] { "running", typeof(SampleState), null };
        Assert.True(InvokeBool("TryConvertScalarByType", enumArgs));
        Assert.Equal(SampleState.Running, Assert.IsType<SampleState>(enumArgs[2]));

        var invalidEnumArgs = new object?[] { "missing", typeof(SampleState), null };
        Assert.False(InvokeBool("TryConvertScalarByType", invalidEnumArgs));

        Assert.Equal("42", Assert.IsType<string>(Invoke("TryChangeType", 42, typeof(string))));
    }

    [Fact]
    public void EnumerableAndHashtableConverters_ReturnExpectedTargetTypes()
    {
        var intArray = Assert.IsType<int[]>(Invoke("ConvertEnumerableToTargetType", new List<object?> { "1", "2" }, typeof(int[]), 0, null));
        Assert.Equal([1, 2], intArray);

        var intList = Assert.IsAssignableFrom<IList>(Invoke("ConvertEnumerableToTargetType", new List<object?> { "3", "4" }, typeof(List<int>), 0, null));
        Assert.Equal(2, intList.Count);
        Assert.Equal(3, intList[0]);
        Assert.Equal(4, intList[1]);

        var table = new Hashtable(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "demo",
            ["count"] = "5"
        };

        var dictionaryConversionArgs = new object?[] { table, typeof(IDictionary), 0, null, null };
        Assert.True(InvokeBool("TryConvertHashtableValue", dictionaryConversionArgs));
        Assert.Same(table, dictionaryConversionArgs[4]);

        var modelConversionArgs = new object?[] { table, typeof(ModelTarget), 0, null, null };
        Assert.True(InvokeBool("TryConvertHashtableValue", modelConversionArgs));
        var model = Assert.IsType<ModelTarget>(modelConversionArgs[4]);
        Assert.Equal("demo", model.Name);
        Assert.Equal(5, model.Count);

        var getValueArgs = new object?[] { table, "NAME", null };
        Assert.True(InvokeBool("TryGetHashtableValue", getValueArgs));
        Assert.Equal("demo", Assert.IsType<string>(getValueArgs[2]));
    }

    [Fact]
    public void CoerceFormPayloadForParameter_WithFormPayload_ReturnsExpectedPayloadType()
    {
        var payload = new KrFormData();
        payload.Fields["title"] = ["hello"];
        payload.Fields["tags"] = ["x", "y"];
        payload.Files["file"] =
        [
            new KrFilePart
            {
                Name = "file",
                OriginalFileName = "demo.txt",
                TempPath = "temp"
            }
        ];

        using var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

        var result = ParameterForInjectionInfo.CoerceFormPayloadForParameter(typeof(FormDataTarget), payload, logger);
        var model = Assert.IsType<KrFormData>(result);
        Assert.Equal("hello", model.Fields["title"][0]);
        Assert.Equal("x", model.Fields["tags"][0]);
        Assert.Equal("demo.txt", model.Files["file"][0].OriginalFileName);
    }

    [Fact]
    public void TryReadPartAsString_ReadsUtf8AndReturnsNullWhenMissing()
    {
        using var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var tempPath = Path.Combine(Path.GetTempPath(), $"kestrun-part-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempPath, "payload", Encoding.UTF8);
            var existingPart = new KrRawPart { Name = "data", TempPath = tempPath };
            Assert.Equal("payload", Assert.IsType<string>(Invoke("TryReadPartAsString", existingPart, logger)));

            var missingPart = new KrRawPart { Name = "missing", TempPath = tempPath + ".none" };
            Assert.Null(Invoke("TryReadPartAsString", missingPart, logger));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static ParameterForInjectionInfo CreateParameter(string name, JsonSchemaType schemaType)
    {
        var metadata = new ParameterMetadata(name, typeof(object));
        var parameter = new OpenApiParameter
        {
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = schemaType,
            }
        };

        return new ParameterForInjectionInfo(metadata, parameter);
    }

    private static object? Invoke(string methodName, params object?[] args)
    {
        var method = GetMethod(methodName, args.Length);
        return method.Invoke(null, args);
    }

    private static bool InvokeBool(string methodName, params object?[] args)
    {
        var value = Invoke(methodName, args);
        Assert.NotNull(value);
        return Assert.IsType<bool>(value);
    }

    private static MethodInfo GetMethod(string methodName, int argumentCount)
    {
        var method = Sut
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal) && m.GetParameters().Length == argumentCount)
            .OrderByDescending(static m => m.GetParameters().Count(static p => p.ParameterType.IsByRef))
            .FirstOrDefault();

        Assert.NotNull(method);
        return method;
    }

    private sealed class FormDataTarget
    {
        public string? Note { get; set; }

        public string[]? Tags { get; set; }

        public KrFilePart? File { get; set; }
    }

    private sealed class ModelTarget
    {
        public string? Name { get; set; }

        public int Count { get; set; }
    }

    private enum SampleState
    {
        Running,
        Stopped
    }
}
