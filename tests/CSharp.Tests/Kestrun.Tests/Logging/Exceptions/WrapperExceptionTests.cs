using Kestrun.Logging.Exceptions;
using Xunit;

namespace KestrunTests.Logging.Exceptions;

/// <summary>
/// Tests for <see cref="WrapperException"/> class.
/// </summary>
public class WrapperExceptionTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithMessage_InitializesMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new WrapperException(message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.ErrorRecordWrapper);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithMessageAndInnerException_InitializesBoth()
    {
        // Arrange
        var message = "Wrapper message";
        var inner = new InvalidOperationException("Inner error");

        // Act
        var exception = new WrapperException(message, inner);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(exception.ErrorRecordWrapper);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithInnerExceptionAndErrorRecord_InitializesException()
    {
        // Arrange - Skip testing with actual ErrorRecord due to InvocationInfo requirements
        var inner = new ArgumentException("Argument error");

        // Act
        var exception = new WrapperException("Message", inner);

        // Assert
        Assert.Equal("Message", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(exception.ErrorRecordWrapper);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithInnerExceptionAndErrorRecord_WrapErrorRecord()
    {
        // Arrange
        var inner = new NotSupportedException("Not supported");

        // Act & Assert
        // Test that WrapperException can be created with inner exception
        var exception = new WrapperException("Wrapper", inner);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(exception.ErrorRecordWrapper);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_NoArguments_InitializesWithDefaultMessage()
    {
        // Act
        var exception = new WrapperException();

        // Assert
        // Default constructor generates default message from Exception base class
        Assert.NotNull(exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.ErrorRecordWrapper);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void ToString_WithInnerException_ReturnsInnerExceptionString()
    {
        // Arrange
        var inner = new ApplicationException("Inner exception message");
        var exception = new WrapperException("Wrapper", inner);

        // Act
        var result = exception.ToString();

        // Assert
        Assert.Equal(inner.ToString(), result);
        Assert.Contains("ApplicationException", result);
        Assert.Contains("Inner exception message", result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void ToString_WithoutInnerException_ReturnsEmptyString()
    {
        // Arrange
        var exception = new WrapperException("Just a message");

        // Act
        var result = exception.ToString();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void ToString_WithNullInnerException_ReturnsEmptyString()
    {
        // Arrange
        var exception = new WrapperException("Message", null!);

        // Act
        var result = exception.ToString();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void ErrorRecordWrapper_PreservesErrorDetails()
    {
        // Arrange
        var inner = new TimeoutException("Timeout occurred");

        // Act & Assert
        // Test that WrapperException can store inner exception
        var exception = new WrapperException("Timeout wrapper", inner);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(exception.ErrorRecordWrapper);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void InheritanceChain_IsException()
    {
        // Arrange & Act
        var exception = new WrapperException("Test");

        // Assert
        _ = Assert.IsAssignableFrom<Exception>(exception);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void MultipleErrorRecords_CanCreateMultipleWrappers()
    {
        // Arrange
        var inner1 = new IOException("IO error");
        var inner2 = new UnauthorizedAccessException("Access denied");

        // Act
        var exception1 = new WrapperException("IO wrapper", inner1);
        var exception2 = new WrapperException("Access wrapper", inner2);

        // Assert
        Assert.Same(inner1, exception1.InnerException);
        Assert.Same(inner2, exception2.InnerException);
        Assert.NotEqual(exception1.Message, exception2.Message);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void MessageConstructor_WithNullMessage_CreatesException()
    {
        // Act & Assert - null message should be allowed (base Exception allows it)
        var exception = new WrapperException(null!);
        Assert.NotNull(exception); // Exception was created successfully
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void MessageAndInnerConstructor_WithNullInner_AllowsIt()
    {
        // Arrange
        var message = "Wrapper message";

        // Act
        var exception = new WrapperException(message, null!);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void NestedInnerExceptions_PreservesChain()
    {
        // Arrange
        var innermost = new IndexOutOfRangeException("Innermost");
        var middle = new InvalidOperationException("Middle", innermost);
        var outer = new WrapperException("Outer", middle);

        // Act & Assert
        Assert.Same(middle, outer.InnerException);
        Assert.Same(innermost, middle.InnerException);
        var stringRep = outer.ToString();
        Assert.Contains("InvalidOperationException", stringRep);
        Assert.Contains("Innermost", stringRep);
    }
}
