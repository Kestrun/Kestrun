using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
namespace Kestrun.Utilities;
/// <summary>
/// Provides extension methods for copying rate limiter options and policies.
/// </summary>
public static class RateLimiterOptionsExtensions
{
    /// <summary>
    /// Copies all rate limiter options and policies from the source to the target <see cref="RateLimiterOptions"/>.
    /// </summary>
    /// <param name="target">The target <see cref="RateLimiterOptions"/> to copy to.</param>
    /// <param name="source">The source <see cref="RateLimiterOptions"/> to copy from.</param>
    public static void CopyFrom(this RateLimiterOptions target, RateLimiterOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // ───── scalar props ─────
        target.GlobalLimiter = source.GlobalLimiter;
        target.OnRejected = source.OnRejected;
        target.RejectionStatusCode = source.RejectionStatusCode;

        // ───── activated policies ─────
        try
        {
            var policyMapField = typeof(RateLimiterOptions).GetField("PolicyMap",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (policyMapField != null)
            {
                var policyMap = (IDictionary<string, object>?)policyMapField.GetValue(source);
                if (policyMap != null)
                {
                    // Find the AddPolicy method that takes an IRateLimiterPolicy<HttpContext>
                    var addPolicyMethod = GetAddPolicyMethod(true);
                    foreach (var kvp in policyMap)
                    {
                        _ = (addPolicyMethod?.Invoke(target, [kvp.Key, kvp.Value]));
                    }
                }
            }
        }
        catch
        {
            // Silently ignore if PolicyMap field doesn't exist in this version
        }

        // ───── factories awaiting DI (un-activated) ─────
        try
        {
            var factoryMapField = typeof(RateLimiterOptions).GetField("UnactivatedPolicyMap",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (factoryMapField != null)
            {
                var factoryMap = (IDictionary<string, object>?)factoryMapField.GetValue(source);
                if (factoryMap != null)
                {
                    // Find the AddPolicy method that takes a Func<IServiceProvider, IRateLimiterPolicy<HttpContext>>
                    var addPolicyMethod = GetAddPolicyMethod(false);
                    foreach (var kvp in factoryMap)
                    {
                        _ = (addPolicyMethod?.Invoke(target, [kvp.Key, kvp.Value]));
                    }
                }
            }
        }
        catch
        {
            // Silently ignore if UnactivatedPolicyMap field doesn't exist in this version
        }
    }

    private static MethodInfo? GetAddPolicyMethod(bool forDirectPolicy)
    {
        var methods = typeof(RateLimiterOptions).GetMethods();
        foreach (var method in methods)
        {
            if (method.Name != "AddPolicy")
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 2)
            {
                continue;
            }

            if (parameters[0].ParameterType != typeof(string))
            {
                continue;
            }

            var secondParamType = parameters[1].ParameterType;

            if (forDirectPolicy)
            {
                // Looking for AddPolicy(string, IRateLimiterPolicy<HttpContext>)
                // The parameter should be an interface that is IRateLimiterPolicy<T>
                if (secondParamType.IsGenericType &&
                    secondParamType.GetGenericTypeDefinition().Name.Contains("IRateLimiterPolicy"))
                {
                    return method;
                }
            }
            else
            {
                // Looking for AddPolicy(string, Func<IServiceProvider, IRateLimiterPolicy<HttpContext>>)
                // The parameter should be a Func delegate
                if (secondParamType.IsGenericType &&
                    secondParamType.GetGenericTypeDefinition() == typeof(Func<,>))
                {
                    return method;
                }
            }
        }

        return null;
    }
}
