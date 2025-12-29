using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Collections;
using Xunit;

namespace KestrunTests.Models;

public class WriteFormUrlEncodedResponseTests
{
    static WriteFormUrlEncodedResponseTests() =>
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    private static KestrunResponse CreateKestrunResponse()
    {
        return TestRequestFactory.CreateContext().Response;
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_WithHashtable_EncodesCorrectly()
    {
        var response = CreateKestrunResponse();
        var input = new Hashtable
        {
            ["username"] = "alice",
            ["password"] = "secret123",
            ["email"] = "alice@example.com"
        };

        response.WriteFormUrlEncodedResponse(input, StatusCodes.Status200OK);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        Assert.Contains("username=alice", body);
        Assert.Contains("password=secret123", body);
        Assert.Contains("email=alice%40example.com", body);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("application/x-www-form-urlencoded", response.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_WithDictionary_EncodesCorrectly()
    {
        var response = CreateKestrunResponse();
        var input = new Dictionary<string, object?>
        {
            ["name"] = "Bob",
            ["age"] = 30,
            ["active"] = true
        };

        response.WriteFormUrlEncodedResponse(input);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        Assert.Contains("name=Bob", body);
        Assert.Contains("age=30", body);
        Assert.Contains("active=True", body);
        Assert.Equal("application/x-www-form-urlencoded", response.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_WithObject_ExtractsAndEncodesProperties()
    {
        var response = CreateKestrunResponse();
        var input = new TestFormData { Username = "charlie", Role = "admin", Score = 95 };

        response.WriteFormUrlEncodedResponse(input);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        Assert.Contains("Username=charlie", body);
        Assert.Contains("Role=admin", body);
        Assert.Contains("Score=95", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_WithNull_ThrowsArgumentNullException()
    {
        var response = CreateKestrunResponse();

        _ = Assert.Throws<ArgumentNullException>(() => response.WriteFormUrlEncodedResponse(null));
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_WithEnumerable_CreatesIndexedKeys()
    {
        var response = CreateKestrunResponse();
        var input = new[] { "apple", "banana", "cherry" };

        response.WriteFormUrlEncodedResponse(input);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        Assert.Contains("item%5B0%5D=apple", body); // item[0]=apple (URL encoded)
        Assert.Contains("item%5B1%5D=banana", body); // item[1]=banana
        Assert.Contains("item%5B2%5D=cherry", body); // item[2]=cherry
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteFormUrlEncodedResponseAsync_WithHashtable_EncodesCorrectly()
    {
        var response = CreateKestrunResponse();
        var input = new Hashtable
        {
            ["clientId"] = "12345",
            ["clientSecret"] = "mysecret"
        };

        await response.WriteFormUrlEncodedResponseAsync(input, StatusCodes.Status202Accepted);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        Assert.Contains("clientId=12345", body);
        Assert.Contains("clientSecret=mysecret", body);
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteFormUrlEncodedResponseAsync_WithObject_PreservesPropertyValues()
    {
        var response = CreateKestrunResponse();
        var input = new TestFormData { Username = "diana", Role = "user", Score = 75 };

        await response.WriteFormUrlEncodedResponseAsync(input);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        Assert.Contains("Username=diana", body);
        Assert.Contains("Role=user", body);
        Assert.Contains("Score=75", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteFormUrlEncodedResponseAsync_WithNull_ThrowsArgumentNullException()
    {
        var response = CreateKestrunResponse();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => response.WriteFormUrlEncodedResponseAsync(null));
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_WithSpecialCharacters_UrlEncodes()
    {
        var response = CreateKestrunResponse();
        var input = new Hashtable
        {
            ["message"] = "Hello World!",
            ["email"] = "test@example.com",
            ["url"] = "https://example.com?param=value&other=123"
        };

        response.WriteFormUrlEncodedResponse(input);

        Assert.NotNull(response.Body);
        var body = response.Body?.ToString();
        Assert.NotNull(body);
        // FormUrlEncodedContent should properly URL-encode special characters
        Assert.Contains("message=Hello+World%21", body);
        Assert.Contains("email=test%40example.com", body);
        Assert.Contains("url=", body); // The URL will be encoded
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_DefaultContentType_IsApplicationFormUrlEncoded()
    {
        var response = CreateKestrunResponse();
        var input = new Hashtable { ["test"] = "value" };

        response.WriteFormUrlEncodedResponse(input);

        Assert.Equal("application/x-www-form-urlencoded", response.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteFormUrlEncodedResponse_DefaultStatusCode_Is200OK()
    {
        var response = CreateKestrunResponse();
        var input = new Hashtable { ["test"] = "value" };

        response.WriteFormUrlEncodedResponse(input);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    // Test helper class
    private class TestFormData
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}
