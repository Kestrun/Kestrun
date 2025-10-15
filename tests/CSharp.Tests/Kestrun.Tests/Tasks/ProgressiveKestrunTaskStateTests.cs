using Kestrun.Tasks;
using Xunit;

namespace KestrunTests.Tasks;

public class ProgressiveKestrunTaskStateTests
{
    [Fact]
    public void Defaults_Are_NotStarted_And_Zero()
    {
        var s = new ProgressiveKestrunTaskState();
        Assert.Equal(0, s.PercentComplete);
        Assert.Equal("Not started", s.StatusMessage);
        Assert.Equal("0% - Not started", s.ToString());
    }

    [Fact]
    public void StatusMessage_Rejects_Null()
    {
        var s = new ProgressiveKestrunTaskState();
        _ = Assert.Throws<ArgumentNullException>(() => s.StatusMessage = null!);
    }

    [Fact]
    public void Reset_Sets_Zero_And_Message_Default_Or_Custom()
    {
        var s = new ProgressiveKestrunTaskState
        {
            PercentComplete = 80,
            StatusMessage = "Working"
        };
        s.Reset();
        Assert.Equal(0, s.PercentComplete);
        Assert.Equal("Not started", s.StatusMessage);

        s.PercentComplete = 20;
        s.StatusMessage = "Phase1";
        s.Reset("Ready");
        Assert.Equal(0, s.PercentComplete);
        Assert.Equal("Ready", s.StatusMessage);
    }

    [Fact]
    public void Complete_Sets_100_And_Message_Default_Or_Custom()
    {
        var s = new ProgressiveKestrunTaskState();
        s.Complete();
        Assert.Equal(100, s.PercentComplete);
        Assert.Equal("Completed", s.StatusMessage);

        s.Reset();
        s.Complete("All good");
        Assert.Equal(100, s.PercentComplete);
        Assert.Equal("All good", s.StatusMessage);
    }

    [Fact]
    public void Fail_Sets_100_And_Message_Default_Or_Custom()
    {
        var s = new ProgressiveKestrunTaskState();
        s.Fail();
        Assert.Equal(100, s.PercentComplete);
        Assert.Equal("Failed", s.StatusMessage);

        s.Reset();
        s.Fail("Boom");
        Assert.Equal(100, s.PercentComplete);
        Assert.Equal("Boom", s.StatusMessage);
    }

    [Fact]
    public void Cancel_Sets_100_And_Message_Default_Or_Custom()
    {
        var s = new ProgressiveKestrunTaskState();
        s.Cancel();
        Assert.Equal(100, s.PercentComplete);
        Assert.Equal("Cancelled", s.StatusMessage);

        s.Reset();
        s.Cancel("Stopped by user");
        Assert.Equal(100, s.PercentComplete);
        Assert.Equal("Stopped by user", s.StatusMessage);
    }

    [Fact]
    public void ToString_Reflects_Current_State()
    {
        var s = new ProgressiveKestrunTaskState
        {
            PercentComplete = 42,
            StatusMessage = "Uploading"
        };
        Assert.Equal("42% - Uploading", s.ToString());
    }
}
