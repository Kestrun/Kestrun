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
    private static SocketsHttpHandler CreateHandler(
        TimeSpan timeout,
        bool ignoreCertErrors,
        DecompressionMethods decompression = DecompressionMethods.All)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = decompression,        // gzip/deflate/br
            ConnectTimeout = (timeout <= TimeSpan.Zero) ? TimeSpan.FromSeconds(100) : timeout
        };

        if (ignoreCertErrors)
        {
            // Accept any server certificate (Invoke-WebRequest -SkipCertificateCheck equivalent)
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            };
        }

        return handler;
    }

    // ---- Named Pipe ---------------------------------------------------------
    /// <summary>Create an HttpClient that talks HTTP over a Windows Named Pipe.</summary>
    public static HttpClient CreateNamedPipeClient(string pipeName, TimeSpan timeout)
        => CreateNamedPipeClient(pipeName, timeout, ignoreCertErrors: false);

    /// <summary>Create an HttpClient that talks HTTP over a Windows Named Pipe.</summary>
    public static HttpClient CreateNamedPipeClient(string pipeName, TimeSpan timeout, bool ignoreCertErrors)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentNullException(nameof(pipeName));
        }

        var handler = CreateHandler(timeout, ignoreCertErrors);
        handler.ConnectCallback = (ctx, ct) =>
        {
            var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            stream.Connect();
            return new ValueTask<Stream>(stream);
        };

        return new HttpClient(handler)
        {
            Timeout = (timeout <= TimeSpan.Zero) ? TimeSpan.FromSeconds(100) : timeout,
            BaseAddress = new Uri("http://localhost") // dummy; ignored by ConnectCallback
        };
    }

    // ---- Unix Domain Socket -------------------------------------------------
    /// <summary>Create an HttpClient that talks HTTP over a Unix Domain Socket.</summary>
    public static HttpClient CreateUnixSocketClient(string socketPath, TimeSpan timeout)
        => CreateUnixSocketClient(socketPath, timeout, ignoreCertErrors: false);

    /// <summary>Create an HttpClient that talks HTTP over a Unix Domain Socket.</summary>
    public static HttpClient CreateUnixSocketClient(string socketPath, TimeSpan timeout, bool ignoreCertErrors)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentNullException(nameof(socketPath));
        }

        var handler = CreateHandler(timeout, ignoreCertErrors);
        handler.ConnectCallback = (ctx, ct) =>
        {
            var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            sock.Connect(new UnixDomainSocketEndPoint(socketPath));
            return new ValueTask<Stream>(new NetworkStream(sock, ownsSocket: true));
        };

        return new HttpClient(handler)
        {
            Timeout = (timeout <= TimeSpan.Zero) ? TimeSpan.FromSeconds(100) : timeout,
            BaseAddress = new Uri("http://localhost") // dummy; ignored by ConnectCallback
        };
    }

    // ---- TCP (HTTP/HTTPS) ---------------------------------------------------
    /// <summary>Classic TCP HttpClient (normal HTTP/S).</summary>
    public static HttpClient CreateTcpClient(Uri baseUri, TimeSpan timeout)
        => CreateTcpClient(baseUri, timeout, ignoreCertErrors: false);

    /// <summary>Classic TCP HttpClient (normal HTTP/S).</summary>
    public static HttpClient CreateTcpClient(Uri baseUri, TimeSpan timeout, bool ignoreCertErrors)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        var handler = CreateHandler(timeout, ignoreCertErrors);

        return new HttpClient(handler)
        {
            Timeout = (timeout <= TimeSpan.Zero) ? TimeSpan.FromSeconds(100) : timeout,
            BaseAddress = baseUri
        };
    }
}
