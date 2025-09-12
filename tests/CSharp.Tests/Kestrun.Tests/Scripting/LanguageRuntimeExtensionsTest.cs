using Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Kestrun.Scripting;

namespace KestrunTests.Scripting;

public class LanguageRuntimeExtensionsTest
{
    [Fact]
    [Trait("Category", "Scripting")]
    public void ScriptLanguageAttribute_SetsLanguageProperty()
    {
        var attr = new ScriptLanguageAttribute(ScriptLanguage.CSharp);
        Assert.Equal(ScriptLanguage.CSharp, attr.Language);
    }
    // Helper classes for testing
    private class TestEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<object> Metadata { get; } = [];
        public void Add(Action<EndpointBuilder> convention)
        {
            var builder = new TestEndpointBuilder();
            convention(builder);
            Metadata.AddRange(builder.Metadata);
        }
    }

    private class TestEndpointBuilder : EndpointBuilder
    {
        public new IList<object> Metadata { get; } = [];
        public new RequestDelegate? RequestDelegate { get; set; }
        public new string? DisplayName { get; set; }

        public override Endpoint Build()
        {
            return new Endpoint(
                RequestDelegate ?? (context => Task.CompletedTask),
                new EndpointMetadataCollection(Metadata),
                DisplayName
            );
        }
    }
}
