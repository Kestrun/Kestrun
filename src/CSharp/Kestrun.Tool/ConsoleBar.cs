namespace Kestrun.Tool;

/// <summary>
/// Writes in-place progress updates for module operations.
/// </summary>
internal sealed class ConsoleProgressBar(string label, long? total, Func<long, long?, string>? detailFormatter) : IDisposable
{
    private const int ProgressBarWidth = 28;
    private const int MinimumBarWidth = 8;
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

        var line = BuildRenderedLine(detail, percent, GetUsableConsoleWidth());

        var paddedLength = Math.Min(_lastRenderedLength, Math.Max(0, GetUsableConsoleWidth()));
        if (line.Length < paddedLength)
        {
            line = line.PadRight(paddedLength);
        }

        _lastRenderedLength = line.Length;
        _hasRendered = true;

        Console.Write('\r');
        Console.Write(line);
    }

    private string BuildRenderedLine(string detail, int percent, int usableWidth)
    {
        if (percent < 0)
        {
            return FitSimpleLineToWidth(_label, detail, usableWidth);
        }

        var percentText = $"{percent,3}%";
        var barWidth = ProgressBarWidth;
        var line = $"{_label} {BuildBarWithWidth(percent, barWidth)} {percentText} {detail}";
        if (line.Length <= usableWidth)
        {
            return line;
        }

        var reservedWithoutDetail = _label.Length + 1 + 2 + barWidth + 1 + percentText.Length + 1;
        var maxDetailLength = usableWidth - reservedWithoutDetail;
        if (maxDetailLength < 0)
        {
            var availableBarWidth = usableWidth - (_label.Length + 1 + 2 + 1 + percentText.Length + 1);
            barWidth = Math.Clamp(availableBarWidth, MinimumBarWidth, ProgressBarWidth);
            reservedWithoutDetail = _label.Length + 1 + 2 + barWidth + 1 + percentText.Length + 1;
            maxDetailLength = usableWidth - reservedWithoutDetail;
        }

        var shortenedDetail = TrimProgressText(detail, maxDetailLength);
        line = $"{_label} {BuildBarWithWidth(percent, barWidth)} {percentText} {shortenedDetail}".TrimEnd();
        if (line.Length <= usableWidth)
        {
            return line;
        }

        return line[..Math.Max(0, usableWidth)];
    }

    private static string FitSimpleLineToWidth(string label, string detail, int usableWidth)
    {
        var line = $"{label} {detail}".TrimEnd();
        if (line.Length <= usableWidth)
        {
            return line;
        }

        var maxDetailLength = usableWidth - label.Length - 1;
        if (maxDetailLength <= 0)
        {
            return label[..Math.Min(label.Length, Math.Max(0, usableWidth))];
        }

        return $"{label} {TrimProgressText(detail, maxDetailLength)}".TrimEnd();
    }

    private static string TrimProgressText(string text, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 3)
        {
            return text[..maxLength];
        }

        return $"{text[..(maxLength - 3)]}...";
    }

    private static int GetUsableConsoleWidth()
    {
        try
        {
            var width = Console.WindowWidth;
            if (width <= 1)
            {
                width = Console.BufferWidth;
            }

            return Math.Max(40, width - 1);
        }
        catch
        {
            return 120;
        }
    }

    private static string BuildBar(int percent)
        => BuildBarWithWidth(percent, ProgressBarWidth);

    private static string BuildBarWithWidth(int percent, int width)
    {
        var normalizedWidth = Math.Max(1, width);
        var filled = (int)Math.Round(percent / 100d * normalizedWidth, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, normalizedWidth);
        return $"[{new string('#', filled)}{new string('-', normalizedWidth - filled)}]";
    }
}

