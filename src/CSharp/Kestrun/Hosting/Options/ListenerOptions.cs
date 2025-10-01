using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Kestrun.Hosting.Options;

/// <summary>
/// Configuration for an individual Kestrel listener.
/// </summary>
public class ListenerOptions
{
    /// <summary>The IP address to bind to.</summary>
    public IPAddress IPAddress { get; set; }

    /// <summary>The port to listen on.</summary>
    public int Port { get; set; }

    /// <summary>Whether HTTPS should be used.</summary>
    public bool UseHttps { get; set; }

    /// <summary>HTTP protocols supported by the listener.</summary>
    public HttpProtocols Protocols { get; set; }

    /// <summary>Enable verbose connection logging.</summary>
    public bool UseConnectionLogging { get; set; }

    /// <summary>Optional TLS certificate.</summary>
    public X509Certificate2? X509Certificate { get; internal set; }

    /// <summary>
    /// Gets or sets a value that controls whether the "Alt-Svc" header is included with response headers.
    /// The "Alt-Svc" header is used by clients to upgrade HTTP/1.1 and HTTP/2 connections to HTTP/3.
    /// <para>
    /// The "Alt-Svc" header is automatically included with a response if <see cref="Protocols"/> has either
    /// HTTP/1.1 or HTTP/2 enabled, and HTTP/3 is enabled. If an "Alt-Svc" header value has already been set
    /// by the app then it isn't changed.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Defaults to false.
    /// </remarks>
    public bool DisableAltSvcHeader { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListenerOptions"/> class with default values.
    /// </summary>
    public ListenerOptions()
    {
        IPAddress = IPAddress.Any;
        UseHttps = false;
        Protocols = HttpProtocols.Http1;
        UseConnectionLogging = false;
    }
    /// <summary>
    /// Returns a string representation of the listener in the format "http(s)://{IPAddress}:{Port}".
    /// </summary>
    /// <returns>A string representation of the listener.</returns>
    public override string ToString() => $"{(UseHttps ? "https" : "http")}://{IPAddress}:{Port}";
}
