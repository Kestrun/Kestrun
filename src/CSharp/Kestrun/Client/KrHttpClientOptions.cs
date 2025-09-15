
using System.Net;

namespace Kestrun.Client;

/// <summary>Extra options to shape HttpClient behavior.</summary>
public sealed class KrHttpClientOptions
{
    /// <summary>Overall request timeout (HttpClient.Timeout). Default 100s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Accept any server certificate (for dev/self-signed).</summary>
    public bool IgnoreCertErrors { get; set; } = false;

    /// <summary>Automatic decompression (gzip/deflate/br). Default: All.</summary>
    public DecompressionMethods Decompression { get; set; } = DecompressionMethods.All;

    /// <summary>Cookies to use (enables UseCookies when non-null).</summary>
    public CookieContainer? Cookies { get; set; } = null;

    /// <summary>Follow redirects automatically. Default: true.</summary>
    public bool AllowAutoRedirect { get; set; } = true;

    /// <summary>Max redirects when AllowAutoRedirect = true. Default: 50.</summary>
    public int MaxAutomaticRedirections { get; set; } = 50;

    /// <summary>Server credentials; ignored if UseDefaultCredentials = true.</summary>
    public ICredentials? Credentials { get; set; } = null;

    /// <summary>Use current user’s credentials for server auth.</summary>
    public bool UseDefaultCredentials { get; set; } = false;

    /// <summary>Proxy to use. Set UseProxy = true to enable.</summary>
    public IWebProxy? Proxy { get; set; } = null;

    /// <summary>Whether to use a proxy handler.</summary>
    public bool UseProxy { get; set; } = false;

    /// <summary>Use current user’s credentials for proxy auth.</summary>
    public bool ProxyUseDefaultCredentials { get; set; } = false;
}
