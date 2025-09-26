using System.Collections.Concurrent;
using System.Diagnostics;
using SerilogLogger = Serilog.ILogger;

namespace Kestrun.Health;

/// <summary>
/// Executes registered <see cref="IProbe"/> instances and aggregates their results into a single report.
/// </summary>
internal static class HealthProbeRunner
{
    /// <summary>
    /// Executes the provided probes and builds an aggregated <see cref="HealthReport"/>.
    /// </summary>
    /// <param name="probes">The probes to execute.</param>
    /// <param name="tagFilter">Optional tag filter (case-insensitive). When provided, only probes that advertise at least one matching tag are executed.</param>
    /// <param name="perProbeTimeout">Maximum execution time per probe. Specify <see cref="TimeSpan.Zero"/> to disable the timeout.</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of probes executed concurrently. Values less than one disable throttling.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    /// <param name="ct">Cancellation token tied to the HTTP request.</param>
    /// <returns>The aggregated health report.</returns>
    public static async Task<HealthReport> RunAsync(
        IReadOnlyList<IProbe> probes,
        IReadOnlyList<string> tagFilter,
        TimeSpan perProbeTimeout,
    int maxDegreeOfParallelism,
    SerilogLogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(tagFilter);
        ArgumentNullException.ThrowIfNull(logger);

        string[] normalizedTags = tagFilter.Count == 0
            ? []
            : [.. tagFilter.Select(static t => t.Trim())
                           .Where(static t => !string.IsNullOrWhiteSpace(t))
                           .Distinct(StringComparer.OrdinalIgnoreCase)];

        var selected = normalizedTags.Length == 0
            ? probes
            : [.. probes.Where(p => p.Tags.Any(tag => normalizedTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))];

        if (selected.Count == 0)
        {
            return new HealthReport(
                ProbeStatus.Healthy,
                "healthy",
                DateTimeOffset.UtcNow,
                [],
                new HealthSummary(0, 0, 0, 0),
                normalizedTags);
        }

        var entries = new ConcurrentBag<HealthProbeEntry>();
        using var throttle = maxDegreeOfParallelism > 0 ? new SemaphoreSlim(maxDegreeOfParallelism) : null;

        var tasks = selected
            .Select(probe => ExecuteProbeAsync(probe, perProbeTimeout, throttle, logger, entries, ct))
            .ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var ordered = entries.OrderBy(static e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var summary = new HealthSummary(
            ordered.Length,
            ordered.Count(static e => e.Status == ProbeStatus.Healthy),
            ordered.Count(static e => e.Status == ProbeStatus.Degraded),
            ordered.Count(static e => e.Status == ProbeStatus.Unhealthy));

        var overall = ordered.Select(static e => e.Status).DefaultIfEmpty(ProbeStatus.Healthy).Max();
        return new HealthReport(
            overall,
            overall.ToString().ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            ordered,
            summary,
            normalizedTags);
    }

    private static async Task ExecuteProbeAsync(
        IProbe probe,
        TimeSpan perProbeTimeout,
    SemaphoreSlim? throttle,
    SerilogLogger logger,
        ConcurrentBag<HealthProbeEntry> sink,
        CancellationToken ct)
    {
        if (throttle is not null)
        {
            await throttle.WaitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (perProbeTimeout > TimeSpan.Zero)
            {
                linkedCts.CancelAfter(perProbeTimeout);
            }

            var sw = Stopwatch.StartNew();
            ProbeResult? result = null;
            string? error = null;

            try
            {
                result = await probe.CheckAsync(linkedCts.Token).ConfigureAwait(false)
                      ?? new ProbeResult(ProbeStatus.Unhealthy, "Probe returned null result");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && linkedCts.IsCancellationRequested)
            {
                logger.Warning("Health probe {Probe} timed out after {Timeout}.", probe.Name, perProbeTimeout);
                result = new ProbeResult(ProbeStatus.Unhealthy, $"Timed out after {perProbeTimeout.TotalSeconds:N1}s");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Health probe {Probe} threw an exception.", probe.Name);
                error = ex.Message;
                result = new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
            }
            finally
            {
                sw.Stop();
            }

            sink.Add(new HealthProbeEntry(
                probe.Name,
                probe.Tags ?? [],
                result.Status,
                result.Status.ToString().ToLowerInvariant(),
                result.Description,
                result.Data,
                sw.Elapsed,
                error));
        }
        finally
        {
            _ = throttle?.Release();
        }
    }
}

/// <summary>
/// Represents an aggregated health report produced by <see cref="HealthProbeRunner"/>.
/// </summary>
/// <param name="Status">Overall status calculated from all probe results.</param>
/// <param name="StatusText">Lowercase textual representation of <see cref="Status"/>.</param>
/// <param name="GeneratedAt">UTC timestamp the report was produced.</param>
/// <param name="Probes">Detailed results for each executed probe.</param>
/// <param name="Summary">Aggregated counts per <see cref="ProbeStatus"/>.</param>
/// <param name="AppliedTags">Tag filters applied to this report.</param>
public sealed record HealthReport(
    ProbeStatus Status,
    string StatusText,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<HealthProbeEntry> Probes,
    HealthSummary Summary,
    IReadOnlyList<string> AppliedTags);

/// <summary>
/// Represents the result of a single probe execution.
/// </summary>
/// <param name="Name">Probe name.</param>
/// <param name="Tags">Probe tags.</param>
/// <param name="Status">Probe status.</param>
/// <param name="StatusText">Lowercase textual status.</param>
/// <param name="Description">Optional description supplied by the probe.</param>
/// <param name="Data">Optional supplemental data.</param>
/// <param name="Duration">Probe execution duration.</param>
/// <param name="Error">Optional captured error details.</param>
public sealed record HealthProbeEntry(
    string Name,
    string[] Tags,
    ProbeStatus Status,
    string StatusText,
    string? Description,
    IReadOnlyDictionary<string, object>? Data,
    TimeSpan Duration,
    string? Error);

/// <summary>
/// Summary counts of probe results grouped by <see cref="ProbeStatus"/>.
/// </summary>
/// <param name="Total">Total number of executed probes.</param>
/// <param name="Healthy">Number of probes reporting <see cref="ProbeStatus.Healthy"/>.</param>
/// <param name="Degraded">Number of probes reporting <see cref="ProbeStatus.Degraded"/>.</param>
/// <param name="Unhealthy">Number of probes reporting <see cref="ProbeStatus.Unhealthy"/>.</param>
public sealed record HealthSummary(int Total, int Healthy, int Degraded, int Unhealthy);
