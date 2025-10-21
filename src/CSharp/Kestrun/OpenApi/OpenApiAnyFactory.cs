using System.Collections;

namespace Kestrun.OpenApi;

/// <summary>
/// Helpers to create Microsoft.OpenApi.Any.IOpenApiAny from .NET objects.
/// Centralizes the dependency on OpenAPI.Any types on the C# side so PowerShell doesn't need to reflect types.
/// </summary>
public static class OpenApiAnyFactory
{
    public static object FromObject(object? value)
    {
        // Resolve Any types via the Microsoft.OpenApi assembly without compile-time references
        var openApiAsm = typeof(Microsoft.OpenApi.OpenApiDocument).Assembly;
        var anyNs = "Microsoft.OpenApi.Any";
        var tObj = openApiAsm.GetType($"{anyNs}.OpenApiObject");
        var tArr = openApiAsm.GetType($"{anyNs}.OpenApiArray");
        var tStr = openApiAsm.GetType($"{anyNs}.OpenApiString");
        var tAny = openApiAsm.GetType($"{anyNs}.IOpenApiAny");

        // Null-safe fallback
        if (value is null)
        {
            return tStr != null ? Activator.CreateInstance(tStr, string.Empty)! : string.Empty;
        }

        // Hashtable/IDictionary -> OpenApiObject
        if (value is IDictionary dict && tObj != null && tAny != null)
        {
            var obj = Activator.CreateInstance(tObj)!;
            var addMethod = tObj.GetMethod("Add", new[] { typeof(string), tAny });
            foreach (DictionaryEntry de in dict)
            {
                var key = de.Key?.ToString() ?? string.Empty;
                var child = FromObject(de.Value);
                addMethod?.Invoke(obj, new[] { key, child });
            }
            return obj;
        }

        // IEnumerable (non-string) -> OpenApiArray
        if (value is System.Collections.IEnumerable en && value is not string && tArr != null && tAny != null)
        {
            var arr = Activator.CreateInstance(tArr)!;
            var addMethod = tArr.GetMethod("Add", new[] { tAny });
            foreach (var item in en)
            {
                var child = FromObject(item);
                addMethod?.Invoke(arr, new[] { child });
            }
            return arr;
        }

        // Primitives -> OpenApiString (simple, safe default)
        return tStr != null ? Activator.CreateInstance(tStr, value.ToString() ?? string.Empty)! : (object)(value.ToString() ?? string.Empty);
    }
}
