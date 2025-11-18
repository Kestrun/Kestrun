using Kestrun.Health;
using Xunit;

namespace KestrunTests.Health;

public class HealthReportTextFormatterTests
{
    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithNullReport_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => HealthReportTextFormatter.Format(null!));

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithHealthyReport_ReturnsFormattedText()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", Array.Empty<string>(), ProbeStatus.Healthy, "healthy",
                "All systems operational", null, TimeSpan.FromMilliseconds(50), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, Array.Empty<string>());

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Status: healthy", result);
        Assert.Contains("test-probe", result);
        Assert.Contains("All systems operational", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithDegradedReport_ReturnsFormattedText()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["freePercent"] = 8.5 };
        var probes = new List<HealthProbeEntry>
        {
            new("disk-probe", Array.Empty<string>(), ProbeStatus.Degraded, "degraded",
                "Low disk space", data, TimeSpan.FromMilliseconds(100), null)
        };
        var summary = new HealthSummary(1, 0, 1, 0);
        var report = new HealthReport(ProbeStatus.Degraded, "degraded", DateTimeOffset.UtcNow, probes, summary, Array.Empty<string>());

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("Status: degraded", result);
        Assert.Contains("disk-probe", result);
        Assert.Contains("degraded", result);
        Assert.Contains("Low disk space", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithUnhealthyReport_IncludesErrorMessage()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("failing-probe", [], ProbeStatus.Unhealthy, "unhealthy",
                "Service unavailable", null, TimeSpan.FromMilliseconds(10), "Connection timeout")
        };
        var summary = new HealthSummary(1, 0, 0, 1);
        var report = new HealthReport(ProbeStatus.Unhealthy, "unhealthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("Status: unhealthy", result);
        Assert.Contains("failing-probe", result);
        Assert.Contains("Service unavailable", result);
        Assert.Contains("Connection timeout", result);
        Assert.Contains("error=", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithProbeData_IncludesDataByDefault()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["freeBytes"] = 1024000000L,
            ["totalBytes"] = 10240000000L,
            ["freePercent"] = 10.0
        };
        var probes = new List<HealthProbeEntry>
        {
            new("disk-probe", [], ProbeStatus.Healthy, "healthy",
                "Sufficient space", data, TimeSpan.FromMilliseconds(50), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report, includeData: true);

        // Assert
        Assert.Contains("freeBytes=", result);
        Assert.Contains("totalBytes=", result);
        Assert.Contains("freePercent=", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithIncludeDataFalse_ExcludesProbeData()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["freeBytes"] = 1024000000L,
            ["totalBytes"] = 10240000000L
        };
        var probes = new List<HealthProbeEntry>
        {
            new("disk-probe", [], ProbeStatus.Healthy, "healthy",
                "Sufficient space", data, TimeSpan.FromMilliseconds(50), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report, includeData: false);

        // Assert
        Assert.DoesNotContain("freeBytes=", result);
        Assert.DoesNotContain("totalBytes=", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithMultipleProbes_IncludesAllProbes()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("probe1", [], ProbeStatus.Healthy, "healthy", "OK", null, TimeSpan.FromMilliseconds(10), null),
            new("probe2", [], ProbeStatus.Degraded, "degraded", "Warning", null, TimeSpan.FromMilliseconds(50), null),
            new("probe3", [], ProbeStatus.Unhealthy, "unhealthy", "Failed", null, TimeSpan.FromMilliseconds(100), "Error details")
        };
        var summary = new HealthSummary(3, 1, 1, 1);
        var report = new HealthReport(ProbeStatus.Unhealthy, "unhealthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("probe1", result);
        Assert.Contains("probe2", result);
        Assert.Contains("probe3", result);
        Assert.Contains("total=3", result);
        Assert.Contains("healthy=1", result);
        Assert.Contains("degraded=1", result);
        Assert.Contains("unhealthy=1", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithAppliedTags_IncludesTags()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", null, TimeSpan.FromMilliseconds(10), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var tags = new[] { "live", "ready" };
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, tags);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("Tags: live,ready", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithNoTags_DoesNotIncludeTagsLine()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", null, TimeSpan.FromMilliseconds(10), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.DoesNotContain("Tags:", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithStringData_QuotesValue()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["path"] = "/var/data",
            ["driveName"] = "C:\\"
        };
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", data, TimeSpan.FromMilliseconds(10), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("path=\"/var/data\"", result);
        Assert.Contains("driveName=", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithNullData_ShowsNullPlaceholder()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["optionalValue"] = null!
        };
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", data, TimeSpan.FromMilliseconds(10), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("optionalValue=<null>", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithDateTimeData_FormatsAsIso8601()
    {
        // Arrange
        var testDate = new DateTime(2025, 11, 17, 10, 30, 45, DateTimeKind.Utc);
        var data = new Dictionary<string, object>
        {
            ["timestamp"] = testDate
        };
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", data, TimeSpan.FromMilliseconds(10), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("timestamp=", result);
        Assert.Contains("2025-11-17T10:30:45", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithDurationLessThan1Ms_ShowsLessThan1Ms()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("fast-probe", [], ProbeStatus.Healthy, "healthy", "OK", null, TimeSpan.FromTicks(100), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("duration=<1ms", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithDurationInMilliseconds_ShowsMs()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", null, TimeSpan.FromMilliseconds(123), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("duration=123ms", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithDurationInSeconds_ShowsSeconds()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "OK", null, TimeSpan.FromSeconds(2.5), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("duration=2.5s", result);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Format_WithSpecialCharactersInDescription_EscapesProperly()
    {
        // Arrange
        var probes = new List<HealthProbeEntry>
        {
            new("test-probe", [], ProbeStatus.Healthy, "healthy", "Status: \"OK\"\nLine 2", null, TimeSpan.FromMilliseconds(10), null)
        };
        var summary = new HealthSummary(1, 1, 0, 0);
        var report = new HealthReport(ProbeStatus.Healthy, "healthy", DateTimeOffset.UtcNow, probes, summary, []);

        // Act
        var result = HealthReportTextFormatter.Format(report);

        // Assert
        Assert.Contains("\\\"OK\\\"", result); // Quotes should be escaped
        Assert.Contains("\\n", result); // Newlines should be escaped
    }
}
