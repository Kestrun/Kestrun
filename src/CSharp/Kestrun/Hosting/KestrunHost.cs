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
using Kestrun.Middleware;
using Kestrun.Scripting;
using Kestrun.Localization;
using Kestrun.Hosting.Options;
using System.Runtime.InteropServices;
using Microsoft.PowerShell;
using System.Net.Sockets;
using Microsoft.Net.Http.Headers;
using Kestrun.Authentication;
using Kestrun.Health;
using Kestrun.Tasks;
using Kestrun.Runtime;
using Kestrun.OpenApi;
using Microsoft.AspNetCore.Antiforgery;
using Kestrun.Callback;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;
using Kestrun.Forms;

namespace Kestrun.Hosting;

/// <summary>
/// Provides hosting and configuration for the Kestrun application, including service registration, middleware setup, and runspace pool management.
/// </summary>
public partial class KestrunHost : IDisposable
{
    private const string KestrunVariableMarkerKey = "__kestrunVariable";

    #region Static Members
    private static readonly JsonSerializerOptions JsonOptions;

    static KestrunHost()
    {
        JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }
    #endregion

    #region Fields
    internal WebApplicationBuilder Builder { get; }

    private WebApplication? _app;

    internal WebApplication App => _app ?? throw new InvalidOperationException("WebApplication is not built yet. Call Build() first.");

    /// <summary>
    /// Gets the runtime information for the Kestrun host.
    /// </summary>
    public KestrunHostRuntime Runtime { get; } = new();

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

    /// <summary>
    /// The runspace pool manager for PowerShell execution.
    /// </summary>
    private KestrunRunspacePoolManager? _runspacePool;

    /// <summary>
    /// Status code options for configuring status code pages.
    /// </summary>
    private StatusCodeOptions? _statusCodeOptions;
    /// <summary>
    /// Exception options for configuring exception handling.
    /// </summary>
    private ExceptionOptions? _exceptionOptions;
    /// <summary>
    /// Forwarded headers options for configuring forwarded headers handling.
    /// </summary>
    private ForwardedHeadersOptions? _forwardedHeaderOptions;

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

    internal readonly Dictionary<(string Pattern, HttpVerb Method), MapRouteOptions> _registeredRoutes =
    new(new RouteKeyComparer());

    //internal readonly Dictionary<(string Scheme, string Type), AuthenticationSchemeOptions> _registeredAuthentications =
    //  new(new AuthKeyComparer());

    /// <summary>
    /// Gets the root directory path for the Kestrun application.
    /// </summary>
    public string? KestrunRoot { get; private set; }

    /// <summary>
    /// Gets the collection of module paths to be loaded by the Kestrun host.
    /// </summary>
    public List<string> ModulePaths => _modulePaths;

    /// <summary>
    /// Gets the shared state store for managing shared data across requests and sessions.
    /// </summary>
    public SharedState.SharedState SharedState { get; }

    /// <summary>
    /// Gets the Serilog logger instance used by the Kestrun host.
    /// </summary>
    public Serilog.ILogger Logger { get; private set; }

    private SchedulerService? _scheduler;
    /// <summary>
    /// Gets the scheduler service used for managing scheduled tasks in the Kestrun host.
    /// Initialized in ConfigureServices via AddScheduler()
    /// </summary>
    public SchedulerService Scheduler
    {
        get => _scheduler ?? throw new InvalidOperationException("SchedulerService is not initialized. Call AddScheduler() to enable scheduling.");
        internal set => _scheduler = value;
    }

    private KestrunTaskService? _tasks;
    /// <summary>
    /// Gets the ad-hoc task service used for running one-off tasks (PowerShell, C#, VB.NET).
    /// Initialized via AddTasks()
    /// </summary>
    public KestrunTaskService Tasks
    {
        get => _tasks ?? throw new InvalidOperationException("Tasks is not initialized. Call AddTasks() to enable task management.");
        internal set => _tasks = value;
    }

    /// <summary>
    /// Gets the stack used for managing route groups in the Kestrun host.
    /// </summary>
    public System.Collections.Stack RouteGroupStack { get; } = new();

    /// <summary>
    /// Gets the registered routes in the Kestrun host.
    /// </summary>
    public Dictionary<(string, HttpVerb), MapRouteOptions> RegisteredRoutes => _registeredRoutes;

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
    /// The localization store used by this host when `UseKestrunLocalization` is configured.
    /// May be null if localization middleware was not added.
    /// </summary>
    public KestrunLocalizationStore? LocalizationStore { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is the default Kestrun host.
    /// </summary>
    public bool DefaultHost { get; internal set; }

    /// <summary>
    /// The list of CORS policy names that have been defined in the KestrunHost instance.
    /// </summary>
    public List<string> DefinedCorsPolicyNames { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether CORS (Cross-Origin Resource Sharing) is enabled.
    /// </summary>
    public bool CorsPolicyDefined => DefinedCorsPolicyNames.Count > 0;

    /// <summary>
    /// Gets the scanned OpenAPI component annotations from PowerShell scripts.
    /// </summary>
    public Dictionary<string, OpenApiComponentAnnotationScanner.AnnotatedVariable>? ComponentAnnotations { get; private set; }

    /// <summary>
    /// Gets or sets the status code options for configuring status code pages.
    /// </summary>
    public StatusCodeOptions? StatusCodeOptions
    {
        get => _statusCodeOptions;
        set
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException("Cannot modify StatusCodeOptions after configuration is applied.");
            }
            _statusCodeOptions = value;
        }
    }

