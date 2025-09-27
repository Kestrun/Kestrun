using Kestrun.Health;
using Kestrun.Models;
using Xunit;

namespace KestrunTests.Health;

public class HealthEndpointFormattingTests
{
    private static async Task<string> SerializeReportAsync(bool compress)
    {
        var report = await HealthProbeRunner.RunAsync(
            probes: [],
            tagFilter: [],
            perProbeTimeout: TimeSpan.FromMilliseconds(100),
            maxDegreeOfParallelism: 0,
            logger: Serilog.Log.Logger,
            ct: CancellationToken.None);

        var req = TestRequestFactory.Create(headers: new Dictionary<string, string> { { "Accept", "application/json" } });
        var res = new KestrunResponse(req);
        await res.WriteJsonResponseAsync(report, depth: 10, compress: compress, statusCode: 200, contentType: "application/json");
        return Assert.IsType<string>(res.Body);
    }

    [Fact]
    public async Task HealthEndpoint_Default_Is_HumanReadableJson()
    {
        var body = await SerializeReportAsync(compress: false);
        Assert.Contains("\n", body); // pretty
        // Status enum value may be omitted due to DefaultValueHandling.Ignore when Healthy (default 0), so assert on statusText instead
        Assert.Contains("\"statusText\"", body);
    }

    [Fact]
    public async Task HealthEndpoint_Compress_Produces_CompactJson()
    {
        var body = await SerializeReportAsync(compress: true);
        Assert.DoesNotContain("\n", body); // compact
        Assert.Contains("\"statusText\"", body);
    }
}
