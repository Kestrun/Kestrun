// src/CSharp/Kestrun.Net/KrHttp.cs
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace Kestrun.Client;

/// <summary>
/// Factory methods to create HttpClient instances for different transport types.
/// </summary>
public static class KrHttpClientFactory
{
    // ---- Internal helper ----------------------------------------------------
    private static SocketsHttpHandler CreateHandler(KrHttpClientOptions opts)
    {
        var handler = CreateBasicHandler(opts);

        // Proxy auth wiring if provided
        if (opts.Proxy is not null)
        {
            if (opts.ProxyUseDefaultCredentials)
            {
                opts.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }
            else if (opts.Proxy.Credentials is null && opts.Credentials is not null)
            {
                // If caller didn't set proxy creds explicitly but provided server creds,
                // reuse them for the proxy (common IWR behavior).
                opts.Proxy.Credentials = opts.Credentials;
            }
        }

        if (opts.IgnoreCertErrors)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (sender, certificate, chain, errors) => true
            };
        }

        return handler;
    }

    /// <summary>
    /// Creates a basic SocketsHttpHandler with common options applied.
    /// </summary>
    /// <param name="opts">The HTTP client options.</param>
    /// <returns>A configured SocketsHttpHandler instance.</returns>
    private static SocketsHttpHandler CreateBasicHandler(KrHttpClientOptions opts)
    {
        var effectiveTimeout = (opts.Timeout <= TimeSpan.Zero) ? TimeSpan.FromSeconds(100) : opts.Timeout;

        return new SocketsHttpHandler
        {
            AutomaticDecompression = opts.Decompression,
            ConnectTimeout = effectiveTimeout,

            // Redirects
            AllowAutoRedirect = opts.AllowAutoRedirect,
            MaxAutomaticRedirections = Math.Max(1, opts.MaxAutomaticRedirections),

            // Cookies/session
            UseCookies = opts.Cookies is not null,
            CookieContainer = opts.Cookies ?? new CookieContainer(),

            // Proxy
            UseProxy = opts.UseProxy && opts.Proxy is not null,
            Proxy = opts.Proxy,

            // Server auth
            Credentials = opts.UseDefaultCredentials
                    ? CredentialCache.DefaultCredentials
                    : opts.Credentials
        };
    }

    /// <summary>
    /// Creates an HttpClient with the specified handler, base address, and timeout.
    /// </summary>
    /// <param name="handler">The HTTP message handler.</param>
    /// <param name="baseAddress">The base address for the HTTP client.</param>
    /// <param name="timeout">The timeout for HTTP requests.</param>
    /// <returns>A configured HttpClient instance.</returns>
    private static HttpClient MakeClient(HttpMessageHandler handler, Uri baseAddress, TimeSpan timeout)
        => new(handler)
        {
            Timeout = (timeout <= TimeSpan.Zero) ? TimeSpan.FromSeconds(100) : timeout,
            BaseAddress = baseAddress
        };

    // ---- Named Pipe ---------------------------------------------------------
    /// <summary>Create an HttpClient that talks HTTP over a Windows Named Pipe.</summary>
    public static HttpClient CreateNamedPipeClient(string pipeName, TimeSpan timeout)
        => CreateNamedPipeClient(pipeName, timeout, ignoreCertErrors: false);

    /// <summary>Create an HttpClient that talks HTTP over a Windows Named Pipe (legacy overload).</summary>
    public static HttpClient CreateNamedPipeClient(string pipeName, TimeSpan timeout, bool ignoreCertErrors)
    {
        var opts = new KrHttpClientOptions { Timeout = timeout, IgnoreCertErrors = ignoreCertErrors };
        return CreateNamedPipeClient(pipeName, opts);
    }

    /// <summary>Create an HttpClient that talks HTTP over a Windows Named Pipe (full options).</summary>
    public static HttpClient CreateNamedPipeClient(string pipeName, KrHttpClientOptions opts)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentNullException(nameof(pipeName));
        }

        var h = CreateHandler(opts);

        // capture pipeName in the lambda (works on .NET 6/7/8)
        h.ConnectCallback = (ctx, ct) =>
        {
            var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            stream.Connect();
            return new ValueTask<Stream>(stream);
        };

        return MakeClient(h, new Uri("http://localhost"), opts.Timeout);
    }

    // ---- Unix Domain Socket -------------------------------------------------
    /// <summary>Create an HttpClient that talks HTTP over a Unix Domain Socket.</summary>
    public static HttpClient CreateUnixSocketClient(string socketPath, TimeSpan timeout)
        => CreateUnixSocketClient(socketPath, timeout, ignoreCertErrors: false);

    /// <summary>Create an HttpClient that talks HTTP over a Unix Domain Socket (legacy overload).</summary>
    public static HttpClient CreateUnixSocketClient(string socketPath, TimeSpan timeout, bool ignoreCertErrors)
    {
        var opts = new KrHttpClientOptions { Timeout = timeout, IgnoreCertErrors = ignoreCertErrors };
        return CreateUnixSocketClient(socketPath, opts);
    }

    /// <summary>Create an HttpClient that talks HTTP over a Unix Domain Socket (full options).</summary>
    public static HttpClient CreateUnixSocketClient(string socketPath, KrHttpClientOptions opts)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentNullException(nameof(socketPath));
        }

        var h = CreateHandler(opts);

        h.ConnectCallback = (ctx, ct) =>
        {
            var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            sock.Connect(new UnixDomainSocketEndPoint(socketPath));
            return new ValueTask<Stream>(new NetworkStream(sock, ownsSocket: true));
        };

        return MakeClient(h, new Uri("http://localhost"), opts.Timeout);
    }

    // ---- TCP (HTTP/HTTPS) ---------------------------------------------------
    /// <summary>Classic TCP HttpClient (normal HTTP/S).</summary>
    public static HttpClient CreateTcpClient(Uri baseUri, TimeSpan timeout)
        => CreateTcpClient(baseUri, timeout, ignoreCertErrors: false);

    /// <summary>Classic TCP HttpClient (normal HTTP/S, legacy overload).</summary>
    public static HttpClient CreateTcpClient(Uri baseUri, TimeSpan timeout, bool ignoreCertErrors)
    {
        var opts = new KrHttpClientOptions { Timeout = timeout, IgnoreCertErrors = ignoreCertErrors };
        return CreateTcpClient(baseUri, opts);
    }

    /// <summary>Classic TCP HttpClient (normal HTTP/S, full options).</summary>
    public static HttpClient CreateTcpClient(Uri baseUri, KrHttpClientOptions opts)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        var h = CreateHandler(opts);
        return MakeClient(h, baseUri, opts.Timeout);
    }
}
