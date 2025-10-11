using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.Utilities;
using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.SignalR;
using Kestrun.Scheduling;
using Kestrun.SharedState;
using Kestrun.Middleware;
using Kestrun.Scripting;
using Kestrun.Hosting.Options;
using System.Runtime.InteropServices;
using Microsoft.PowerShell;
using System.Net.Sockets;
using Microsoft.Net.Http.Headers;
using Kestrun.Authentication;
using Kestrun.Health;

namespace Kestrun.Hosting;

/// <summary>
/// Provides hosting and configuration for the Kestrun application, including service registration, middleware setup, and runspace pool management.
/// </summary>
public class KestrunHost : IDisposable
{
    #region Fields
    internal WebApplicationBuilder Builder { get; }

    private WebApplication? _app;

    internal WebApplication App => _app ?? throw new InvalidOperationException("WebApplication is not built yet. Call Build() first.");

    /// <summary>
    /// Gets the application name for the Kestrun host.
    /// </summary>
    public string ApplicationName => Options.ApplicationName ?? "KestrunApp";

    /// <summary>
    /// Gets the configuration options for the Kestrun host.
    /// </summary>
    public KestrunOptions Options { get; private set; } = new();

    /// <summary>
    /// List of PowerShell module paths to be loaded.
    /// </summary>
    private readonly List<string> _modulePaths = [];

    /// <summary>
    /// Indicates whether the Kestrun host is stopping.
    /// </summary>
    private int _stopping; // 0 = running, 1 = stopping

    /// <summary>
    /// Indicates whether the Kestrun host configuration has been applied.
    /// </summary>
    public bool IsConfigured { get; private set; }

    private KestrunRunspacePoolManager? _runspacePool;

    internal KestrunRunspacePoolManager RunspacePool => _runspacePool ?? throw new InvalidOperationException("Runspace pool is not initialized. Call EnableConfiguration first.");

    // ── ✦ QUEUE #1 : SERVICE REGISTRATION ✦ ─────────────────────────────
    private readonly List<Action<IServiceCollection>> _serviceQueue = [];

    // ── ✦ QUEUE #2 : MIDDLEWARE STAGES ✦ ────────────────────────────────
    private readonly List<Action<IApplicationBuilder>> _middlewareQueue = [];

    internal List<Action<KestrunHost>> FeatureQueue { get; } = [];

    internal List<IProbe> HealthProbes { get; } = [];
#if NET9_0_OR_GREATER
    private readonly Lock _healthProbeLock = new();
#else
    private readonly object _healthProbeLock = new();
#endif

    internal readonly Dictionary<(string Pattern, string Method), MapRouteOptions> _registeredRoutes =
    new(new RouteKeyComparer());

    //internal readonly Dictionary<(string Scheme, string Type), AuthenticationSchemeOptions> _registeredAuthentications =
    //  new(new AuthKeyComparer());

    /// <summary>
    /// Gets the root directory path for the Kestrun application.
    /// </summary>
    public string? KestrunRoot { get; private set; }

    /// <summary>
    /// Gets the Serilog logger instance used by the Kestrun host.
    /// </summary>
    public Serilog.ILogger Logger { get; private set; }

    /// <summary>
    /// Gets the scheduler service used for managing scheduled tasks in the Kestrun host.
    /// </summary>
    public SchedulerService Scheduler { get; internal set; } = null!; // Initialized in ConfigureServices

    /// <summary>
    /// Gets the stack used for managing route groups in the Kestrun host.
    /// </summary>
    public System.Collections.Stack RouteGroupStack { get; } = new();


    /// <summary>
    /// Gets the registered routes in the Kestrun host.
    /// </summary>
    public Dictionary<(string, string), MapRouteOptions> RegisteredRoutes => _registeredRoutes;

    /// <summary>
    /// Gets the registered authentication schemes in the Kestrun host.
    /// </summary>
    public AuthenticationRegistry RegisteredAuthentications { get; } = new();

    /// <summary>
    /// Gets or sets the default cache control settings for HTTP responses.
    /// </summary>
    public CacheControlHeaderValue? DefaultCacheControl { get; internal set; }

    /// <summary>
    /// Gets the shared state manager for managing shared data across requests and sessions.
    /// </summary>
    public bool PowershellMiddlewareEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is the default Kestrun host.
    /// </summary>
    public bool DefaultHost { get; internal set; }

    /// <summary>
    /// Gets or sets the status code options for configuring status code pages.
    /// </summary>
    public StatusCodeOptions? StatusCodeOptions { get; set; }

    /// <summary>
    /// Gets or sets the exception options for configuring exception handling.
    /// </summary>
    public ExceptionOptions? ExceptionOptions { get; set; }

    #endregion

