using Kestrun.Forms;
using Kestrun.Languages;
using Microsoft.OpenApi;
using Xunit;

namespace KestrunTests.Languages;

public class ParameterForInjectionInfoBaseTests
{
    private sealed class TestParam(string name, Type parameterType) : ParameterForInjectionInfoBase(name, parameterType);

    [Fact]
    [Trait("Category", "Languages")]
    public void IsRequestBody_IsTrue_WhenInIsNull()
    {
        var p = new TestParam("body", typeof(object));
        Assert.True(p.IsRequestBody);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void IsRequestBody_IsFalse_WhenInIsSet()
    {
        var p = new TestParam("q", typeof(string)) { In = ParameterLocation.Query };
        Assert.False(p.IsRequestBody);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void FormOptions_CanBeAttached()
    {
        var formOptions = new KrFormOptions();
        var p = new TestParam("payload", typeof(object)) { FormOptions = formOptions };
        Assert.Same(formOptions, p.FormOptions);
    }
}
