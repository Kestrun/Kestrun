using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Kestrun.Languages;
using Kestrun.SharedState;
using Kestrun.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.VisualBasic;
using KestrunCompilationErrorException = Kestrun.Scripting.CompilationErrorException;
using RoslynCompilationErrorException = Microsoft.CodeAnalysis.Scripting.CompilationErrorException;
using SerilogLogger = Serilog.ILogger;
using Serilog.Events;

namespace Kestrun.Health;

/// <summary>
/// Creates <see cref="IProbe"/> implementations backed by dynamic scripts.
/// </summary>
internal static class ScriptProbeFactory
{
    public const string STATUS_OK = "ok";
    public const string STATUS_HEALTHY = "healthy";
    public const string STATUS_WARN = "warn";
    public const string STATUS_WARNING = "warning";
    public const string STATUS_DEGRADED = "degraded";
    public const string STATUS_FAIL = "fail";
    public const string STATUS_FAILED = "failed";
    public const string STATUS_UNHEALTHY = "unhealthy";
    internal static IProbe Create(
        string name,
        IEnumerable<string>? tags,
        ScriptLanguage language,
        string code,
        SerilogLogger logger,
        Func<KestrunRunspacePoolManager>? runspaceAccessor,
        IReadOnlyDictionary<string, object?>? arguments,
        string[]? extraImports,
        Assembly[]? extraRefs)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return language switch
        {
            ScriptLanguage.PowerShell => CreatePowerShellProbe(name, tags, code, logger, runspaceAccessor, arguments),
            ScriptLanguage.CSharp => CreateCSharpProbe(name, tags, code, logger, arguments, extraImports, extraRefs),
            ScriptLanguage.VBNet => CreateVbProbe(name, tags, code, logger, arguments, extraImports, extraRefs),
            ScriptLanguage.Native => throw new NotSupportedException("Use AddProbe(Func<...>) for native probes."),
            ScriptLanguage.FSharp => throw new NotImplementedException("F# health probes are not yet supported."),
            ScriptLanguage.Python => throw new NotImplementedException("Python health probes are not yet supported."),
            ScriptLanguage.JavaScript => throw new NotImplementedException("JavaScript health probes are not yet supported."),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    private static IProbe CreatePowerShellProbe(
        string name,
        IEnumerable<string>? tags,
        string code,
        SerilogLogger logger,
        Func<KestrunRunspacePoolManager>? runspaceAccessor,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(runspaceAccessor);
        return new PowerShellScriptProbe(name, tags, code, logger, runspaceAccessor, arguments);
    }

    private static IProbe CreateCSharpProbe(
        string name,
        IEnumerable<string>? tags,
        string code,
        SerilogLogger logger,
        IReadOnlyDictionary<string, object?>? arguments,
        string[]? extraImports,
        Assembly[]? extraRefs)
    {
        try
        {
            var runner = BuildCSharpRunner(code, extraImports, extraRefs);
            return new CSharpScriptProbe(name, tags, runner, arguments, logger);
        }
        catch (RoslynCompilationErrorException ex)
        {
            logger.Error(ex, "Failed to compile C# health probe {Probe}.", name);
            throw;
        }
    }

    private static IProbe CreateVbProbe(
        string name,
        IEnumerable<string>? tags,
        string code,
        SerilogLogger logger,
        IReadOnlyDictionary<string, object?>? arguments,
        string[]? extraImports,
        Assembly[]? extraRefs)
    {
        try
        {
            var runner = VBNetDelegateBuilder.Compile<ProbeResult>(code, logger, extraImports, extraRefs, arguments, LanguageVersion.VisualBasic16_9);
            return new VbScriptProbe(name, tags, runner, arguments, logger);
        }
        catch (KestrunCompilationErrorException ex)
        {
            logger.Error(ex, "Failed to compile VB.NET health probe {Probe}.", name);
            throw;
        }
    }

    private static ScriptRunner<ProbeResult> BuildCSharpRunner(string code, string[]? extraImports, Assembly[]? extraRefs)
    {
        var options = ScriptOptions.Default
            .AddReferences(DelegateBuilder.BuildBaselineReferences())
            .AddReferences(typeof(ProbeResult).Assembly, typeof(ScriptProbeFactory).Assembly)
            .WithImports(DelegateBuilder.PlatformImports)
            .AddImports("Kestrun", "Kestrun.Health", "Kestrun.SharedState");

        if (extraImports is { Length: > 0 })
        {
            options = options.WithImports(options.Imports.Concat(extraImports).Distinct(StringComparer.Ordinal));
        }

        if (extraRefs is { Length: > 0 })
        {
            var additional = extraRefs
                .Where(static r => !string.IsNullOrEmpty(r.Location) && File.Exists(r.Location))
                .Select(static r => MetadataReference.CreateFromFile(r.Location));
            options = options.AddReferences(additional);
        }

        var script = CSharpScript.Create<ProbeResult>(code, options, typeof(CsGlobals));
        var diagnostics = script.Compile();
        return diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error)
            ? throw new RoslynCompilationErrorException("C# health probe compilation failed.", diagnostics)
            : script.CreateDelegate();
    }

