using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kestrun.Razor;

/// <summary>
/// Base PageModel that exposes whatever the sibling PowerShell script placed
/// in <c>HttpContext.Items["PageModel"]</c>.
/// </summary>
public class PwshKestrunModel : PageModel
{
    /// <summary>
    /// Gets the dynamic data object placed in <c>HttpContext.Items["PageModel"]</c> by the sibling PowerShell script.
    /// </summary>
    public dynamic? Data => HttpContext.Items["PageModel"];

    // convenience helpers ----------------------------------------------

    /// <summary>
    /// Gets the value of a query string parameter by key from the current HTTP request.
    /// </summary>
    /// <param name="key">The query string key.</param>
    /// <returns>The value associated with the specified key, or null if not found.</returns>
    public string? Query(string key) => HttpContext.Request.Query[key];

    /// <summary>
    /// Gets the application configuration from the current HTTP request's service provider.
    /// </summary>
    public IConfiguration Config =>
        HttpContext.RequestServices.GetRequiredService<IConfiguration>();
}
