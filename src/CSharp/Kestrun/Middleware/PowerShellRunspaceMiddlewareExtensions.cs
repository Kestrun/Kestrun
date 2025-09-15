using Kestrun.Scripting;
namespace Kestrun.Middleware;

/// <summary>
/// Extension methods for adding PowerShell runspace middleware.
/// </summary>
public static class PowerShellRunspaceMiddlewareExtensions
{
    /// <summary>
    /// Registers <see cref="PowerShellRunspaceMiddleware"/> with the given runspace pool.
    /// </summary>
    public static IApplicationBuilder UsePowerShellRunspace(
        this IApplicationBuilder app, KestrunRunspacePoolManager pool) => app.UseMiddleware<PowerShellRunspaceMiddleware>(pool);
}
