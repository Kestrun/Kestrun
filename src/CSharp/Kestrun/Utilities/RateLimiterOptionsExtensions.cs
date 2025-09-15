using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
namespace Kestrun.Utilities;
/// <summary>
/// Provides extension methods for copying rate limiter options and policies.
/// </summary>
public static class RateLimiterOptionsExtensions
{
    private static readonly MethodInfo AddPolicyMethod =
        typeof(RateLimiterOptions)
            .GetMethods()
            .Single(m => m.Name == "AddPolicy" && m.GetParameters().Length == 2);

    private static readonly FieldInfo PolicyMapField =
        typeof(RateLimiterOptions).GetField("PolicyMap",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo UnactivatedPolicyMapField =
        typeof(RateLimiterOptions).GetField("UnactivatedPolicyMap",
            BindingFlags.Instance | BindingFlags.NonPublic)!;


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
        var policyMap = (IDictionary<string, object>)PolicyMapField.GetValue(source)!;
        foreach (var kvp in policyMap)
        {
            // AddPolicy(string, IRateLimiterPolicy<HttpContext>)
            _ = AddPolicyMethod.Invoke(target, [kvp.Key, kvp.Value]);
        }

        // ───── factories awaiting DI (un-activated) ─────
        var factoryMap = (IDictionary<string, object>)UnactivatedPolicyMapField.GetValue(source)!;
        foreach (var kvp in factoryMap)
        {
            // AddPolicy(string, Func<IServiceProvider, IRateLimiterPolicy<HttpContext>>)
            _ = AddPolicyMethod.Invoke(target, [kvp.Key, kvp.Value]);
        }
    }
}