    /// <summary>
    /// Gets or sets the exception options for configuring exception handling.
    /// </summary>
    public ExceptionOptions? ExceptionOptions
    {
        get => _exceptionOptions;
        set
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException("Cannot modify ExceptionOptions after configuration is applied.");
            }
            _exceptionOptions = value;
        }
    }

    /// <summary>
    /// Gets or sets the forwarded headers options for configuring forwarded headers handling.
    /// </summary>
    public ForwardedHeadersOptions? ForwardedHeaderOptions
    {
        get => _forwardedHeaderOptions;
        set
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException("Cannot modify ForwardedHeaderOptions after configuration is applied.");
            }
            _forwardedHeaderOptions = value;
        }
    }

    /// <summary>
    /// Gets the antiforgery options for configuring antiforgery token generation and validation.
    /// </summary>
    public AntiforgeryOptions? AntiforgeryOptions { get; set; }

    /// <summary>
    /// Gets the OpenAPI document descriptor for configuring OpenAPI generation.
    /// </summary>
    public Dictionary<string, OpenApiDocDescriptor> OpenApiDocumentDescriptor { get; } = [];

    /// <summary>
    /// Gets the IDs of all OpenAPI documents configured in the Kestrun host.
    /// </summary>
    public string[] OpenApiDocumentIds => [.. OpenApiDocumentDescriptor.Keys];

    /// <summary>
    /// Gets the default OpenAPI document descriptor.
    /// </summary>
    public OpenApiDocDescriptor? DefaultOpenApiDocumentDescriptor
        => OpenApiDocumentDescriptor.FirstOrDefault().Value;

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
    /// Initializes a new instance of the <see cref="KestrunHost"/> class with the specified application name and logger.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <param name="logger">The Serilog logger instance to use.</param>
    /// <param name="ordinalIgnoreCase">Indicates whether the shared state should be case-insensitive.</param>
    public KestrunHost(string? appName, Serilog.ILogger logger,
          bool ordinalIgnoreCase) : this(appName, logger, null, null, null, ordinalIgnoreCase)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunHost"/> class with the specified application name, logger, root directory, and optional module paths.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <param name="logger">The Serilog logger instance to use.</param>
    /// <param name="kestrunRoot">The root directory for the Kestrun application.</param>
    /// <param name="modulePathsObj">An array of module paths to be loaded.</param>
    /// <param name="args">Command line arguments to pass to the application.</param>
    /// <param name="ordinalIgnoreCase">Indicates whether the shared state should be case-insensitive.</param>
    public KestrunHost(string? appName, Serilog.ILogger logger,
    string? kestrunRoot = null, string[]? modulePathsObj = null, string[]? args = null, bool ordinalIgnoreCase = true)
    {
        // ① Logger
        Logger = logger ?? Log.Logger;
        LogConstructorArgs(appName, logger == null, kestrunRoot, modulePathsObj?.Length ?? 0);
        SharedState = new(ordinalIgnoreCase: ordinalIgnoreCase);
        // ② Working directory/root
        SetWorkingDirectoryIfNeeded(kestrunRoot);

        // ③ Ensure Kestrun module path is available
        AddKestrunModulePathIfMissing(modulePathsObj);

        // ④ WebApplicationBuilder
        // NOTE:
        // ASP.NET Core's WebApplicationBuilder validates that ContentRootPath exists.
        // On Unix/macOS, the process current working directory (CWD) can be deleted by tests or external code.
        // If we derive ContentRootPath from a missing/deleted directory, CreateBuilder throws.
        // We therefore (a) choose an existing directory when possible and (b) retry with a stable fallback
        // to keep host creation resilient in CI where test ordering/parallelism can surface this.
        WebApplicationOptions CreateWebAppOptions(string contentRootPath)
        {
            return new()
            {
                ContentRootPath = contentRootPath,
                Args = args ?? [],
                EnvironmentName = EnvironmentHelper.Name
            };
        }

        var contentRootPath = GetSafeContentRootPath(kestrunRoot);

        try
        {
            Builder = WebApplication.CreateBuilder(CreateWebAppOptions(contentRootPath));
        }
        catch (ArgumentException ex) when (
            string.Equals(ex.ParamName, "contentRootPath", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(contentRootPath, AppContext.BaseDirectory, StringComparison.Ordinal))
        {
            // The selected content root may have been deleted between resolution and builder initialization
            // (TOCTOU race) or the process CWD may have become invalid. Fall back to a stable path so host
            // creation does not fail.
            Builder = WebApplication.CreateBuilder(CreateWebAppOptions(AppContext.BaseDirectory));
        }
        // ✅ add here, after Builder is definitely assigned
        _ = Builder.Services.Configure<HostOptions>(o =>
        {
            _ = o.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });

        // Enable Serilog for the host
        _ = Builder.Host.UseSerilog();

        // Make this KestrunHost available via DI so framework-created components (e.g., auth handlers)
        // can resolve it. We register the current instance as a singleton.
        _ = Builder.Services.AddSingleton(this);

        // Expose Serilog.ILogger via DI for components (e.g., SignalR hubs) that depend on Serilog's logger
        // ASP.NET Core registers Microsoft.Extensions.Logging.ILogger by default; we also bind Serilog.ILogger
        // to the same instance so constructors like `KestrunHub(Serilog.ILogger logger)` resolve properly.
        _ = Builder.Services.AddSingleton(Logger);

        // ⑤ Options
        InitializeOptions(appName);

        // ⑥ Add user-provided module paths
        AddUserModulePaths(modulePathsObj);

        Logger.Information("Current working directory: {CurrentDirectory}", GetSafeCurrentDirectory());
    }
    #endregion

    #region Helpers

    /// <summary>
    /// Adds a form parsing option for the specified name.
    /// </summary>
    /// <param name="options">The form options to add.</param>
    /// <returns>True if the option was added successfully; otherwise, false.</returns>
    public bool AddFormOption(KrFormOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Name);

        if (Runtime.FormOptions.TryAdd(options.Name, options))
        {
            // Link scoped rules under their container rule(s) once at configuration-time.
            // This keeps KrFormPartRule.NestedRules useful for introspection/debugging.
            FormHelper.PopulateNestedRulesFromScopes(options);

            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Added form option with name '{FormOptionName}'.", options.Name);
            }
            return true;
        }
        else
        {
            if (Logger.IsEnabled(LogEventLevel.Warning))
            {
                Logger.Warning("Form option with name '{FormOptionName}' already exists. Skipping addition.", options.Name);
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the form parsing option for the specified name.
    /// </summary>
    /// <param name="name">The name of the form option.</param>
    /// <returns>The form options if found; otherwise, null.</returns>
    public KrFormOptions? GetFormOption(string name) => Runtime.FormOptions.TryGetValue(name, out var options) ? options : null;

    /// <summary>
    /// Adds a form part rule for the specified name.
    /// </summary>
    /// <param name="ruleOptions">The form part rule to add.</param>
    /// <returns>True if the rule was added successfully; otherwise, false.</returns>
    public bool AddFormPartRule(KrFormPartRule ruleOptions)
    {
        ArgumentNullException.ThrowIfNull(ruleOptions);
        ArgumentNullException.ThrowIfNull(ruleOptions.Name);

        if (Runtime.FormPartRules.TryAdd(ruleOptions.Name, ruleOptions))
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Added form part rule with name '{FormPartRuleName}'.", ruleOptions.Name);
            }
            return true;
        }
        else
        {
            if (Logger.IsEnabled(LogEventLevel.Warning))
            {
                Logger.Warning("Form part rule with name '{FormPartRuleName}' already exists. Skipping addition.", ruleOptions.Name);
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the form part rule for the specified name.
    /// </summary>
    /// <param name="name">The name of the form part rule.</param>
    /// <returns>The form part rule if found; otherwise, null.</returns>
    public KrFormPartRule? GetFormPartRule(string name) => Runtime.FormPartRules.TryGetValue(name, out var options) ? options : null;

    /// <summary>
    /// Gets the OpenAPI document descriptor for the specified document ID.
    /// </summary>
    /// <param name="apiDocId">The ID of the OpenAPI document.</param>
    /// <returns>The OpenAPI document descriptor.</returns>
    public OpenApiDocDescriptor GetOrCreateOpenApiDocument(string apiDocId)
    {
        if (string.IsNullOrWhiteSpace(apiDocId))
        {
            throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(apiDocId));
        }
        // Check if descriptor already exists
        if (OpenApiDocumentDescriptor.TryGetValue(apiDocId, out var descriptor))
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("OpenAPI document descriptor for ID '{DocId}' already exists. Returning existing descriptor.", apiDocId);
            }
        }
        else
        {
            descriptor = new OpenApiDocDescriptor(this, apiDocId);
            OpenApiDocumentDescriptor[apiDocId] = descriptor;
        }
        return descriptor;
    }

    /// <summary>
    /// Gets the list of OpenAPI document descriptors for the specified document IDs.
    /// </summary>
    /// <param name="openApiDocIds"> The array of OpenAPI document IDs.</param>
    /// <returns>A list of OpenApiDocDescriptor objects corresponding to the provided document IDs.</returns>
    public List<OpenApiDocDescriptor> GetOrCreateOpenApiDocument(string[] openApiDocIds)
    {
        var list = new List<OpenApiDocDescriptor>();
        foreach (var apiDocId in openApiDocIds)
        {
            list.Add(GetOrCreateOpenApiDocument(apiDocId));
        }
        return list;
    }

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

        if (!string.Equals(GetSafeCurrentDirectory(), kestrunRoot, StringComparison.Ordinal))
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

    private static string GetSafeContentRootPath(string? kestrunRoot)
    {
        var candidate = !string.IsNullOrWhiteSpace(kestrunRoot)
            ? kestrunRoot
            : GetSafeCurrentDirectory();

        // WebApplication.CreateBuilder requires that ContentRootPath exists.
        // On Unix/macOS, getcwd() can fail (or return a path that was deleted) if the CWD was removed.
        // This can happen in tests that use temp directories and delete them after constructing a host.
        // Guard here to avoid injecting a non-existent content root into ASP.NET Core.
        return Directory.Exists(candidate)
            ? candidate
            : AppContext.BaseDirectory;
    }

    private static string GetSafeCurrentDirectory()
    {
        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            DirectoryNotFoundException or
            FileNotFoundException)
        {
            // On Unix/macOS, getcwd() can fail with ENOENT if the CWD was deleted.
            // Fall back to the app base directory to keep host creation resilient.
            return AppContext.BaseDirectory;
        }
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
        var probe = ScriptProbeFactory.Create(host: this, name: name, tags: tags,
            effectiveLanguage, code: code,
            runspaceAccessor: effectiveLanguage == ScriptLanguage.PowerShell ? () => RunspacePool : null,
            arguments: arguments, extraImports: extraImports, extraRefs: extraRefs);

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
    #region OpenAPI

    /// <summary>
    /// Adds callback automation middleware to the Kestrun host.
    /// </summary>
    /// <param name="options">Optional callback dispatch options.</param>
    /// <returns>The updated Kestrun host.</returns>
    public KestrunHost AddCallbacksAutomation(CallbackDispatchOptions? options = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug(
                "Adding callback automation middleware (custom configuration supplied: {HasConfig})",
                options != null);
        }
        options ??= new CallbackDispatchOptions();
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding callback automation middleware with options: {@Options}", options);
        }

        _ = AddService(services =>
        {
            _ = services.AddSingleton(options ?? new CallbackDispatchOptions());
            _ = services.AddSingleton<InMemoryCallbackQueue>();
            _ = services.AddSingleton<ICallbackDispatcher, InMemoryCallbackDispatcher>();
            _ = services.AddHostedService<InMemoryCallbackDispatchWorker>();
            _ = services.AddHttpClient("kestrun-callbacks", c =>
            {
                c.Timeout = options?.DefaultTimeout ?? TimeSpan.FromSeconds(30);
            });
            _ = services.AddSingleton<ICallbackRetryPolicy>(sp =>
                {
                    return new DefaultCallbackRetryPolicy(options);
                });

            _ = services.AddSingleton<ICallbackUrlResolver, DefaultCallbackUrlResolver>();
            _ = services.AddSingleton<ICallbackBodySerializer, JsonCallbackBodySerializer>();

            _ = services.AddHttpClient<ICallbackSender, HttpCallbackSender>();

            _ = services.AddHostedService<CallbackWorker>();
        });
        return this;
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
        // Validate state
        if (IsConfigured)
        {
            throw new InvalidOperationException("Cannot configure listeners after configuration is applied.");
        }
        // Validate protocols
        if (protocols == HttpProtocols.Http1AndHttp2AndHttp3 && !CcUtilities.PreviewFeaturesEnabled())
        {
            Logger.Warning("Http3 is not supported in this version of Kestrun. Using Http1 and Http2 only.");
            protocols = HttpProtocols.Http1AndHttp2;
        }
        // Add listener
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
    /// <param name="openApiClassesPath">Path to the OpenAPI class definitions to inject into the runspace pool.</param>
    /// <exception cref="InvalidOperationException">Thrown when runspace pool creation fails.</exception>
    internal void InitializeRunspacePool(Dictionary<string, object>? userVariables, Dictionary<string, string>? userFunctions, string openApiClassesPath)
    {
        _runspacePool =
            CreateRunspacePool(Options.MaxRunspaces, userVariables, userFunctions, openApiClassesPath) ??
            throw new InvalidOperationException("Failed to create runspace pool.");
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
    /// <param name="userVariables">User-defined variables to inject into the runspace pool.</param>
    /// <param name="userFunctions">User-defined functions to inject into the runspace pool.</param>
    /// <param name="userCallbacks">User-defined callback functions for OpenAPI classes.</param>
    public void EnableConfiguration(Dictionary<string, object>? userVariables = null, Dictionary<string, string>? userFunctions = null, Dictionary<string, string>? userCallbacks = null)
    {
        if (!ValidateConfiguration())
        {
            return;
        }

        try
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Applying configuration to KestrunHost.");
            }
            // Inject user variables into shared state
            _ = ApplyUserVarsToState(userVariables);

            // Scan for OpenAPI component annotations in the main script.
            // In C#-only scenarios (including xUnit tests), there may be no PowerShell entry script.
            ComponentAnnotations = !string.IsNullOrWhiteSpace(KestrunHostManager.EntryScriptPath)
                && File.Exists(KestrunHostManager.EntryScriptPath)
            ? OpenApiComponentAnnotationScanner.ScanFromPath(mainPath: KestrunHostManager.EntryScriptPath)
            : null;

            // Export OpenAPI classes from PowerShell
            var openApiClassesPath = ExportOpenApiClasses(userCallbacks);
            // Initialize PowerShell runspace pool
            InitializeRunspacePool(userVariables: null, userFunctions: userFunctions, openApiClassesPath: openApiClassesPath);
            // Configure Kestrel server
            ConfigureKestrelBase();
            // Configure named pipe listeners if any
            ConfigureNamedPipes();

            // Apply Kestrel listeners and HTTPS settings
            _ = Builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                ConfigureHttpsAdapter(serverOptions);
                BindListeners(serverOptions);
            });

            // Generate OpenAPI components after runspace is ready
            foreach (var openApiDocument in OpenApiDocumentDescriptor.Values)
            {
                openApiDocument.GenerateComponents();
            }

            // Log configured endpoints after building
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
    /// Applies user-defined variables to the shared state.
    /// </summary>
    /// <param name="userVariables">User-defined variables to inject into the shared state.</param>
    /// <returns>True if all variables were successfully applied; otherwise, false.</returns>
    private bool ApplyUserVarsToState(Dictionary<string, object>? userVariables)
    {
        var statusSet = true;
        if (userVariables is not null)
        {
            foreach (var v in userVariables)
            {
                statusSet &= SharedState.Set(v.Key, v.Value, true);
            }
        }
        return statusSet;
    }

    /// <summary>
    /// Exports OpenAPI classes from PowerShell.
    /// </summary>
    /// <param name="userCallbacks">User-defined callbacks for OpenAPI class export.</param>
    private string ExportOpenApiClasses(Dictionary<string, string>? userCallbacks)
    {
        // Export OpenAPI classes from PowerShell
        var openApiClassesPath = PowerShellOpenApiClassExporter.ExportOpenApiClasses(userCallbacks: userCallbacks);
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            if (string.IsNullOrWhiteSpace(openApiClassesPath))
            {
                Logger.Debug("No OpenAPI classes exported from PowerShell.");
            }
            else
            {
                Logger.Debug("Exported OpenAPI classes from PowerShell: {path}", openApiClassesPath);
            }
        }
        return openApiClassesPath;
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

        // 🔔 SignalR shutdown notification
        _ = _app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                using var scope = _app.Services.CreateScope();

                var isService = scope.ServiceProvider.GetService<IServiceProviderIsService>();
                if (isService?.IsService(typeof(IHubContext<SignalR.KestrunHub>)) != true)
                {
                    Logger.Debug("SignalR hub context not available. Skipping shutdown notification.");
                    return;
                }

                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<SignalR.KestrunHub>>();
                _ = hub.Clients.All.SendAsync("serverShutdown", "Server stopping");
                Logger.Information("Sent SignalR shutdown notification to clients.");
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to send SignalR shutdown notification.");
            }
        });
    }

    /// <summary>
    /// Configures built-in middleware components in the correct order.
    /// </summary>
    private void ConfigureBuiltInMiddleware()
    {
        // Configure routing
        ConfigureRouting();
        // Configure CORS
        ConfigureCors();
        // Configure exception handling
        ConfigureExceptionHandling();
        // Configure forwarded headers
        ConfigureForwardedHeaders();
        // Configure status code pages
        ConfigureStatusCodePages();
        // Configure PowerShell runtime
        ConfigurePowerShellRuntime();
    }

    /// <summary>
    /// Configures routing middleware.
    /// </summary>
    private void ConfigureRouting()
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Enabling routing middleware.");
        }
        _ = _app!.UseRouting();
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Routing middleware is enabled.");
        }
    }

    /// <summary>
    /// Configures CORS middleware if a CORS policy is defined.
    /// </summary>
    private void ConfigureCors()
    {
        if (CorsPolicyDefined)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Enabling CORS middleware.");
            }
            _ = _app!.UseCors();
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("CORS middleware is enabled.");
            }
        }
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
                Logger.Debug("Enabling exception handling middleware.");
            }
            _ = ExceptionOptions.DeveloperExceptionPageOptions is not null
                ? _app!.UseDeveloperExceptionPage(ExceptionOptions.DeveloperExceptionPageOptions)
                : _app!.UseExceptionHandler(ExceptionOptions);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Exception handling middleware is enabled.");
            }
        }
    }

    /// <summary>
    /// Configures forwarded headers middleware if enabled.
    /// </summary>
    private void ConfigureForwardedHeaders()
    {
        if (ForwardedHeaderOptions is not null)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Enabling forwarded headers middleware.");
            }
            _ = _app!.UseForwardedHeaders(ForwardedHeaderOptions);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Forwarded headers middleware is enabled.");
            }
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
                Logger.Debug("Enabling status code pages middleware.");
            }
            _ = _app!.UseStatusCodePages(StatusCodeOptions);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Status code pages middleware is enabled.");
            }
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
                Logger.Debug("Enabling PowerShell middleware.");
            }

            if (_runspacePool is null)
            {
                throw new InvalidOperationException("Runspace pool is not initialized. Call EnableConfiguration first.");
            }

            Logger.Information("Adding PowerShell runtime");
            _ = _app!.UseLanguageRuntime(
                    ScriptLanguage.PowerShell,
                    b => b.UsePowerShellRunspace(_runspacePool));

            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("PowerShell middleware is enabled.");
            }
        }
    }

    /// <summary>
    /// Logs application information including working directory and Pages directory contents.
    /// </summary>
    private void LogApplicationInfo()
    {
        Logger.Information("CWD: {CWD}", GetSafeCurrentDirectory());
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
    /// Returns true if the specified service type has already been registered in the IServiceCollection.
    /// </summary>
    public bool IsServiceRegistered(Type serviceType)
        => Builder?.Services?.Any(sd => sd.ServiceType == serviceType) ?? false;

    /// <summary>
    /// Generic convenience overload.
    /// </summary>
    public bool IsServiceRegistered<TService>() => IsServiceRegistered(typeof(TService));

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

            if (host._scheduler is null)
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
    /// Adds the Tasks feature to run ad-hoc scripts with status/result/cancellation.
    /// </summary>
    /// <param name="MaxRunspaces">Optional max runspaces for the task PowerShell pool; when null uses scheduler default.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddTasks(int? MaxRunspaces = null)
    {
        return MaxRunspaces is not null and <= 0
            ? throw new ArgumentOutOfRangeException(nameof(MaxRunspaces), "MaxRunspaces must be greater than zero.")
            : AddFeature(host =>
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("AddTasks (deferred)");
            }

            if (host._tasks is null)
            {
                // Reuse scheduler pool sizing unless explicitly overridden
                if (MaxRunspaces is not null and > 0)
                {
                    Logger.Information("Setting MaxTaskRunspaces to {MaxRunspaces}", MaxRunspaces);
                }
                var pool = host.CreateRunspacePool(MaxRunspaces ?? host.Options.MaxSchedulerRunspaces);
                var logger = Logger.ForContext<KestrunHost>();
                host.Tasks = new KestrunTaskService(pool, logger);
            }
            else
            {
                Logger.Warning("KestrunTaskService already configured; skipping.");
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

    /// <summary>
    /// Adds the Realtime tag to the OpenAPI document if not already present.
    /// </summary>
    /// <param name="defTag"> OpenAPI document descriptor to which the Realtime tag will be added.</param>
    private static void AddRealTimeTag(OpenApiDocDescriptor defTag)
    {
        // Add Realtime default tag if not present
        if (!defTag.ContainsTag("Realtime"))
        {
            _ = defTag.AddTag(name: "Realtime",
                summary: "Real-time communication",
                description: "Protocols and endpoints for real-time, push-based communication such as SignalR and Server-Sent Events.",
                kind: "nav",
                externalDocs: new OpenApiExternalDocs
                {
                    Description = "Real-time communication overview",
                    Url = new Uri("https://learn.microsoft.com/aspnet/core/signalr/")
                });
        }
    }

    /// <summary>
    /// Adds the SignalR tag to the OpenAPI document if not already present.
    /// </summary>
    /// <param name="defTag"> OpenAPI document descriptor to which the SignalR tag will be added.</param>
    private static void AddSignalRTag(OpenApiDocDescriptor defTag)
    {
        if (!defTag.ContainsTag(SignalROptions.DefaultTag))
        {
            _ = defTag.AddTag(name: SignalROptions.DefaultTag,
                 description: "SignalR hubs providing real-time, bidirectional communication over persistent connections.",
                 summary: "SignalR hubs",
                 parent: "Realtime",
                  externalDocs: new OpenApiExternalDocs
                  {
                      Description = "ASP.NET Core SignalR documentation",
                      Url = new Uri("https://learn.microsoft.com/aspnet/core/signalr/introduction")
                  });
        }
    }

    /// <summary>
    /// Computes the SignalR negotiate endpoint path based on the hub path.
    /// </summary>
    /// <param name="hubPath">The hub route path.</param>
    /// <returns>The negotiate path for the hub.</returns>
    private static string GetSignalRNegotiatePath(string hubPath)
        => hubPath.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase)
            ? hubPath
            : hubPath.TrimEnd('/') + "/negotiate";

    /// <summary>
    /// Creates a native route registration with no script body.
    /// </summary>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="verb">The HTTP verb for the route.</param>
    /// <returns>A configured <see cref="MapRouteOptions"/> instance.</returns>
    private static MapRouteOptions CreateNativeRouteOptions(string pattern, HttpVerb verb)
        => new()
        {
            Pattern = pattern,
            HttpVerbs = [verb],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native,
                Code = string.Empty
            }
        };

    /// <summary>
    /// Registers a route in the internal route registry.
    /// </summary>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="verb">The HTTP verb.</param>
    /// <param name="routeOptions">The route options.</param>
    private void RegisterRoute(string pattern, HttpVerb verb, MapRouteOptions routeOptions)
        => _registeredRoutes[(pattern, verb)] = routeOptions;

    /// <summary>
    /// Ensures the default OpenAPI tags for real-time and SignalR are present when the caller uses default tagging.
    /// </summary>
    /// <param name="options">SignalR configuration options.</param>
    /// <param name="apiDocDescriptors">OpenAPI document descriptors to update.</param>
    private static void EnsureDefaultSignalRTags(SignalROptions options, IEnumerable<OpenApiDocDescriptor> apiDocDescriptors)
    {
        if (options.Tags?.Contains(SignalROptions.DefaultTag) != true)
        {
            return;
        }

        foreach (var defTag in apiDocDescriptors)
        {
            AddRealTimeTag(defTag);
            AddSignalRTag(defTag);
        }
    }

    /// <summary>
    /// Creates the common OpenAPI response set for the SignalR hub connect endpoint.
    /// </summary>
    /// <returns>The OpenAPI responses collection.</returns>
    private static OpenApiResponses CreateSignalRHubResponses()
        => new()
        {
            ["101"] = new OpenApiResponse { Description = "Switching Protocols (WebSocket upgrade)" },
            ["401"] = new OpenApiResponse { Description = "Unauthorized" },
            ["403"] = new OpenApiResponse { Description = "Forbidden" },
            ["404"] = new OpenApiResponse { Description = "Not Found" },
            ["500"] = new OpenApiResponse { Description = "Internal Server Error" }
        };

    /// <summary>
    /// Creates the common OpenAPI response set for the SignalR negotiate endpoint.
    /// </summary>
    /// <returns>The OpenAPI responses collection.</returns>
    private static OpenApiResponses CreateSignalRNegotiateResponses()
        => new()
        {
            ["200"] = new OpenApiResponse { Description = "Successful negotiation" },
            ["401"] = new OpenApiResponse { Description = "Unauthorized" },
            ["403"] = new OpenApiResponse { Description = "Forbidden" },
            ["404"] = new OpenApiResponse { Description = "Not Found" },
            ["500"] = new OpenApiResponse { Description = "Internal Server Error" }
        };

    /// <summary>
    /// Builds the OpenAPI extensions for SignalR endpoints.
    /// </summary>
    /// <param name="options">SignalR configuration options.</param>
    /// <param name="negotiatePath">The negotiate endpoint path.</param>
    /// <param name="role">The SignalR endpoint role (e.g., connect, negotiate).</param>
    /// <returns>Extensions dictionary for OpenAPI metadata.</returns>
    private static Dictionary<string, IOpenApiExtension> CreateSignalRExtensions(SignalROptions options, string negotiatePath, string role)
        => new()
        {
            ["x-signalr-role"] = new JsonNodeExtension(JsonValue.Create(role)),
            ["x-signalr"] = new JsonNodeExtension(new JsonObject
            {
                ["hub"] = options.HubName,
                ["path"] = options.Path,
                ["negotiatePath"] = negotiatePath,
                ["connectOperation"] = "get:" + options.Path,
                ["transports"] = new JsonArray("websocket", "sse", "longPolling"),
                ["formats"] = new JsonArray("json"),
            })
        };

    /// <summary>
    /// Adds OpenAPI metadata to the hub connect route, if OpenAPI is enabled.
    /// </summary>
    /// <param name="options">SignalR configuration options.</param>
    /// <param name="apiDocDescriptors">OpenAPI document descriptors for tag registration.</param>
    /// <param name="routeOptions">The route options to enrich with OpenAPI metadata.</param>
    /// <param name="negotiatePath">The computed negotiate endpoint path.</param>
    private void TryAddSignalRHubOpenApiMetadata(
        SignalROptions options,
        IEnumerable<OpenApiDocDescriptor> apiDocDescriptors,
        MapRouteOptions routeOptions,
        string negotiatePath)
    {
        if (options.SkipOpenApi)
        {
            return;
        }

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding OpenAPI metadata for SignalR hub at path: {Path}", options.Path);
        }

        EnsureDefaultSignalRTags(options, apiDocDescriptors);

        var meta = new OpenAPIPathMetadata(pattern: options.Path, mapOptions: routeOptions)
        {
            DocumentId = options.DocId,
            Summary = string.IsNullOrWhiteSpace(options.Summary) ? null : options.Summary,
            Description = string.IsNullOrWhiteSpace(options.Description) ? null : options.Description,
            Tags = options.Tags?.ToList() ?? [],
            Responses = CreateSignalRHubResponses(),
            Extensions = CreateSignalRExtensions(options, negotiatePath, role: "connect")
        };

        routeOptions.OpenAPI[HttpVerb.Get] = meta;
    }

    /// <summary>
    /// Adds OpenAPI metadata to the negotiate route, if OpenAPI is enabled.
    /// </summary>
    /// <param name="options">SignalR configuration options.</param>
    /// <param name="negotiateRouteOptions">The negotiate route options to enrich with OpenAPI metadata.</param>
    /// <param name="negotiatePath">The negotiate endpoint path.</param>
    private static void TryAddSignalRNegotiateOpenApiMetadata(
        SignalROptions options,
        MapRouteOptions negotiateRouteOptions,
        string negotiatePath)
    {
        if (options.SkipOpenApi)
        {
            return;
        }

        var negotiateMeta = new OpenAPIPathMetadata(pattern: negotiatePath, mapOptions: negotiateRouteOptions)
        {
            Summary = "SignalR negotiate endpoint",
            Description = "Negotiates connection parameters for a SignalR client before establishing the transport.",
            Tags = options.Tags?.ToList() ?? [],
            Responses = CreateSignalRNegotiateResponses(),
            Extensions = CreateSignalRExtensions(options, negotiatePath, role: "negotiate")
        };

        negotiateRouteOptions.OpenAPI[HttpVerb.Post] = negotiateMeta;
    }

    /// <summary>
    /// Registers SignalR services and JSON protocol configuration.
    /// </summary>
    /// <typeparam name="THub">The hub type being registered.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    private static void ConfigureSignalRServices<THub>(IServiceCollection services) where THub : Hub
    {
        _ = services.AddSignalR(o =>
        {
            o.HandshakeTimeout = TimeSpan.FromSeconds(5);
            o.KeepAliveInterval = TimeSpan.FromSeconds(2);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(10);
        }).AddJsonProtocol(opts =>
        {
            // Avoid failures when payloads contain cycles; our sanitizer should prevent most, this is a safety net.
            opts.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

        // Register IRealtimeBroadcaster as singleton if it's the KestrunHub
        if (typeof(THub) == typeof(SignalR.KestrunHub))
        {
            _ = services.AddSingleton<SignalR.IRealtimeBroadcaster, SignalR.RealtimeBroadcaster>();
            _ = services.AddSingleton<SignalR.IConnectionTracker, SignalR.InMemoryConnectionTracker>();
        }
    }

    /// <summary>
    /// Maps the SignalR hub to the application's endpoint route builder.
    /// </summary>
    /// <typeparam name="THub">The hub type being mapped.</typeparam>
    /// <param name="app">The application builder.</param>
    /// <param name="path">The hub path.</param>
    private static void MapSignalRHub<THub>(IApplicationBuilder app, string path) where THub : Hub
        => ((IEndpointRouteBuilder)app).MapHub<THub>(path);

    /// <summary>
    /// Adds a SignalR hub to the application at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the SignalR hub.</typeparam>
    /// <param name="options">The options for configuring the SignalR hub.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddSignalR<T>(SignalROptions options) where T : Hub
    {
        options ??= SignalROptions.Default;

        var apiDocDescriptors = GetOrCreateOpenApiDocument(options.DocId);
        var negotiatePath = GetSignalRNegotiatePath(options.Path);

        var routeOptions = CreateNativeRouteOptions(options.Path, HttpVerb.Get);
        TryAddSignalRHubOpenApiMetadata(options, apiDocDescriptors, routeOptions, negotiatePath);
        RegisterRoute(options.Path, HttpVerb.Get, routeOptions);

        if (options.IncludeNegotiateEndpoint)
        {
            var negotiateRouteOptions = CreateNativeRouteOptions(negotiatePath, HttpVerb.Post);
            TryAddSignalRNegotiateOpenApiMetadata(options, negotiateRouteOptions, negotiatePath);
            RegisterRoute(negotiatePath, HttpVerb.Post, negotiateRouteOptions);
        }

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding SignalR hub of type {HubType} at path: {Path}", typeof(T).FullName, options.Path);
        }

        return AddService(ConfigureSignalRServices<T>)
            .Use(app => MapSignalRHub<T>(app, options.Path));
    }

    /// <summary>
    /// Adds the default SignalR hub (KestrunHub) to the application at the specified path.
    /// </summary>
    /// <param name="options">The options for configuring the SignalR hub.</param>
    /// <returns></returns>
    public KestrunHost AddSignalR(SignalROptions options) => AddSignalR<SignalR.KestrunHub>(options);

    /*
        // ④ gRPC
        public KestrunHost AddGrpc<TService>() where TService : class
        {
            return AddService(s => s.AddGrpc())
                   .Use(app => app.MapGrpcService<TService>());
        }
    */

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
        Runtime.StartTime = DateTime.UtcNow;
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
            Runtime.StartTime = DateTime.UtcNow;
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
                Runtime.StopTime = DateTime.UtcNow;
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
        Runtime.StopTime = DateTime.UtcNow;
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
    /// <param name="openApiClassesPath">The file path to the OpenAPI class definitions to inject into the runspace pool.</param>
    /// <returns>A configured <see cref="KestrunRunspacePoolManager"/> instance.</returns>
    public KestrunRunspacePoolManager CreateRunspacePool(int? maxRunspaces = 0, Dictionary<string, object>? userVariables = null, Dictionary<string, string>? userFunctions = null, string? openApiClassesPath = null)
    {
        LogCreateRunspacePool(maxRunspaces);

        var iss = BuildInitialSessionState(openApiClassesPath);
        AddHostVariables(iss);
        AddSharedVariables(iss);
        AddUserVariables(iss, userVariables);
        AddUserFunctions(iss, userFunctions);

        var maxRs = ResolveMaxRunspaces(maxRunspaces);

        Logger.Information("Creating runspace pool with max runspaces: {MaxRunspaces}", maxRs);
        return new KestrunRunspacePoolManager(this, Options?.MinRunspaces ?? 1, maxRunspaces: maxRs, initialSessionState: iss, openApiClassesPath: openApiClassesPath);
    }

    private void LogCreateRunspacePool(int? maxRunspaces)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("CreateRunspacePool() called: {@MaxRunspaces}", maxRunspaces);
        }
    }

    private InitialSessionState BuildInitialSessionState(string? openApiClassesPath)
    {
        var iss = InitialSessionState.CreateDefault();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, we can use the full .NET Framework modules
            iss.ExecutionPolicy = ExecutionPolicy.Unrestricted;
        }

        ImportModulePaths(iss);
        AddOpenApiStartupScript(iss, openApiClassesPath);

        return iss;
    }

    private void ImportModulePaths(InitialSessionState iss)
    {
        foreach (var path in _modulePaths)
        {
            iss.ImportPSModule([path]);
        }
    }

    private void AddOpenApiStartupScript(InitialSessionState iss, string? openApiClassesPath)
    {
        if (string.IsNullOrWhiteSpace(openApiClassesPath))
        {
            return;
        }

        _ = iss.StartupScripts.Add(openApiClassesPath);
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Configured OpenAPI class script at {ScriptPath}", openApiClassesPath);
        }
    }

    private void AddHostVariables(InitialSessionState iss)
    {
        iss.Variables.Add(
            new SessionStateVariableEntry(
                "KrServer",
                this,
                "The Kestrun Server Host (KestrunHost) instance"
            )
        );
    }

    private void AddSharedVariables(InitialSessionState iss)
    {
        foreach (var kvp in SharedState.Snapshot())
        {
            iss.Variables.Add(
                new SessionStateVariableEntry(
                    kvp.Key,
                    kvp.Value,
                    "Global variable"
                )
            );
        }
    }

    private static void AddUserVariables(InitialSessionState iss, IReadOnlyDictionary<string, object>? userVariables)
    {
        if (userVariables is null)
        {
            return;
        }

        foreach (var kvp in userVariables)
        {
            if (kvp.Value is PSVariable psVar)
            {
                iss.Variables.Add(
                    new SessionStateVariableEntry(
                        kvp.Key,
                        UnwrapKestrunVariableValue(psVar.Value),
                        psVar.Description ?? "User-defined variable"
                    )
                );
                continue;
            }

            iss.Variables.Add(
                new SessionStateVariableEntry(
                    kvp.Key,
                    UnwrapKestrunVariableValue(kvp.Value),
                    "User-defined variable"
                )
            );
        }
    }

    /// <summary>
    /// Unwraps a Kestrun variable value if it is wrapped in a dictionary with a specific marker.
    /// </summary>
    /// <param name="raw">The raw variable value to unwrap.</param>
    /// <returns>The unwrapped variable value, or the original value if not wrapped.</returns>
    private static object? UnwrapKestrunVariableValue(object? raw)
    {
        if (raw is null)
        {
            return null;
        }

        // unwrap PSObject if needed
        raw = UnwrapPsObject(raw);

        // check for dictionary
        if (raw is not System.Collections.IDictionary dict)
        {
            return raw;
        }

        // check for marker key
        if (!TryGetDictionaryValueIgnoreCase(dict, KestrunVariableMarkerKey, out var markerObj))
        {
            return raw;
        }

        // check if marker is enabled
        if (!IsKestrunVariableMarkerEnabled(markerObj))
        {
            return raw;
        }

        // extract the "Value" entry
        return TryGetDictionaryValueIgnoreCase(dict, "Value", out var valueObj)
            ? UnwrapPsObject(valueObj)
            : null;
    }

    /// <summary>
    /// Unwraps a PowerShell <see cref="PSObject"/> by returning its <see cref="PSObject.BaseObject"/>.
    /// </summary>
    /// <param name="raw">The value to unwrap.</param>
    /// <returns>The underlying base object when <paramref name="raw"/> is a <see cref="PSObject"/>, otherwise <paramref name="raw"/>.</returns>
    private static object? UnwrapPsObject(object? raw)
        => raw is PSObject pso ? pso.BaseObject : raw;

    /// <summary>
    /// Determines whether the Kestrun variable marker is enabled.
    /// </summary>
    /// <param name="markerObj">The marker value (typically a boolean or a PowerShell-wrapped boolean).</param>
    /// <returns><c>true</c> if the marker indicates the value is wrapped; otherwise, <c>false</c>.</returns>
    private static bool IsKestrunVariableMarkerEnabled(object? markerObj)
        => markerObj switch
        {
            bool b => b,
            PSObject psMarker when psMarker.BaseObject is bool b => b,
            _ => false
        };

    private static bool TryGetDictionaryValueIgnoreCase(System.Collections.IDictionary dict, string key, out object? value)
    {
        value = null;

        if (dict.Contains(key))
        {
            value = dict[key];
            return true;
        }

        foreach (System.Collections.DictionaryEntry de in dict)
        {
            if (de.Key is string s && string.Equals(s, key, StringComparison.OrdinalIgnoreCase))
            {
                value = de.Value;
                return true;
            }
        }

        return false;
    }

    private static void AddUserFunctions(InitialSessionState iss, IReadOnlyDictionary<string, string>? userFunctions)
    {
        if (userFunctions is null)
        {
            return;
        }

        foreach (var function in userFunctions)
        {
            var entry = new SessionStateFunctionEntry(
                function.Key,
                function.Value,
                ScopedItemOptions.ReadOnly,
                helpFile: null
            );

            iss.Commands.Add(entry);
        }
    }

    private static int ResolveMaxRunspaces(int? maxRunspaces) =>
        (maxRunspaces.HasValue && maxRunspaces.Value > 0)
            ? maxRunspaces.Value
            : Environment.ProcessorCount * 2;

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
        _scheduler?.Dispose();
        (Logger as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Script Validation

    #endregion
}