    private sealed class PowerShellScriptProbe(
        string name,
        IEnumerable<string>? tags,
        string script,
        SerilogLogger logger,
        Func<KestrunRunspacePoolManager> poolAccessor,
        IReadOnlyDictionary<string, object?>? arguments) : IProbe
    {
        private readonly SerilogLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly Func<KestrunRunspacePoolManager> _poolAccessor = poolAccessor ?? throw new ArgumentNullException(nameof(poolAccessor));
        private readonly IReadOnlyDictionary<string, object?>? _arguments = arguments;
        private readonly string _script = string.IsNullOrWhiteSpace(script)
            ? throw new ArgumentException("Probe script cannot be null or whitespace.", nameof(script))
            : script;

        /// <summary>
        /// Gets the name of the probe.
        /// </summary>
        public string Name { get; } = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Probe name cannot be null or empty.", nameof(name))
            : name;

        /// <summary>
        /// The tags associated with the probe.
        /// </summary>
        public string[] Tags { get; } = tags is null
            ? []
            : [.. tags.Where(static t => !string.IsNullOrWhiteSpace(t))
                      .Select(static t => t.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc />
        public SerilogLogger Logger { get; init; } = logger;

        /// <summary>
        /// Executes the PowerShell script and converts the output to a <see cref="ProbeResult"/>.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation, with a <see cref="ProbeResult"/> as the result.</returns>
        public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
        {
            var pool = _poolAccessor();
            Runspace? runspace = null;
            try
            {
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("PowerShellScriptProbe {Probe} acquiring runspace", Name);
                }
                runspace = await pool.AcquireAsync(ct).ConfigureAwait(false);
                using var ps = CreateConfiguredPowerShell(runspace);
                var output = await InvokeScriptAsync(ps, ct).ConfigureAwait(false);
                return ProcessOutput(ps, output);
            }
            catch (PipelineStoppedException) when (ct.IsCancellationRequested)
            {
                _logger.Information("PowerShell health probe {Probe} canceled (PipelineStopped).", Name);
                return new ProbeResult(ProbeStatus.Degraded, "Canceled");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex is PipelineStoppedException)
                {
                    _logger.Warning(ex, "PowerShell health probe {Probe} pipeline stopped.", Name);
                    return new ProbeResult(ProbeStatus.Degraded, "Canceled");
                }
                _logger.Error(ex, "PowerShell health probe {Probe} failed.", Name);
                return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
            }
            finally
            {
                if (runspace is not null)
                {
                    try { pool.Release(runspace); }
                    catch { runspace.Dispose(); }
                }
            }
        }

        /// <summary>
        /// Creates and configures a PowerShell instance with the provided runspace and script.
        /// </summary>
        /// <param name="runspace">The runspace to use for the PowerShell instance.</param>
        /// <returns>A configured PowerShell instance.</returns>
        private PowerShell CreateConfiguredPowerShell(Runspace runspace)
        {
            var ps = PowerShell.Create();
            ps.Runspace = runspace;
            PowerShellExecutionHelpers.SetVariables(ps, _arguments, _logger);
            PowerShellExecutionHelpers.AddScript(ps, _script);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("PowerShellScriptProbe {Probe} invoking script length={Length}", Name, _script.Length);
            }
            return ps;
        }

