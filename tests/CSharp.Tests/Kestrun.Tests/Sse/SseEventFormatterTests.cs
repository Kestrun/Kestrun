using Xunit;

namespace KestrunTests.Sse;

public class SseEventFormatterTests
{
    [Trait("Category", "Sse")]
    [Fact]
    public void Format_IncludesRetryIdEventAndData_AndEndsWithBlankLine()
    {
        var payload = Kestrun.Sse.SseEventFormatter.Format(
            eventName: "tick",
            data: "hello",
            id: "1",
            retryMs: 2000);

        Assert.Equal(
            "retry: 2000\n" +
            "id: 1\n" +
            "event: tick\n" +
            "data: hello\n" +
            "\n",
            payload);
    }

    [Trait("Category", "Sse")]
    [Fact]
    public void Format_OmitsWhitespaceEventAndId()
    {
        var payload = Kestrun.Sse.SseEventFormatter.Format(
            eventName: " ",
            data: "one\ntwo\r\nthree",
            id: "\t",
            retryMs: null);

        Assert.Equal(
            "data: one\n" +
            "data: two\n" +
            "data: three\n" +
            "\n",
            payload);
        Assert.DoesNotContain("event:", payload);
        Assert.DoesNotContain("id:", payload);
        Assert.DoesNotContain("retry:", payload);
    }

    [Trait("Category", "Sse")]
    [Fact]
    public void Format_WhenDataIsEmpty_ReturnsOnlyTerminator()
    {
        var payload = Kestrun.Sse.SseEventFormatter.Format(eventName: null, data: string.Empty);

        Assert.Equal("\n", payload);
    }

    [Trait("Category", "Sse")]
    [Fact]
    public void FormatComment_UsesCommentWireFormat()
    {
        var payload = Kestrun.Sse.SseEventFormatter.FormatComment("keep-alive");
        Assert.Equal(": keep-alive\n\n", payload);
    }
}
