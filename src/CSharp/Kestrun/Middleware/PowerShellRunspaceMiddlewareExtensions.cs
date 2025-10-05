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
    /// <param name="app">The application builder.</param>
    /// <param name="pool">The runspace pool manager.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UsePowerShellRunspace(
        this IApplicationBuilder app, KestrunRunspacePoolManager pool) => app.UseMiddleware<PowerShellRunspaceMiddleware>(pool);
}
