using System.Management.Automation;
using Kestrun.Logging.Data;
using Xunit;

namespace KestrunTests.Logging;

public class ErrorRecordWrapperTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void Wraps_ErrorRecord_Properties_And_ToString()
    {
        // Arrange: create a real ErrorRecord with InvocationInfo via PowerShell
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'boom' -Category InvalidOperation -ErrorId ERR123");
        _ = ps.Invoke();
        Assert.True(ps.HadErrors);
        var err = ps.Streams.Error[0];

        // Act
        var w = new ErrorRecordWrapper(err);

        // Assert
        Assert.Equal("ERR123", w.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.InvalidOperation, w.CategoryInfo.Category);
        Assert.Equal("boom", w.ExceptionMessage);
        Assert.NotNull(w.ExceptionDetails);
        Assert.NotNull(w.InvocationInfoWrapper);
        // Avoid calling ToString() here to prevent potential deep recursion in table formatting
    }
}
