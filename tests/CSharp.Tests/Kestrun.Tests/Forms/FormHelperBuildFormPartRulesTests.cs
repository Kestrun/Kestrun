using Kestrun.Forms;
using Xunit;

namespace KestrunTests.Forms;

public sealed class FormHelperBuildFormPartRulesTests
{
    private sealed class SimplePayload
    {
        [KrPart(ContentTypes = ["text/plain"], Required = true, AllowMultiple = false)]
        public string? Note { get; set; }
    }

    private sealed class InnerPayload
    {
        [KrPart(ContentTypes = ["text/plain"], Required = true)]
        public string? Text { get; set; }
    }

    private sealed class OuterPayload
    {
        [KrPart(ContentTypes = ["multipart/mixed"], Required = true)]
        public InnerPayload? Nested { get; set; }
    }

    [Fact]
    [Trait("Category", "Forms")]
    public void BuildFormPartRulesFromType_SimplePart_ProducesExpectedRule()
    {
        var rules = FormHelper.BuildFormPartRulesFromType(typeof(SimplePayload));

        var note = Assert.Single(rules, r => string.Equals(r.Name, nameof(SimplePayload.Note), StringComparison.Ordinal));
        Assert.True(note.Required);
        Assert.False(note.AllowMultiple);
        Assert.Contains("text/plain", note.AllowedContentTypes);
        Assert.Null(note.Scope);
        Assert.Empty(note.NestedRules);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public void BuildFormPartRulesFromType_MultipartContainer_FlattensAndScopesNestedRules()
    {
        var rules = FormHelper.BuildFormPartRulesFromType(typeof(OuterPayload));

        var container = Assert.Single(rules, r => string.Equals(r.Name, nameof(InnerPayload), StringComparison.Ordinal));

        Assert.True(container.Required);
        Assert.Contains("multipart/mixed", container.AllowedContentTypes);

        var nested = Assert.Single(rules, r => string.Equals(r.Name, nameof(InnerPayload.Text), StringComparison.Ordinal) &&
                                              string.Equals(r.Scope, container.Name, StringComparison.Ordinal));

        Assert.Contains(container.NestedRules, r => ReferenceEquals(r, nested));
    }
}
