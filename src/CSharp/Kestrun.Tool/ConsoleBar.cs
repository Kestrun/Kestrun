namespace Kestrun.Tool;

/// <summary>
/// Writes in-place progress updates for module operations.
/// </summary>
internal sealed class ConsoleProgressBar(string label, long? total, Func<long, long?, string>? detailFormatter) : IDisposable
{
    private const int ProgressBarWidth = 28;
    private static readonly TimeSpan RenderThrottle = TimeSpan.FromMilliseconds(80);

    private readonly string _label = label;
    private readonly long? _total = total.HasValue && total.Value > 0 ? total : null;
    private readonly Func<long, long?, string>? _detailFormatter = detailFormatter;
    private readonly bool _enabled = !Console.IsOutputRedirected;
    private int _lastRenderedLength;
    private int _lastPercent = -1;
    private long _lastValue = -1;
    private DateTime _lastRenderUtc = DateTime.MinValue;
    private bool _hasRendered;
    private bool _isComplete;

    public void Report(long value, bool force = false)
    {
        if (!_enabled || _isComplete)
        {
            return;
        }

        var sanitizedValue = Math.Max(0, value);
        var percent = GetPercent(sanitizedValue);
        var now = DateTime.UtcNow;

        if (!force
            && sanitizedValue == _lastValue
            && percent == _lastPercent)
        {
            return;
        }

        if (!force
            && percent == _lastPercent
            && now - _lastRenderUtc < RenderThrottle)
        {
            return;
        }

        _lastValue = sanitizedValue;
        _lastPercent = percent;
        _lastRenderUtc = now;
        Render(sanitizedValue, percent);
    }

    public void Complete(long value)
    {
        if (!_enabled || _isComplete)
        {
            return;
        }

        var completionValue = _total ?? Math.Max(0, value);
        Report(completionValue, force: true);
        Console.WriteLine();
        _isComplete = true;
    }

    public void Dispose()
    {
        if (_enabled && _hasRendered && !_isComplete)
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Returns the percentage (0-100) for the given value based on the total, or -1 if percentage cannot be calculated.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <returns>The percentage (0-100) or -1 if percentage cannot be calculated.</returns>
    private int GetPercent(long value)
    {
        if (!_total.HasValue || _total.Value <= 0)
        {
            return -1;
        }
        // Use long math to avoid overflow, then clamp to 0-100.
        return (int)Math.Clamp(value * 100L / _total.Value, 0, 100);
    }

    private void Render(long value, int percent)
    {
        var detail = _detailFormatter is null
            ? _total.HasValue
                ? $"{value}/{_total.Value}"
                : value.ToString()
            : _detailFormatter(value, _total);

        var line = percent >= 0
            ? $"{_label} {BuildBar(percent)} {percent,3}% {detail}"
            : $"{_label} {detail}";

        if (line.Length < _lastRenderedLength)
        {
            line = line.PadRight(_lastRenderedLength);
        }

        _lastRenderedLength = line.Length;
        _hasRendered = true;

        Console.Write('\r');
        Console.Write(line);
    }

    private static string BuildBar(int percent)
    {
        var filled = (int)Math.Round(percent / 100d * ProgressBarWidth, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, ProgressBarWidth);
        return $"[{new string('#', filled)}{new string('-', ProgressBarWidth - filled)}]";
    }
}

