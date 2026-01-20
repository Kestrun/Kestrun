using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackDispatchOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var opt = new CallbackDispatchOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), opt.DefaultTimeout);
        Assert.Equal(3, opt.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(2), opt.BaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), opt.MaxDelay);
    }
}
