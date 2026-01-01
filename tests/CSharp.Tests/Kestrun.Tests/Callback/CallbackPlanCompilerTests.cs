using System.Reflection;
using Kestrun.Callback;
using Microsoft.OpenApi;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackPlanCompilerTests
{
    [Fact]
    public void Compile_ExtractsPlans_PathParams_AndBodyMediaType()
    {
        var cb = new OpenApiCallback();

        var op = new OpenApiOperation
        {
            OperationId = "op-123",
            Parameters =
            [
                new OpenApiParameter { Name = "paymentId", In = ParameterLocation.Path },
                new OpenApiParameter { Name = "ignoredQuery", In = ParameterLocation.Query },
            ],
            RequestBody = new OpenApiRequestBody()
        };

        EnsureRequestBodyContentInitialized(op.RequestBody);
        var bodyContent = op.RequestBody.Content!;
        bodyContent["application/xml"] = new OpenApiMediaType();
        bodyContent["application/json"] = new OpenApiMediaType();

        var pathItem = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Post] = op
            }
        };

        SetCallbackPathItem(cb, "{$request.body#/callbackUrls/status}/v1/payments/{paymentId}/status", pathItem);

        var plans = InvokeCompile(cb, "paymentStatus");

        var plan = Assert.Single(plans);

        Assert.Equal("paymentStatus", plan.CallbackId);
        Assert.Equal("{$request.body#/callbackUrls/status}/v1/payments/{paymentId}/status", plan.UrlTemplate);
        Assert.Equal(HttpMethod.Post, plan.Method);
        Assert.Equal("op-123", plan.OperationId);

        var p = Assert.Single(plan.PathParams);
        Assert.Equal("paymentId", p.Name);
        Assert.Equal("path", p.Location);

        var body = plan.Body ?? throw new InvalidOperationException("Expected Body to be non-null.");
        Assert.Equal("application/json", body.MediaType); // prefers json if present
    }

    [Fact]
    public void Compile_UsesFallbackOperationId_WhenMissing()
    {
        var cb = new OpenApiCallback();
        var op = new OpenApiOperation { OperationId = null };

        var pathItem = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = op
            }
        };

        SetCallbackPathItem(cb, "https://example.com/{id}", pathItem);

        var plans = InvokeCompile(cb, "cbid");
        var plan = Assert.Single(plans);

        Assert.Equal("cbid__get", plan.OperationId);
    }

    private static IReadOnlyList<CallbackPlan> InvokeCompile(OpenApiCallback callback, string callbackId)
    {
        var asm = typeof(CallbackPlan).Assembly;
        var t = asm.GetType("Kestrun.Callback.CallbackPlanCompiler", throwOnError: true)!;
        var m = t.GetMethod("Compile", BindingFlags.Static | BindingFlags.NonPublic) ??
                t.GetMethod("Compile", BindingFlags.Static | BindingFlags.Public);

        Assert.NotNull(m);

        var result = m!.Invoke(null, [callback, callbackId]);
        Assert.NotNull(result);

        return Assert.IsAssignableFrom<IReadOnlyList<CallbackPlan>>(result);
    }

    private static void SetCallbackPathItem(OpenApiCallback cb, string expression, OpenApiPathItem pathItem)
    {
        // PathItems is a generic dictionary keyed by a runtime-expression type which varies by OpenAPI package version.
        // Create the key and dictionary via reflection to keep the test resilient.
        var prop = cb.GetType().GetProperty("PathItems")!;
        var dict = prop.GetValue(cb);

        if (dict is null)
        {
            var dictType = prop.PropertyType;

            // If it's an interface (common), instantiate Dictionary<TKey, TValue>
            if (dictType.IsInterface && dictType.IsGenericType)
            {
                var args = dictType.GetGenericArguments();
                var concrete = typeof(Dictionary<,>).MakeGenericType(args[0], args[1]);
                dict = Activator.CreateInstance(concrete)!;
            }
            else
            {
                dict = Activator.CreateInstance(dictType)!;
            }

            prop.SetValue(cb, dict);
        }

        var dictTypeRuntime = dict.GetType();
        var genArgs = dictTypeRuntime.GetGenericArguments();
        var keyType = genArgs[0];

        var key = CreateRuntimeExpressionKey(keyType, expression);

        // Set via indexer: dict[key] = pathItem
        var indexer = dictTypeRuntime.GetProperty("Item");
        Assert.NotNull(indexer);
        indexer!.SetValue(dict, pathItem, [key]);
    }

    private static object CreateRuntimeExpressionKey(Type keyType, string expression)
    {
        // Some OpenAPI versions expose an abstract base (e.g., Microsoft.OpenApi.RuntimeExpression)
        // as the dictionary key. Find a usable concrete subtype if needed.
        var candidateType = keyType;

        if (candidateType.IsAbstract)
        {
            candidateType = candidateType.Assembly
                .GetTypes()
                .FirstOrDefault(t =>
                    keyType.IsAssignableFrom(t)
                    && !t.IsAbstract
                    && t.GetConstructor([typeof(string)]) is not null)
                ?? candidateType;
        }

        var ctor = candidateType.GetConstructor([typeof(string)]);
        if (ctor is not null)
        {
            return ctor.Invoke([expression]);
        }

        // As a last resort, try parameterless ctor + Expression property.
        var key = Activator.CreateInstance(candidateType)!;
        var exprProp = candidateType.GetProperty("Expression", BindingFlags.Public | BindingFlags.Instance);
        exprProp?.SetValue(key, expression);
        return key;
    }

    private static void EnsureRequestBodyContentInitialized(IOpenApiRequestBody requestBody)
    {
        // Some Microsoft.OpenApi versions leave Content null by default.
        // Populate it via reflection so CallbackPlanCompiler can see media types.
        var prop = requestBody.GetType().GetProperty("Content", BindingFlags.Public | BindingFlags.Instance);
        ArgumentNullException.ThrowIfNull(prop, "OpenApiRequestBody.Content property not found.");

        if (prop.GetValue(requestBody) is not null)
        {
            return;
        }

        // Prefer setting via property setter (if present)
        if (prop.CanWrite)
        {
            prop.SetValue(requestBody, CreateDictionaryInstance(prop.PropertyType));
            return;
        }

        // Fall back to backing field (auto-property)
        var backing = requestBody.GetType().GetField("<Content>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (backing is not null)
        {
            backing.SetValue(requestBody, CreateDictionaryInstance(prop.PropertyType));
            return;
        }

        // Last resort: try common private field names
        foreach (var name in new[] { "_content", "content" })
        {
            var f = requestBody.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f is not null)
            {
                f.SetValue(requestBody, CreateDictionaryInstance(prop.PropertyType));
                return;
            }
        }

        throw new InvalidOperationException("Unable to initialize OpenApiRequestBody.Content.");
    }

    private static object CreateDictionaryInstance(Type dictionaryType)
    {
        if (dictionaryType.IsInterface && dictionaryType.IsGenericType)
        {
            var args = dictionaryType.GetGenericArguments();
            var concrete = typeof(Dictionary<,>).MakeGenericType(args[0], args[1]);
            return Activator.CreateInstance(concrete)!;
        }

        return Activator.CreateInstance(dictionaryType)!;
    }
}