    // Accepts optional module paths (from PowerShell)
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunHost"/> class with the specified application name, root directory, and optional module paths.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <param name="kestrunRoot">The root directory for the Kestrun application.</param>
    /// <param name="modulePathsObj">An array of module paths to be loaded.</param>
    public KestrunHost(string? appName, string? kestrunRoot = null, string[]? modulePathsObj = null) :
            this(appName, Log.Logger, kestrunRoot, modulePathsObj)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunHost"/> class with the specified application name, logger, root directory, and optional module paths.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <param name="logger">The Serilog logger instance to use.</param>
    /// <param name="kestrunRoot">The root directory for the Kestrun application.</param>
    /// <param name="modulePathsObj">An array of module paths to be loaded.</param>
    public KestrunHost(string? appName, Serilog.ILogger logger, string? kestrunRoot = null, string[]? modulePathsObj = null)
    {
        // ① Logger
        Logger = logger ?? Log.Logger;
        LogConstructorArgs(appName, logger == null, kestrunRoot, modulePathsObj?.Length ?? 0);

        // ② Working directory/root
        SetWorkingDirectoryIfNeeded(kestrunRoot);

        // ③ Ensure Kestrun module path is available
        AddKestrunModulePathIfMissing(modulePathsObj);

        // ④ Builder + logging
        Builder = WebApplication.CreateBuilder();
        _ = Builder.Host.UseSerilog();

        // ④.1 Make this KestrunHost available via DI so framework-created components (e.g., auth handlers)
        // can resolve it. We register the current instance as a singleton.
        Builder.Services.AddSingleton(this);

        // ⑤ Options
        InitializeOptions(appName);

        // ⑥ Add user-provided module paths
        AddUserModulePaths(modulePathsObj);

        Logger.Information("Current working directory: {CurrentDirectory}", Directory.GetCurrentDirectory());
    }
    #endregion

    #region Helpers


