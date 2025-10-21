using System.Collections;

namespace Kestrun.OpenApi;

/// <summary>
/// Helpers to create Microsoft.OpenApi.Any.IOpenApiAny from .NET objects.
/// Centralizes the dependency on OpenAPI.Any types on the C# side so PowerShell doesn't need to reflect types.
/// </summary>
public static class OpenApiAnyFactory
{
    /// <summary>
    /// Create an OpenApiAny from a .NET object.
    /// </summary>
    /// <param name="value">The .NET object to convert.</param>
    /// <returns>An OpenApiAny representation of the object.</returns>
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
            var addMethod = tObj.GetMethod("Add", [typeof(string), tAny]);
            foreach (DictionaryEntry de in dict)
            {
                var key = de.Key?.ToString() ?? string.Empty;
                var child = FromObject(de.Value);
                _ = (addMethod?.Invoke(obj, [key, child]));
            }
            return obj;
        }

        // IEnumerable (non-string) -> OpenApiArray
        if (value is IEnumerable en && value is not string && tArr != null && tAny != null)
        {
            var arr = Activator.CreateInstance(tArr)!;
            var addMethod = tArr.GetMethod("Add", [tAny]);
            foreach (var item in en)
            {
                var child = FromObject(item);
                _ = (addMethod?.Invoke(arr, [child]));
            }
            return arr;
        }

        // Primitives -> OpenApiString (simple, safe default)
        return tStr != null ? Activator.CreateInstance(tStr, value.ToString() ?? string.Empty)! : value.ToString() ?? string.Empty;
    }
}
