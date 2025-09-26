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