    /// <summary>
    /// Logs constructor arguments at Debug level for diagnostics.
    /// </summary>
    private void LogConstructorArgs(string? appName, bool defaultLogger, string? kestrunRoot, int modulePathsLength)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug(
                "KestrunHost ctor: AppName={AppName}, DefaultLogger={DefaultLogger}, KestrunRoot={KestrunRoot}, ModulePathsLength={Len}",
                appName, defaultLogger, kestrunRoot, modulePathsLength);
        }
    }

    /// <summary>
    /// Sets the current working directory to the provided Kestrun root if needed and stores it.
    /// </summary>
    /// <param name="kestrunRoot">The Kestrun root directory path.</param>
    private void SetWorkingDirectoryIfNeeded(string? kestrunRoot)
    {
        if (string.IsNullOrWhiteSpace(kestrunRoot))
        {
            return;
        }

        if (!string.Equals(Directory.GetCurrentDirectory(), kestrunRoot, StringComparison.Ordinal))
        {
            Directory.SetCurrentDirectory(kestrunRoot);
            Logger.Information("Changed current directory to Kestrun root: {KestrunRoot}", kestrunRoot);
        }
        else
        {
            Logger.Verbose("Current directory is already set to Kestrun root: {KestrunRoot}", kestrunRoot);
        }

        KestrunRoot = kestrunRoot;
    }

    /// <summary>
    /// Ensures the core Kestrun module path is present; if missing, locates and adds it.
    /// </summary>
    /// <param name="modulePathsObj">The array of module paths to check.</param>
    private void AddKestrunModulePathIfMissing(string[]? modulePathsObj)
    {
        var needsLocate = modulePathsObj is null ||
                          (modulePathsObj?.Any(p => p.Contains("Kestrun.psm1", StringComparison.Ordinal)) == false);
        if (!needsLocate)
        {
            return;
        }

        var kestrunModulePath = PowerShellModuleLocator.LocateKestrunModule();
        if (string.IsNullOrWhiteSpace(kestrunModulePath))
        {
            Logger.Fatal("Kestrun module not found. Ensure the Kestrun module is installed.");
            throw new FileNotFoundException("Kestrun module not found.");
        }

        Logger.Information("Found Kestrun module at: {KestrunModulePath}", kestrunModulePath);
        Logger.Verbose("Adding Kestrun module path: {KestrunModulePath}", kestrunModulePath);
        _modulePaths.Add(kestrunModulePath);
    }

    /// <summary>
    /// Initializes Kestrun options and sets the application name when provided.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    private void InitializeOptions(string? appName)
    {
        if (string.IsNullOrEmpty(appName))
        {
            Logger.Information("No application name provided, using default.");
            Options = new KestrunOptions();
        }
        else
        {
            Logger.Information("Setting application name: {AppName}", appName);
            Options = new KestrunOptions { ApplicationName = appName };
        }
    }

    /// <summary>
    /// Adds user-provided module paths if they exist, logging warnings for invalid entries.
    /// </summary>
    /// <param name="modulePathsObj">The array of module paths to check.</param>
    private void AddUserModulePaths(string[]? modulePathsObj)
    {
        if (modulePathsObj is IEnumerable<object> modulePathsEnum)
        {
            foreach (var modPathObj in modulePathsEnum)
            {
                if (modPathObj is string modPath && !string.IsNullOrWhiteSpace(modPath))
                {
                    if (File.Exists(modPath))
                    {
                        Logger.Information("[KestrunHost] Adding module path: {ModPath}", modPath);
                        _modulePaths.Add(modPath);
                    }
                    else
                    {
                        Logger.Warning("[KestrunHost] Module path does not exist: {ModPath}", modPath);
                    }
                }
                else
                {
                    Logger.Warning("[KestrunHost] Invalid module path provided.");
                }
            }
        }
    }
    #endregion


    #region Health Probes

    /// <summary>
    /// Registers the provided <see cref="IProbe"/> instance with the host.
    /// </summary>
    /// <param name="probe">The probe to register.</param>
    /// <returns>The current <see cref="KestrunHost"/> instance.</returns>
    public KestrunHost AddProbe(IProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        RegisterProbeInternal(probe);
        return this;
    }

    /// <summary>
    /// Registers a delegate-based probe.
    /// </summary>
    /// <param name="name">Probe name.</param>
    /// <param name="tags">Optional tag list used for filtering.</param>
    /// <param name="callback">Delegate executed when the probe runs.</param>
    /// <returns>The current <see cref="KestrunHost"/> instance.</returns>
    public KestrunHost AddProbe(string name, string[]? tags, Func<CancellationToken, Task<ProbeResult>> callback)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(callback);

        var probe = new DelegateProbe(name, tags, callback);
        RegisterProbeInternal(probe);
        return this;
    }

    /// <summary>
    /// Registers a script-based probe written in any supported language.
    /// </summary>
    /// <param name="name">Probe name.</param>
    /// <param name="tags">Optional tag list used for filtering.</param>
    /// <param name="code">Script contents.</param>
    /// <param name="language">Optional language override. When null, <see cref="KestrunOptions.Health"/> defaults are used.</param>
    /// <param name="arguments">Optional argument dictionary exposed to the script.</param>
    /// <param name="extraImports">Optional language-specific imports.</param>
    /// <param name="extraRefs">Optional additional assembly references.</param>
    /// <returns>The current <see cref="KestrunHost"/> instance.</returns>
    public KestrunHost AddProbe(
        string name,
        string[]? tags,
        string code,
        ScriptLanguage? language = null,
        IReadOnlyDictionary<string, object?>? arguments = null,
        string[]? extraImports = null,
        Assembly[]? extraRefs = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var effectiveLanguage = language ?? Options.Health.DefaultScriptLanguage;
        var logger = Logger.ForContext("HealthProbe", name);
        var probe = ScriptProbeFactory.Create(
            name,
            tags,
            effectiveLanguage,
            code,
            logger,
            effectiveLanguage == ScriptLanguage.PowerShell ? () => RunspacePool : null,
            arguments,
            extraImports,
            extraRefs);

        RegisterProbeInternal(probe);
        return this;
    }

    /// <summary>
    /// Returns a snapshot of the currently registered probes.
    /// </summary>
    internal IReadOnlyList<IProbe> GetHealthProbesSnapshot()
    {
        lock (_healthProbeLock)
        {
            return [.. HealthProbes];
        }
    }

    private void RegisterProbeInternal(IProbe probe)
    {
        lock (_healthProbeLock)
        {
            var index = HealthProbes.FindIndex(p => string.Equals(p.Name, probe.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                HealthProbes[index] = probe;
                Logger.Information("Replaced health probe {ProbeName}.", probe.Name);
            }
            else
            {
                HealthProbes.Add(probe);
                Logger.Information("Registered health probe {ProbeName}.", probe.Name);
            }
        }
    }

    #endregion


    #region ListenerOptions

    /// <summary>
    /// Configures a listener for the Kestrun host with the specified port, optional IP address, certificate, protocols, and connection logging.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="ipAddress">The IP address to bind to. If null, binds to any address.</param>
    /// <param name="x509Certificate">The X509 certificate for HTTPS. If null, HTTPS is not used.</param>
    /// <param name="protocols">The HTTP protocols to use.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost ConfigureListener(
    int port,
    IPAddress? ipAddress = null,
    X509Certificate2? x509Certificate = null,
    HttpProtocols protocols = HttpProtocols.Http1,
    bool useConnectionLogging = false)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("ConfigureListener port={Port}, ipAddress={IPAddress}, protocols={Protocols}, useConnectionLogging={UseConnectionLogging}, certificate supplied={HasCert}", port, ipAddress, protocols, useConnectionLogging, x509Certificate != null);
        }

        if (protocols == HttpProtocols.Http1AndHttp2AndHttp3 && !CcUtilities.PreviewFeaturesEnabled())
        {
            Logger.Warning("Http3 is not supported in this version of Kestrun. Using Http1 and Http2 only.");
            protocols = HttpProtocols.Http1AndHttp2;
        }

        Options.Listeners.Add(new ListenerOptions
        {
            IPAddress = ipAddress ?? IPAddress.Any,
            Port = port,
            UseHttps = x509Certificate != null,
            X509Certificate = x509Certificate,
            Protocols = protocols,
            UseConnectionLogging = useConnectionLogging
        });
        return this;
    }

    /// <summary>
    /// Configures a listener for the Kestrun host with the specified port, optional IP address, and connection logging.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="ipAddress">The IP address to bind to. If null, binds to any address.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    public void ConfigureListener(
    int port,
    IPAddress? ipAddress = null,
    bool useConnectionLogging = false) => _ = ConfigureListener(port: port, ipAddress: ipAddress, x509Certificate: null, protocols: HttpProtocols.Http1, useConnectionLogging: useConnectionLogging);

    /// <summary>
    /// Configures a listener for the Kestrun host with the specified port and connection logging option.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    public void ConfigureListener(
    int port,
    bool useConnectionLogging = false) => _ = ConfigureListener(port: port, ipAddress: null, x509Certificate: null, protocols: HttpProtocols.Http1, useConnectionLogging: useConnectionLogging);


    /// <summary>
    /// Configures listeners for the Kestrun host by resolving the specified host name to IP addresses and binding to each address.
    /// </summary>
    /// <param name="hostName">The host name to resolve and bind to.</param>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="x509Certificate">The X509 certificate for HTTPS. If null, HTTPS is not used.</param>
    /// <param name="protocols">The HTTP protocols to use.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    /// <param name="families">Optional array of address families to filter resolved addresses (e.g., IPv4-only).</param>
    /// <returns>The current KestrunHost instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the host name is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no valid IP addresses are resolved.</exception>
    public KestrunHost ConfigureListener(
    string hostName,
    int port,
    X509Certificate2? x509Certificate = null,
    HttpProtocols protocols = HttpProtocols.Http1,
    bool useConnectionLogging = false,
    AddressFamily[]? families = null) // e.g. new[] { AddressFamily.InterNetwork } for IPv4-only
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new ArgumentException("Host name must be provided.", nameof(hostName));
        }

        // If caller passed an IP literal, just bind once.
        if (IPAddress.TryParse(hostName, out var parsedIp))
        {
            _ = ConfigureListener(port, parsedIp, x509Certificate, protocols, useConnectionLogging);
            return this;
        }

        // Resolve and bind to ALL matching addresses (IPv4/IPv6)
        var addrs = Dns.GetHostAddresses(hostName)
                       .Where(a => families is null || families.Length == 0 || families.Contains(a.AddressFamily))
                       .Where(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                       .ToArray();

        if (addrs.Length == 0)
        {
            throw new InvalidOperationException($"No IPv4/IPv6 addresses resolved for host '{hostName}'.");
        }

        foreach (var addr in addrs)
        {
            _ = ConfigureListener(port, addr, x509Certificate, protocols, useConnectionLogging);
        }

        return this;
    }

    /// <summary>
    /// Configures listeners for the Kestrun host based on the provided absolute URI, resolving the host to IP addresses and binding to each address.
    /// </summary>
    /// <param name="uri">The absolute URI to configure the listener for.</param>
    /// <param name="x509Certificate">The X509 certificate for HTTPS. If null, HTTPS is not used.</param>
    /// <param name="protocols">The HTTP protocols to use.</param>
    /// <param name="useConnectionLogging">Specifies whether to enable connection logging.</param>
    /// <param name="families">Optional array of address families to filter resolved addresses (e.g., IPv4-only).</param>
    /// <returns>The current KestrunHost instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided URI is not absolute.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no valid IP addresses are resolved.</exception>
    public KestrunHost ConfigureListener(
    Uri uri,
    X509Certificate2? x509Certificate = null,
    HttpProtocols? protocols = null,
    bool useConnectionLogging = false,
    AddressFamily[]? families = null)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("URL must be absolute.", nameof(uri));
        }

        var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var port = uri.IsDefaultPort ? (isHttps ? 443 : 80) : uri.Port;

        // Default: HTTPS → H1+H2, HTTP → H1
        var chosenProtocols = protocols ?? (isHttps ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http1);

        // Delegate to hostname overload (which will resolve or handle IP literal)
        return ConfigureListener(
            hostName: uri.Host,
            port: port,
            x509Certificate: x509Certificate,
            protocols: chosenProtocols,
            useConnectionLogging: useConnectionLogging,
            families: families
        );
    }

    #endregion

    #region Configuration


    /// <summary>
    /// Validates if configuration can be applied and returns early if already configured.
    /// </summary>
    /// <returns>True if configuration should proceed, false if it should be skipped.</returns>
    internal bool ValidateConfiguration()
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("EnableConfiguration(options) called");
        }

        if (IsConfigured)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Configuration already applied, skipping");
            }
            return false; // Already configured
        }

        return true;
    }

    /// <summary>
    /// Creates and initializes the runspace pool for PowerShell execution.
    /// </summary>
    /// <param name="userVariables">User-defined variables to inject into the runspace pool.</param>
    /// <param name="userFunctions">User-defined functions to inject into the runspace pool.</param>
    /// <exception cref="InvalidOperationException">Thrown when runspace pool creation fails.</exception>
    internal void InitializeRunspacePool(Dictionary<string, object>? userVariables, Dictionary<string, string>? userFunctions)
    {
        _runspacePool = CreateRunspacePool(Options.MaxRunspaces, userVariables, userFunctions);
        if (_runspacePool == null)
        {
            throw new InvalidOperationException("Failed to create runspace pool.");
        }

        if (Logger.IsEnabled(LogEventLevel.Verbose))
        {
            Logger.Verbose("Runspace pool created with max runspaces: {MaxRunspaces}", Options.MaxRunspaces);
        }
    }

    /// <summary>
    /// Configures the Kestrel web server with basic options.
    /// </summary>
    internal void ConfigureKestrelBase()
    {
        _ = Builder.WebHost.UseKestrel(opts =>
        {
            opts.CopyFromTemplate(Options.ServerOptions);
        });
    }

    /// <summary>
    /// Configures named pipe listeners if supported on the current platform.
    /// </summary>
    internal void ConfigureNamedPipes()
    {
        if (Options.NamedPipeOptions is not null)
        {
            if (OperatingSystem.IsWindows())
            {
                _ = Builder.WebHost.UseNamedPipes(opts =>
                {
                    opts.ListenerQueueCount = Options.NamedPipeOptions.ListenerQueueCount;
                    opts.MaxReadBufferSize = Options.NamedPipeOptions.MaxReadBufferSize;
                    opts.MaxWriteBufferSize = Options.NamedPipeOptions.MaxWriteBufferSize;
                    opts.CurrentUserOnly = Options.NamedPipeOptions.CurrentUserOnly;
                    opts.PipeSecurity = Options.NamedPipeOptions.PipeSecurity;
                });
            }
            else
            {
                Logger.Verbose("Named pipe listeners configuration is supported only on Windows; skipping UseNamedPipes configuration.");
            }
        }
    }

    /// <summary>
    /// Configures HTTPS connection adapter defaults.
    /// </summary>
    /// <param name="serverOptions">The Kestrel server options to configure.</param>
    internal void ConfigureHttpsAdapter(KestrelServerOptions serverOptions)
    {
        if (Options.HttpsConnectionAdapter is not null)
        {
            Logger.Verbose("Applying HTTPS connection adapter options from KestrunOptions.");

            // Apply HTTPS defaults if needed
            serverOptions.ConfigureHttpsDefaults(httpsOptions =>
            {
                httpsOptions.SslProtocols = Options.HttpsConnectionAdapter.SslProtocols;
                httpsOptions.ClientCertificateMode = Options.HttpsConnectionAdapter.ClientCertificateMode;
                httpsOptions.ClientCertificateValidation = Options.HttpsConnectionAdapter.ClientCertificateValidation;
                httpsOptions.CheckCertificateRevocation = Options.HttpsConnectionAdapter.CheckCertificateRevocation;
                httpsOptions.ServerCertificate = Options.HttpsConnectionAdapter.ServerCertificate;
                httpsOptions.ServerCertificateChain = Options.HttpsConnectionAdapter.ServerCertificateChain;
                httpsOptions.ServerCertificateSelector = Options.HttpsConnectionAdapter.ServerCertificateSelector;
                httpsOptions.HandshakeTimeout = Options.HttpsConnectionAdapter.HandshakeTimeout;
                httpsOptions.OnAuthenticate = Options.HttpsConnectionAdapter.OnAuthenticate;
            });
        }
    }

    /// <summary>
    /// Binds all configured listeners (Unix sockets, named pipes, TCP) to the server.
    /// </summary>
    /// <param name="serverOptions">The Kestrel server options to configure.</param>
    internal void BindListeners(KestrelServerOptions serverOptions)
    {
        // Unix domain socket listeners
        foreach (var unixSocket in Options.ListenUnixSockets)
        {
            if (!string.IsNullOrWhiteSpace(unixSocket))
            {
                Logger.Verbose("Binding Unix socket: {Sock}", unixSocket);
                serverOptions.ListenUnixSocket(unixSocket);
                // NOTE: control access via directory perms/umask; UDS file perms are inherited from process umask
                // Prefer placing the socket under a group-owned dir (e.g., /var/run/kestrun) with 0770.
            }
        }

        // Named pipe listeners
        foreach (var namedPipeName in Options.NamedPipeNames)
        {
            if (!string.IsNullOrWhiteSpace(namedPipeName))
            {
                Logger.Verbose("Binding Named Pipe: {Pipe}", namedPipeName);
                serverOptions.ListenNamedPipe(namedPipeName);
            }
        }

        // TCP listeners
        foreach (var opt in Options.Listeners)
        {
            serverOptions.Listen(opt.IPAddress, opt.Port, listenOptions =>
            {
                listenOptions.Protocols = opt.Protocols;
                listenOptions.DisableAltSvcHeader = opt.DisableAltSvcHeader;
                if (opt.UseHttps && opt.X509Certificate is not null)
                {
                    _ = listenOptions.UseHttps(opt.X509Certificate);
                }
                if (opt.UseConnectionLogging)
                {
                    _ = listenOptions.UseConnectionLogging();
                }
            });
        }
    }

    /// <summary>
    /// Logs the configured endpoints after building the application.
    /// </summary>
    internal void LogConfiguredEndpoints()
    {
        // build the app to validate configuration
        _app = Build();
        // Log configured endpoints
        var dataSource = _app.Services.GetRequiredService<EndpointDataSource>();

        if (dataSource.Endpoints.Count == 0)
        {
            Logger.Warning("EndpointDataSource is empty. No endpoints configured.");
        }
        else
        {
            foreach (var ep in dataSource.Endpoints)
            {
                Logger.Information("➡️  Endpoint: {DisplayName}", ep.DisplayName);
            }
        }
    }

    /// <summary>
    /// Handles configuration errors and wraps them with meaningful messages.
    /// </summary>
    /// <param name="ex">The exception that occurred during configuration.</param>
    /// <exception cref="InvalidOperationException">Always thrown with wrapped exception.</exception>
    internal void HandleConfigurationError(Exception ex)
    {
        Logger.Error(ex, "Error applying configuration: {Message}", ex.Message);
        throw new InvalidOperationException("Failed to apply configuration.", ex);
    }

    /// <summary>
    /// Applies the configured options to the Kestrel server and initializes the runspace pool.
    /// </summary>
    public void EnableConfiguration(Dictionary<string, object>? userVariables = null, Dictionary<string, string>? userFunctions = null)
    {
        if (!ValidateConfiguration())
        {
            return;
        }

        try
        {
            InitializeRunspacePool(userVariables, userFunctions);
            ConfigureKestrelBase();
            ConfigureNamedPipes();

            // Apply Kestrel listeners and HTTPS settings
            _ = Builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                ConfigureHttpsAdapter(serverOptions);
                BindListeners(serverOptions);
            });

            LogConfiguredEndpoints();

            // Register default probes after endpoints are logged but before marking configured
            RegisterDefaultHealthProbes();
            IsConfigured = true;
            Logger.Information("Configuration applied successfully.");
        }
        catch (Exception ex)
        {
            HandleConfigurationError(ex);
        }
    }

    /// <summary>
    /// Registers built-in default health probes (idempotent). Currently includes disk space probe.
    /// </summary>
    private void RegisterDefaultHealthProbes()
    {
        try
        {
            // Avoid duplicate registration if user already added a probe named "disk".
            lock (_healthProbeLock)
            {
                if (HealthProbes.Any(p => string.Equals(p.Name, "disk", StringComparison.OrdinalIgnoreCase)))
                {
                    return; // already present
                }
            }

            var tags = new[] { IProbe.TAG_SELF }; // neutral tag; user can filter by name if needed
            var diskProbe = new DiskSpaceProbe("disk", tags);
            RegisterProbeInternal(diskProbe);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to register default disk space probe.");
        }
    }

    #endregion
    #region Builder
    /* More information about the KestrunHost class
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.webapplication?view=aspnetcore-8.0

    */

    /// <summary>
    /// Builds the WebApplication.
    /// This method applies all queued services and middleware stages,
    /// and returns the built WebApplication instance.
    /// </summary>
    /// <returns>The built WebApplication.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public WebApplication Build()
    {
        ValidateBuilderState();
        ApplyQueuedServices();
        BuildWebApplication();
        ConfigureBuiltInMiddleware();
        LogApplicationInfo();
        ApplyQueuedMiddleware();
        ApplyFeatures();

        return _app!;
    }

    /// <summary>
    /// Validates that the builder is properly initialized before building.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the builder is not initialized.</exception>
    private void ValidateBuilderState()
    {
        if (Builder == null)
        {
            throw new InvalidOperationException("Call CreateBuilder() first.");
        }
    }

    /// <summary>
    /// Applies all queued service configurations to the service collection.
    /// </summary>
    private void ApplyQueuedServices()
    {
        foreach (var configure in _serviceQueue)
        {
            configure(Builder.Services);
        }
    }

    /// <summary>
    /// Builds the WebApplication instance from the configured builder.
    /// </summary>
    private void BuildWebApplication()
    {
        _app = Builder.Build();
        Logger.Information("Application built successfully.");
    }

    /// <summary>
    /// Configures built-in middleware components in the correct order.
    /// </summary>
    private void ConfigureBuiltInMiddleware()
    {
        ConfigureExceptionHandling();
        ConfigureStatusCodePages();
        ConfigurePowerShellRuntime();
    }

    /// <summary>
    /// Configures exception handling middleware if enabled.
    /// </summary>
    private void ConfigureExceptionHandling()
    {
        if (ExceptionOptions is not null)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Exception handling middleware is enabled.");
            }
            _ = ExceptionOptions.DeveloperExceptionPageOptions is not null
                ? _app!.UseDeveloperExceptionPage(ExceptionOptions.DeveloperExceptionPageOptions)
                : _app!.UseExceptionHandler(ExceptionOptions);
        }
    }

    /// <summary>
    /// Configures status code pages middleware if enabled.
    /// </summary>
    private void ConfigureStatusCodePages()
    {
        // Register StatusCodePages BEFORE language runtimes so that re-executed requests
        // pass through language middleware again (and get fresh RouteValues/context).
        if (StatusCodeOptions is not null)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Status code pages middleware is enabled.");
            }
            _ = _app!.UseStatusCodePages(StatusCodeOptions);
        }
    }

    /// <summary>
    /// Configures PowerShell runtime middleware if enabled.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when PowerShell is enabled but runspace pool is not initialized.</exception>
    private void ConfigurePowerShellRuntime()
    {
        if (PowershellMiddlewareEnabled)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("PowerShell middleware is enabled.");
            }

            if (_runspacePool is null)
            {
                throw new InvalidOperationException("Runspace pool is not initialized. Call EnableConfiguration first.");
            }
            Logger.Information("Adding PowerShell runtime");
            _ = _app!.UseLanguageRuntime(
                    ScriptLanguage.PowerShell,
                    b => b.UsePowerShellRunspace(_runspacePool));
        }
    }

    /// <summary>
    /// Logs application information including working directory and Pages directory contents.
    /// </summary>
    private void LogApplicationInfo()
    {
        Logger.Information("CWD: {CWD}", Directory.GetCurrentDirectory());
        Logger.Information("ContentRoot: {Root}", _app!.Environment.ContentRootPath);
        LogPagesDirectory();
    }

    /// <summary>
    /// Logs information about the Pages directory and its contents.
    /// </summary>
    private void LogPagesDirectory()
    {
        var pagesDir = Path.Combine(_app!.Environment.ContentRootPath, "Pages");
        Logger.Information("Pages Dir: {PagesDir}", pagesDir);

        if (Directory.Exists(pagesDir))
        {
            foreach (var file in Directory.GetFiles(pagesDir, "*.*", SearchOption.AllDirectories))
            {
                Logger.Information("Pages file: {File}", file);
            }
        }
        else
        {
            Logger.Warning("Pages directory does not exist: {PagesDir}", pagesDir);
        }
    }

    /// <summary>
    /// Applies all queued middleware stages to the application pipeline.
    /// </summary>
    private void ApplyQueuedMiddleware()
    {
        foreach (var stage in _middlewareQueue)
        {
            stage(_app!);
        }
    }

    /// <summary>
    /// Applies all queued features to the host.
    /// </summary>
    private void ApplyFeatures()
    {
        foreach (var feature in FeatureQueue)
        {
            feature(this);
        }
    }

    /// <summary>
    /// Adds a service configuration action to the service queue.
    /// This action will be executed when the services are built.
    /// </summary>
    /// <param name="configure">The service configuration action.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddService(Action<IServiceCollection> configure)
    {
        _serviceQueue.Add(configure);
        return this;
    }

    /// <summary>
    /// Adds a middleware stage to the application pipeline.
    /// </summary>
    /// <param name="stage">The middleware stage to add.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost Use(Action<IApplicationBuilder> stage)
    {
        _middlewareQueue.Add(stage);
        return this;
    }

    /// <summary>
    /// Adds a feature configuration action to the feature queue.
    /// This action will be executed when the features are applied.
    /// </summary>
    /// <param name="feature">The feature configuration action.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddFeature(Action<KestrunHost> feature)
    {
        FeatureQueue.Add(feature);
        return this;
    }

    /// <summary>
    /// Adds a scheduling feature to the Kestrun host, optionally specifying the maximum number of runspaces for the scheduler.
    /// </summary>
    /// <param name="MaxRunspaces">The maximum number of runspaces for the scheduler. If null, uses the default value.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddScheduling(int? MaxRunspaces = null)
    {
        return MaxRunspaces is not null and <= 0
            ? throw new ArgumentOutOfRangeException(nameof(MaxRunspaces), "MaxRunspaces must be greater than zero.")
            : AddFeature(host =>
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("AddScheduling (deferred)");
            }

            if (host.Scheduler is null)
            {
                if (MaxRunspaces is not null and > 0)
                {
                    Logger.Information("Setting MaxSchedulerRunspaces to {MaxRunspaces}", MaxRunspaces);
                    host.Options.MaxSchedulerRunspaces = MaxRunspaces.Value;
                }
                Logger.Verbose("Creating SchedulerService with MaxSchedulerRunspaces={MaxRunspaces}",
                    host.Options.MaxSchedulerRunspaces);
                var pool = host.CreateRunspacePool(host.Options.MaxSchedulerRunspaces);
                var logger = Logger.ForContext<KestrunHost>();
                host.Scheduler = new SchedulerService(pool, logger);
            }
            else
            {
                Logger.Warning("SchedulerService already configured; skipping.");
            }
        });
    }

    /// <summary>
    /// Adds MVC / API controllers to the application.
    /// </summary>
    /// <param name="cfg">The configuration options for MVC / API controllers.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddControllers(Action<Microsoft.AspNetCore.Mvc.MvcOptions>? cfg = null)
    {
        return AddService(services =>
        {
            var builder = services.AddControllers();
            if (cfg != null)
            {
                _ = builder.ConfigureApplicationPartManager(pm => { }); // customise if you wish
            }
        });
    }




    /// <summary>
    /// Adds a PowerShell runtime to the application.
    /// This middleware allows you to execute PowerShell scripts in response to HTTP requests.
    /// </summary>
    /// <param name="routePrefix">The route prefix to use for the PowerShell runtime.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRuntime(PathString? routePrefix = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding PowerShell runtime with route prefix: {RoutePrefix}", routePrefix);
        }

        return Use(app =>
        {
            ArgumentNullException.ThrowIfNull(_runspacePool);
            // ── mount PowerShell at the root ──
            _ = app.UseLanguageRuntime(
                ScriptLanguage.PowerShell,
                b => b.UsePowerShellRunspace(_runspacePool));
        });
    }







    // ② SignalR
    /// <summary>
    /// Adds a SignalR hub to the application at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the SignalR hub.</typeparam>
    /// <param name="path">The path at which to map the SignalR hub.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddSignalR<T>(string path) where T : Hub
    {
        return AddService(s => s.AddSignalR())
               .Use(app => ((IEndpointRouteBuilder)app).MapHub<T>(path));
    }

    /*
        // ④ gRPC
        public KestrunHost AddGrpc<TService>() where TService : class
        {
            return AddService(s => s.AddGrpc())
                   .Use(app => app.MapGrpcService<TService>());
        }
    */

    /*   public KestrunHost AddSwagger()
       {
           AddService(s =>
           {
               s.AddEndpointsApiExplorer();
               s.AddSwaggerGen();
           });
           //  ⚠️ Swagger’s middleware normally goes first in the pipeline
           return Use(app =>
           {
               app.UseSwagger();
               app.UseSwaggerUI();
           });
       }*/

    // Add as many tiny helpers as you wish:
    // • AddAuthentication(jwt => { … })
    // • AddSignalR()
    // • AddHealthChecks()
    // • AddGrpc()
    // etc.

    #endregion
    #region Run/Start/Stop

    /// <summary>
    /// Runs the Kestrun web application, applying configuration and starting the server.
    /// </summary>
    public void Run()
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Run() called");
        }

        EnableConfiguration();

        _app?.Run();
    }

    /// <summary>
    /// Starts the Kestrun web application asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("StartAsync() called");
        }

        EnableConfiguration();
        if (_app != null)
        {
            await _app.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Stops the Kestrun web application asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("StopAsync() called");
        }

        if (_app != null)
        {
            try
            {
                // Initiate graceful shutdown
                await _app.StopAsync(cancellationToken);
            }
            catch (Exception ex) when (ex.GetType().FullName == "System.Net.Quic.QuicException")
            {
                // QUIC exceptions can occur during shutdown, especially if the server is not using QUIC.
                // We log this as a debug message to avoid cluttering the logs with expected exceptions.
                // This is a workaround for

                Logger.Debug("Ignored QUIC exception during shutdown: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Initiates a graceful shutdown of the Kestrun web application.
    /// </summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1)
        {
            return; // already stopping
        }
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Stop() called");
        }
        // This initiates a graceful shutdown.
        _app?.Lifetime.StopApplication();
    }

    /// <summary>
    /// Determines whether the Kestrun web application is currently running.
    /// </summary>
    /// <returns>True if the application is running; otherwise, false.</returns>
    public bool IsRunning
    {
        get
        {
            var appField = typeof(KestrunHost)
                .GetField("_app", BindingFlags.NonPublic | BindingFlags.Instance);

            return appField?.GetValue(this) is WebApplication app && !app.Lifetime.ApplicationStopping.IsCancellationRequested;
        }
    }

    #endregion



    #region Runspace Pool Management

    /// <summary>
    /// Creates and returns a new <see cref="KestrunRunspacePoolManager"/> instance with the specified maximum number of runspaces.
    /// </summary>
    /// <param name="maxRunspaces">The maximum number of runspaces to create. If not specified or zero, defaults to twice the processor count.</param>
    /// <param name="userVariables">A dictionary of user-defined variables to inject into the runspace pool.</param>
    /// <param name="userFunctions">A dictionary of user-defined functions to inject into the runspace pool.</param>
    /// <returns>A configured <see cref="KestrunRunspacePoolManager"/> instance.</returns>
    public KestrunRunspacePoolManager CreateRunspacePool(int? maxRunspaces = 0, Dictionary<string, object>? userVariables = null, Dictionary<string, string>? userFunctions = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("CreateRunspacePool() called: {@MaxRunspaces}", maxRunspaces);
        }

        // Create a default InitialSessionState with an unrestricted policy:
        var iss = InitialSessionState.CreateDefault();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, we can use the full .NET Framework modules
            iss.ExecutionPolicy = ExecutionPolicy.Unrestricted;
        }
        foreach (var p in _modulePaths)
        {
            iss.ImportPSModule([p]);
        }

        // Inject 'KrServer' variable to provide access to the host instance
        iss.Variables.Add(
            new SessionStateVariableEntry(
                "KrServer",
                this,
                "The Kestrun Server Host (KestrunHost) instance"
            )
        );
        // Inject global variables into all runspaces
        foreach (var kvp in SharedStateStore.Snapshot())
        {
            // kvp.Key = "Visits", kvp.Value = 0
            iss.Variables.Add(
                new SessionStateVariableEntry(
                    kvp.Key,
                    kvp.Value,
                    "Global variable"
                )
            );
        }

        foreach (var kvp in userVariables ?? [])
        {
            if (kvp.Value is PSVariable psVar)
            {
                iss.Variables.Add(
                    new SessionStateVariableEntry(
                        kvp.Key,
                        psVar.Value,
                        psVar.Description ?? "User-defined variable"
                    )
                );
            }
            else
            {
                iss.Variables.Add(
                    new SessionStateVariableEntry(
                        kvp.Key,
                        kvp.Value,
                        "User-defined variable"
                    )
                );
            }
        }

        foreach (var r in userFunctions ?? [])
        {
            var name = r.Key;
            var def = r.Value;

            // Use the string-based ctor available in 7.4 ref/net8.0
            var entry = new SessionStateFunctionEntry(
                name,
                def,
                ScopedItemOptions.ReadOnly,   // or ScopedItemOptions.None if you want them mutable
                helpFile: null
            );

            iss.Commands.Add(entry);
        }

        // Determine max runspaces
        var maxRs = (maxRunspaces.HasValue && maxRunspaces.Value > 0) ? maxRunspaces.Value : Environment.ProcessorCount * 2;

        Logger.Information($"Creating runspace pool with max runspaces: {maxRs}");
        var runspacePool = new KestrunRunspacePoolManager(this, Options?.MinRunspaces ?? 1, maxRunspaces: maxRs, initialSessionState: iss);
        // Return the created runspace pool
        return runspacePool;
    }


    #endregion


    #region Disposable

    /// <summary>
    /// Releases all resources used by the <see cref="KestrunHost"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Dispose() called");
        }

        _runspacePool?.Dispose();
        _runspacePool = null; // Clear the runspace pool reference
        IsConfigured = false; // Reset configuration state
        _app = null;
        Scheduler?.Dispose();
        (Logger as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Script Validation


    #endregion
}