        /// <summary>
        /// Invokes the PowerShell script asynchronously.
        /// </summary>
        /// <param name="ps">The PowerShell instance to use.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation, with a list of PSObject as the result.</returns>
        private async Task<IReadOnlyList<PSObject>> InvokeScriptAsync(PowerShell ps, CancellationToken ct)
        {
            var output = await PowerShellExecutionHelpers.InvokeAsync(ps, _logger, ct).ConfigureAwait(false);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("PowerShellScriptProbe {Probe} received {Count} output objects", Name, output.Count);
            }
            // Materialize to a List to satisfy IReadOnlyList contract and avoid invalid casts.
            return output.Count == 0 ? Array.Empty<PSObject>() : new List<PSObject>(output);
        }

        /// <summary>
        /// Processes the output from the PowerShell script.
        /// </summary>
        /// <param name="ps">The PowerShell instance used to invoke the script.</param>
        /// <param name="output">The output objects returned by the script.</param>
        /// <returns>A ProbeResult representing the outcome of the script execution.</returns>
        private ProbeResult ProcessOutput(PowerShell ps, IReadOnlyList<PSObject> output)
        {
            if (ps.HadErrors || ps.Streams.Error.Count > 0)
            {
                var errors = string.Join("; ", ps.Streams.Error.Select(static e => e.ToString()));
                ps.Streams.Error.Clear();
                return new ProbeResult(ProbeStatus.Unhealthy, errors);
            }

            for (var i = output.Count - 1; i >= 0; i--)
            {
                if (TryConvert(output[i], out var result))
                {
                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("PowerShellScriptProbe {Probe} converted output index={Index} status={Status}", Name, i, result.Status);
                    }
                    return result;
                }
            }
            return new ProbeResult(ProbeStatus.Unhealthy, "PowerShell probe produced no recognizable result.");
        }

        /// <summary>
        /// Tries to convert a PSObject to a ProbeResult.
        /// </summary>
        /// <param name="obj">The PSObject to convert.</param>
        /// <param name="result">The resulting ProbeResult.</param>
        /// <returns>True if the conversion was successful, false otherwise.</returns>
        private static bool TryConvert(PSObject obj, out ProbeResult result)
        {
            // Direct pass-through if already a ProbeResult
            if (TryUnwrapProbeResult(obj, out result))
            {
                return true;
            }

            // Try string-based conversion
            if (TryConvertFromString(obj, out result))
            {
                return true;
            }

            // Try property-based conversion
            if (TryConvertFromProperties(obj, out result))
            {
                return true;
            }

            result = default!;
            return false;
        }

        // Handles string-based conversion scenario
        private static bool TryConvertFromString(PSObject obj, out ProbeResult result)
        {
            if (TryGetStatus(obj, out var status, out var descriptionWhenString, out var statusTextIsRaw))
            {
                if (statusTextIsRaw)
                {
                    // We interpreted the entire string object as both status + description
                    result = new ProbeResult(status, descriptionWhenString);
                    return true;
                }
            }
            result = default!;
            return false;
        }

        // Handles property-based conversion scenario
        private static bool TryConvertFromProperties(PSObject obj, out ProbeResult result)
        {
            if (TryGetStatus(obj, out var status, out var descriptionWhenString, out var statusTextIsRaw))
            {
                if (!statusTextIsRaw)
                {
                    var description = descriptionWhenString ?? GetDescription(obj);
                    var data = GetDataDictionary(obj);
                    result = new ProbeResult(status, description, data);
                    return true;
                }
            }
            result = default!;
            return false;
        }
        /// <summary>
        /// Tries to unwrap a PSObject that directly wraps a ProbeResult.
        /// </summary>
        /// <param name="obj">The PSObject to unwrap.</param>
        /// <param name="result">The unwrapped ProbeResult.</param>
        /// <returns>True if the unwrapping was successful, false otherwise.</returns>
        private static bool TryUnwrapProbeResult(PSObject obj, out ProbeResult result)
        {
            if (obj.BaseObject is ProbeResult pr)
            {
                result = pr;
                return true;
            }
            result = default!;
            return false;
        }

        /// <summary>
        /// Parses the status from a PSObject.
        /// </summary>
        /// <param name="obj">The PSObject to parse.</param>
        /// <param name="status">The parsed ProbeStatus.</param>
        /// <param name="descriptionOrRaw">The description or raw status string.</param>
        /// <param name="isRawString">True if the status was a raw string, false otherwise.</param>
        /// <returns>True if the parsing was successful, false otherwise.</returns>
        private static bool TryGetStatus(PSObject obj, out ProbeStatus status, out string? descriptionOrRaw, out bool isRawString)
        {
            var statusValue = obj.Properties["status"]?.Value ?? obj.Properties["Status"]?.Value;
            if (statusValue is null)
            {
                if (obj.BaseObject is string statusText && TryParseStatus(statusText, out var parsedFromText))
                {
                    status = parsedFromText;
                    descriptionOrRaw = statusText;
                    isRawString = true;
                    return true;
                }
                status = default;
                descriptionOrRaw = null;
                isRawString = false;
                return false;
            }

            status = TryParseStatus(statusValue.ToString(), out var parsed)
                ? parsed
                : ProbeStatus.Unhealthy;
            descriptionOrRaw = null; // description will be resolved separately
            isRawString = false;
            return true;
        }
        /// <summary>
        /// Gets the description from a PSObject.
        /// </summary>
        /// <param name="obj">The PSObject to extract the description from.</param>
        /// <returns>The description string, or null if not found.</returns>
        private static string? GetDescription(PSObject obj)
            => obj.Properties["description"]?.Value?.ToString() ?? obj.Properties["Description"]?.Value?.ToString();

        /// <summary>
        /// Gets the data dictionary from a PSObject.
        /// </summary>
        /// <param name="obj">The PSObject to extract the data from.</param>
        /// <returns>The data dictionary, or null if not found or empty.</returns>
        private static IReadOnlyDictionary<string, object>? GetDataDictionary(PSObject obj)
        {
            var dataProperty = obj.Properties["data"] ?? obj.Properties["Data"];
            if (dataProperty?.Value is not IDictionary dictionary || dictionary.Count == 0)
            {
                return null;
            }
            // Normalize values and filter out nulls to satisfy ProbeResult Data (non-null object values)
            var temp = new Dictionary<string, object?>(dictionary.Count, StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is null || entry.Value is null)
                {
                    continue;
                }
                var key = entry.Key.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }
                var normalized = NormalizePsValue(entry.Value);
                if (normalized is not null)
                {
                    temp[key] = normalized;
                }
            }
            if (temp.Count == 0)
            {
                return null;
            }
            var final = new Dictionary<string, object>(temp.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in temp)
            {
                final[kv.Key] = kv.Value!; // safe due to null filter
            }
            return final;
        }

        /// <summary>
        /// Normalizes a value that may originate from PowerShell so JSON serialization stays lean.
        /// Strategy:
        /// 1. Unwrap <c>PSObject</c> instances to their <c>BaseObject</c> (recursively).
        /// 2. Preserve primitives directly.
        /// 3. For dictionaries (<c>IDictionary</c>) create a new <c>Dictionary&lt;string, object?&gt;</c> with normalized children.
        /// 4. For enumerables (<c>IEnumerable</c>) that are not <c>string</c>, return a <c>List&lt;object?&gt;</c> of normalized items.
        /// 5. Fallback: return the original object (POCOs are handled by the default serializer).
        /// A small max recursion depth (8) prevents pathological graphs from ballooning.
        /// </summary>
        private static object? NormalizePsValue(object? value, int depth = 0)
        {
            if (value is null)
            {
                return null;
            }

            // Hard depth stop to avoid pathological/custom objects exploding recursively
            if (depth > 8)
            {
                return value is PSObject pso ? pso.ToString() : value.ToString();
            }

            // Unwrap PSObject layers
            if (value is PSObject psObj)
            {
                // Direct scalar unwrap
                var baseObj = psObj.BaseObject;
                return baseObj is null || ReferenceEquals(baseObj, psObj)
                    ? psObj.ToString()
                    : NormalizePsValue(baseObj, depth + 1);
            }

            // Treat PSPrimitive-like wrappers (PowerShell sometimes wraps ints/doubles)
            if (value is IFormattable && value.GetType().IsPrimitive)
            {
                return value; // JSON serializer handles primitives directly
            }

            // IDictionary → normalize each entry
            if (value is IDictionary rawDict)
            {
                var result = new Dictionary<string, object?>(rawDict.Count);
                foreach (DictionaryEntry de in rawDict)
                {
                    if (de.Key is null)
                    {
                        continue;
                    }
                    var k = de.Key.ToString();
                    if (string.IsNullOrWhiteSpace(k))
                    {
                        continue;
                    }
                    result[k] = NormalizePsValue(de.Value, depth + 1);
                }
                return result;
            }

            // IEnumerable (but not string) → list of normalized children
            if (value is IEnumerable enumerable and not string)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(NormalizePsValue(item, depth + 1));
                }
                return list;
            }

            // Leave everything else untouched (e.g., plain POCOs / numbers / DateTimes)
            return value;
        }

        /// <summary>
        /// Parses a status string into a ProbeStatus enum.
        /// </summary>
        /// <param name="value">The status string to parse.</param>
        /// <param name="status">The resulting ProbeStatus enum.</param>
        /// <returns>True if the parsing was successful, false otherwise.</returns>
        private static bool TryParseStatus(string? value, out ProbeStatus status)
        {
            if (Enum.TryParse(value, ignoreCase: true, out status))
            {
                return true;
            }


            switch (value?.ToLowerInvariant())
            {
                case STATUS_OK:
                case STATUS_HEALTHY:
                    status = ProbeStatus.Healthy;
                    return true;
                case STATUS_WARN:
                case STATUS_WARNING:
                case STATUS_DEGRADED:
                    status = ProbeStatus.Degraded;
                    return true;
                case STATUS_FAIL:
                case STATUS_FAILED:
                case STATUS_UNHEALTHY:
                    status = ProbeStatus.Unhealthy;
                    return true;
                default:
                    status = ProbeStatus.Unhealthy;
                    return false;
            }
        }
    }

    /// <summary>
    /// A health probe implemented via a C# script.
    /// </summary>
    /// <param name="name">The name of the probe.</param>
    /// <param name="tags">The tags associated with the probe.</param>
    /// <param name="runner">The script runner to execute the probe.</param>
    /// <param name="locals">The local variables for the script.</param>
    /// <param name="logger">The logger to use for logging.</param>
    private sealed class CSharpScriptProbe(
        string name,
        IEnumerable<string>? tags,
        ScriptRunner<ProbeResult> runner,
        IReadOnlyDictionary<string, object?>? locals,
        SerilogLogger logger) : IProbe
    {
        /// <summary>
        /// The script runner to execute the probe.
        /// </summary>
        private readonly ScriptRunner<ProbeResult> _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        /// <summary>
        /// The local variables for the script.
        /// </summary>
        private readonly IReadOnlyDictionary<string, object?>? _locals = locals;
        /// <summary>
        /// Gets the name of the probe.
        /// </summary>
        public string Name { get; } = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Probe name cannot be null or empty.", nameof(name))
            : name;
        /// <summary>
        /// Gets the tags associated with the probe.
        /// </summary>
        public string[] Tags { get; } = tags is null
            ? []
            : [.. tags.Where(static t => !string.IsNullOrWhiteSpace(t))
                      .Select(static t => t.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc />
        public SerilogLogger Logger { get; init; } = logger;

        /// <summary>
        /// Executes the C# script and returns the resulting ProbeResult.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation, with a ProbeResult as the result.</returns>
        public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
        {
            var globals = _locals is { Count: > 0 }
                ? new CsGlobals(SharedStateStore.Snapshot(), _locals)
                : new CsGlobals(SharedStateStore.Snapshot());
            try
            {
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("CSharpScriptProbe {Probe} executing", Name);
                }
                var result = await _runner(globals, ct).ConfigureAwait(false)
                    ?? new ProbeResult(ProbeStatus.Unhealthy, "Script returned null result");
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("CSharpScriptProbe {Probe} completed status={Status}", Name, result.Status);
                }
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (RoslynCompilationErrorException ex)
            {
                Logger.Error(ex, "C# health probe {Probe} failed to execute.", Name);
                return new ProbeResult(ProbeStatus.Unhealthy, string.Join("; ", ex.Diagnostics.Select(static d => d.GetMessage())));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "C# health probe {Probe} threw an exception.", Name);
                return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// A health probe implemented via a VB.NET script.
    /// </summary>
    /// <param name="name">The name of the probe.</param>
    /// <param name="tags">The tags associated with the probe.</param>
    /// <param name="runner">The script runner to execute the probe.</param>
    /// <param name="locals">The local variables for the script.</param>
    /// <param name="logger">The logger to use for logging.</param>
    private sealed class VbScriptProbe(
        string name,
        IEnumerable<string>? tags,
        Func<CsGlobals, Task<ProbeResult>> runner,
        IReadOnlyDictionary<string, object?>? locals,
        SerilogLogger logger) : IProbe
    {
        /// <summary>
        /// The script runner to execute the probe.
        /// </summary>
        private readonly Func<CsGlobals, Task<ProbeResult>> _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        private readonly IReadOnlyDictionary<string, object?>? _locals = locals;
        /// <summary>
        /// The logger to use for logging.
        /// </summary>
        private readonly SerilogLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        /// <summary>
        /// Gets the name of the probe.
        /// </summary>
        public string Name { get; } = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Probe name cannot be null or empty.", nameof(name))
            : name;

        /// <summary>
        /// Gets the tags associated with the probe.
        /// </summary>
        public string[] Tags { get; } = tags is null
            ? []
            : [.. tags.Where(static t => !string.IsNullOrWhiteSpace(t))
                      .Select(static t => t.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc />
        public SerilogLogger Logger { get; init; } = logger;

        /// <summary>
        /// Executes the VB.NET script and returns the resulting ProbeResult.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation, with a ProbeResult as the result.</returns>
        public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
        {
            var globals = _locals is { Count: > 0 }
                ? new CsGlobals(SharedStateStore.Snapshot(), _locals)
                : new CsGlobals(SharedStateStore.Snapshot());
            try
            {
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("VbScriptProbe {Probe} executing", Name);
                }
                var result = await _runner(globals).WaitAsync(ct).ConfigureAwait(false)
                    ?? new ProbeResult(ProbeStatus.Unhealthy, "Script returned null result");
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("VbScriptProbe {Probe} completed status={Status}", Name, result.Status);
                }
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "VB.NET health probe {Probe} failed.", Name);
                return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
            }
        }
    }
}
