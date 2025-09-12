using Kestrun.Models;
using Xunit;

namespace KestrunTests.Models;

public class ContentDispositionOptionsTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void Defaults_To_NoContentDisposition()
    {
        var opts = new ContentDispositionOptions();
        Assert.Equal(ContentDispositionType.NoContentDisposition, opts.Type);
        Assert.Null(opts.FileName);
        Assert.Equal(string.Empty, opts.ToString());
    }

    [Theory]
    [InlineData(ContentDispositionType.Attachment, "attachment")]
    [InlineData(ContentDispositionType.Inline, "inline")]
    public void Without_FileName_Renders_Disposition_Only(ContentDispositionType type, string expected)
    {
        var opts = new ContentDispositionOptions { Type = type, FileName = null };
        Assert.Equal(expected, opts.ToString());
    }

    [Fact]
    [Trait("Category", "Models")]
    public void With_FileName_Encodes_And_Quotes()
    {
        var opts = new ContentDispositionOptions
        {
            Type = ContentDispositionType.Attachment,
            FileName = "report 2025-08-20 & notes.txt"
        };

        var rendered = opts.ToString();

        Assert.StartsWith("attachment; filename=\"", rendered);
        Assert.EndsWith("\"", rendered);
        // Space and ampersand should be URL-encoded
        Assert.Contains("report+2025-08-20+%26+notes.txt", rendered);
    }
}
