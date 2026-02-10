using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Kestrun.Health;
namespace Kestrun.Hosting.Options;

/// <summary>
/// Simple options class for configuring Kestrel server settings.
/// </summary>
/// <remarks>
/// This class provides a strongly-typed alternative to using a hashtable for Kestrel server options.
/// </remarks>
public class KestrunOptions
{
    /// <summary>
    /// Default media type value for responses.
    /// </summary>
    private const string DefaultResponseMediaTypeValue = "text/plain";
    /// <summary>
    /// Default media type value for API responses.
    /// </summary>
    private const string DefaultApiResponseMediaTypeValue = "application/json";

    /// <summary>
    /// Default upload path value for form parts.
    /// </summary>
    private static readonly string DefaultUploadPathValue = Path.Combine(Path.GetTempPath(), "kestrun-uploads");

    /// <summary>
    /// Gets or sets the Kestrel server options.
    /// </summary>
    public KestrelServerOptions ServerOptions { get; set; }

    /// <summary>Provides access to request limit options. Use a hashtable or a KestrelServerLimits instance.</summary>
    public KestrelServerLimits ServerLimits => ServerOptions.Limits;

    /// <summary>Application name (optional, for diagnostics).</summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of runspaces to use for script execution.
    /// </summary>
    public int? MaxRunspaces { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of runspaces to use for script execution.
    /// Defaults to 1.
    /// </summary>
    public int MinRunspaces { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of runspaces to use for the scheduler service.
    /// Defaults to 8.
    /// </summary>
    public int MaxSchedulerRunspaces { get; set; }

    /// <summary>
    /// Gets or sets the health endpoint configuration.
    /// </summary>
    public HealthEndpointOptions Health { get; set; } = new();

    /// <summary>
    /// List of configured listeners for the Kestrel server.
    /// Each listener can be configured with its own IP address, port, protocols, and other options.
    /// </summary>
    public List<ListenerOptions> Listeners { get; }

    /// <summary>
    /// Gets the HTTPS connection adapter options.
    /// </summary>
    public HttpsConnectionAdapterOptions? HttpsConnectionAdapter { get; set; }

    /// <summary>
    /// Optional path to a Unix domain socket for Kestrel to listen on.
    /// </summary>
    public List<string> ListenUnixSockets { get; }

    /// <summary>
    /// Optional name of a Named Pipe for Kestrel to listen on.
    /// </summary>
    public List<string> NamedPipeNames { get; }

    /// <summary>
    /// Gets or sets the Named Pipe transport options.
    /// </summary>
    public NamedPipeTransportOptions? NamedPipeOptions { get; set; }

    /// <summary>
    /// Gets or sets the default media type to use for responses when no Accept header is provided.
    /// </summary>
    public Dictionary<string, ICollection<string>> DefaultResponseMediaType { get; set; } =
        new Dictionary<string, ICollection<string>> { { "default", new List<string> { DefaultResponseMediaTypeValue } } };


    /// <summary>
    /// Gets or sets the default media type to use for API responses when no Accept header is provided.
    /// </summary>
    public Dictionary<string, ICollection<string>> DefaultApiResponseMediaType { get; set; } =
        new Dictionary<string, ICollection<string>> { { "default", new List<string> { DefaultApiResponseMediaTypeValue } } };

    /// <summary>
    /// Gets or sets the default upload path for form parts.
    /// </summary>
    public string DefaultUploadPath { get; set; } = DefaultUploadPathValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunOptions"/> class with default values.
    /// </summary>
    public KestrunOptions()
    {
        // Set default values if needed
        MinRunspaces = 1; // Default to 1 runspace
        Listeners = [];
        ServerOptions = new KestrelServerOptions();
        MaxSchedulerRunspaces = 8; // Default max scheduler runspaces
        ListenUnixSockets = [];
        NamedPipeNames = [];
        Health = new HealthEndpointOptions();
    }
}
