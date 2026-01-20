using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Text;
using System.Management.Automation;
using Xunit;

namespace KestrunTests.Models;

public partial class KestrunResponseTests
{
    private static KestrunContext MakeCtx(string accept = "application/json") =>
        TestRequestFactory.CreateContext(path: "/test", headers: new Dictionary<string, string> { { "Accept", accept } });

    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("application/json", true)]
    [InlineData("application/vnd.foo+json", true)]
    [InlineData("application/vnd.foo+xml", true)]
    [InlineData("image/png", false)]
    [InlineData("", false)]
    public void IsTextBasedContentType_Detection(string type, bool expected)
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        Assert.Equal(expected, KestrunResponse.IsTextBasedContentType(type));
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteResponse_Chooses_Json_For_Accept()
    {
        var ctx = MakeCtx("application/json");
        var res = ctx.Response;

        await res.WriteResponseAsync(new { Name = "alice" }, StatusCodes.Status201Created);

        Assert.Equal(201, res.StatusCode);
        Assert.Contains("application/json", res.ContentType);
        var strBody = Assert.IsType<string>(res.Body);
        Assert.Contains("alice", strBody);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteResponse_Chooses_Highest_QValue_From_Accept()
    {
        var ctx = MakeCtx("application/xml;q=0.1, application/json;q=0.9");
        var res = ctx.Response;

        await res.WriteResponseAsync(new { Name = "alice" }, StatusCodes.Status200OK);

        Assert.Contains("application/json", res.ContentType);
        var strBody = Assert.IsType<string>(res.Body);
        Assert.Contains("alice", strBody);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteResponse_Treats_VendorPlusJson_As_Json()
    {
        var ctx = MakeCtx("application/vnd.foo+json");
        var res = ctx.Response;

        await res.WriteResponseAsync(new { Name = "alice" }, StatusCodes.Status200OK);

        Assert.Contains("application/json", res.ContentType);
        var strBody = Assert.IsType<string>(res.Body);
        Assert.Contains("alice", strBody);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteResponse_Wildcard_Accept_FallsBack_To_TextPlain()
    {
        var ctx = MakeCtx("*/*");
        var res = ctx.Response;

        await res.WriteResponseAsync(new { Name = "alice" }, StatusCodes.Status200OK);

        Assert.Contains("text/plain", res.ContentType);
        var strBody = Assert.IsType<string>(res.Body);
        Assert.Contains("alice", strBody);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Sets_ContentDisposition_With_FileName()
    {
        var ctx = MakeCtx();
        var res = ctx.Response;
        res.ContentDisposition = new ContentDispositionOptions
        {
            Type = ContentDispositionType.Attachment,
            FileName = "report.txt"
        };
        await res.WriteTextResponseAsync("body");

        var http = new DefaultHttpContext();
        await res.ApplyTo(http.Response);

        Assert.True(http.Response.Headers.TryGetValue("Content-Disposition", out var val));
        Assert.Contains("attachment", val.ToString());
        Assert.Contains("report.txt", val.ToString());
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Writes_String_Body()
    {
        var krCtx = MakeCtx();
        var res = krCtx.Response;
        await res.WriteTextResponseAsync("hello", StatusCodes.Status200OK, "text/plain");

        var http = new DefaultHttpContext();
        using var ms = new MemoryStream();
        http.Response.Body = ms;

        await res.ApplyTo(http.Response);

        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body, Encoding.UTF8);
        var text = reader.ReadToEnd();
        Assert.Equal("hello", text);
        Assert.Equal("text/plain; charset=" + res.AcceptCharset.WebName, http.Response.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Writes_Bytes_Body()
    {
        var krCtx = MakeCtx();
        var res = krCtx.Response;
        res.Body = Encoding.UTF8.GetBytes("bin");
        res.ContentType = "application/octet-stream";

        var http = new DefaultHttpContext();
        using var ms = new MemoryStream();
        http.Response.Body = ms;

        await res.ApplyTo(http.Response);

        http.Response.Body.Position = 0;
        var buf = new byte[3];
        _ = http.Response.Body.Read(buf, 0, 3);
        Assert.Equal("bin", Encoding.UTF8.GetString(buf));
    }
    private static KestrunResponse NewRes() => TestRequestFactory.CreateContext().Response;

    [Fact]
    [Trait("Category", "Models")]
    public void WriteTextResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteTextResponse("hello", StatusCodes.Status200OK);
        Assert.Equal("hello", res.Body);
        Assert.Equal(StatusCodes.Status200OK, res.StatusCode);
        Assert.Contains("text/plain", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteJsonResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteJsonResponse(new { a = 1 });
        Assert.Contains("\"a\": 1", res.Body as string);
        Assert.Contains("application/json", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteJsonResponse_Sanitizes_PSObject_Wrapped_Objects()
    {
        var res = NewRes();

        var payload = new PSObject();
        payload.TypeNames.Insert(0, "System.Management.Automation.PSCustomObject");
        payload.Properties.Add(new PSNoteProperty("id", 101L));
        payload.Properties.Add(new PSNoteProperty("name", "Laptop Pro"));

        var response = new Dictionary<string, object?>
        {
            ["page"] = 1,
            ["items"] = new[] { payload }
        };

        res.WriteJsonResponse(response);

        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("\"page\": 1", body);
        Assert.Contains("\"id\":", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("101", body);
        Assert.Contains("\"name\":", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Laptop Pro", body);
        Assert.DoesNotContain("\"members\"", body);
        Assert.DoesNotContain("\"properties\"", body);
        Assert.DoesNotContain("\"methods\"", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteYamlResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteYamlResponse(new { a = 1 });
        Assert.Contains("a: 1", res.Body as string);
        Assert.Contains("application/yaml", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteXmlResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteXmlResponse(new { a = 1 });
        Assert.Contains("<a>1</a>", res.Body as string);
        Assert.Contains("application/xml", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteXmlResponse_CustomRoot_Works()
    {
        var res = NewRes();
        res.WriteXmlResponse(new { a = 1 }, rootElementName: "HealthStatus");
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("<HealthStatus>", body);
        Assert.Contains("<a>1</a>", body);
    }

    private sealed class Node
    {
        public string Name { get; set; } = string.Empty;
        public Node? Next { get; set; }
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteXmlResponse_CycleDetection_Emits_Warning()
    {
        var n1 = new Node { Name = "one" };
        var n2 = new Node { Name = "two" };
        n1.Next = n2;
        n2.Next = n1; // cycle

        var res = NewRes();
        res.WriteXmlResponse(n1, rootElementName: "Graph");
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("CycleDetected", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteXmlResponse_DepthLimit_Emits_Warning()
    {
        // Build a deep linked list exceeding 32 levels
        Node head = new() { Name = "0" };
        var current = head;
        for (var i = 1; i < 32; i++)
        {
            var next = new Node { Name = i.ToString() };
            current.Next = next;
            current = next;
        }

        var res = NewRes();
        res.WriteXmlResponse(head, rootElementName: "Deep");
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("MaxDepthExceeded", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteXmlResponse_Default_Is_HumanReadable()
    {
        var res = NewRes();
        res.WriteXmlResponse(new { a = 1, b = new { c = 2 } }); // default compress=false -> readable
        var body = Assert.IsType<string>(res.Body);
        // Pretty print should introduce newlines and indentation
        Assert.Contains("\n", body);
        // Look for an indented child element (at least two spaces before <a>)
        var hasIndentedA = body.Split('\n').Any(l => l.StartsWith("  <a>"));
        Assert.True(hasIndentedA, "Expected pretty-printed indentation for <a> element");
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteXmlResponse_Compress_Produces_Compact()
    {
        var res = NewRes();
        res.WriteXmlResponse(new { a = 1, b = new { c = 2 } }, compress: true);
        var body = Assert.IsType<string>(res.Body);
        Assert.DoesNotContain("\n", body);
        Assert.Contains("<a>1</a>", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteJsonResponse_Default_Is_HumanReadable()
    {
        var res = NewRes();
        // Default call -> compress=false -> indented JSON
        res.WriteJsonResponse(new { a = 1, b = new { c = 2 } });
        var body = Assert.IsType<string>(res.Body);
        // Expect pretty printed structure with newlines and indentation
        Assert.Contains("\n", body);
        Assert.Contains("\n  \"a\"", body); // line with indentation for first property
        Assert.Contains("\"b\": {", body);
        // Ensure nested property appears on its own indented line
        Assert.True(body.Split('\n').Any(l => l.TrimStart().StartsWith("\"c\"")), "Expected nested property 'c' in pretty-printed output");
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteJsonResponse_Compress_Produces_Compact()
    {
        var res = NewRes();
        res.WriteJsonResponse(new { a = 1, b = new { c = 2 } }, depth: 10, compress: true);
        var body = Assert.IsType<string>(res.Body);
        // Compact JSON should have no newline characters (aside from possibly platform specific, but None formatting yields single line)
        Assert.DoesNotContain("\n", body);
        Assert.DoesNotContain("  \"a\"", body); // no indentation spaces
        Assert.Contains(/*lang=json,strict*/ "{\"a\":1,\"b\":{\"c\":2}}", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteBinaryResponse_SetsFields()
    {
        var res = NewRes();
        res.WriteBinaryResponse([1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, res.Body as byte[]);
        Assert.Equal("application/octet-stream", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteStreamResponse_SetsFields()
    {
        var res = NewRes();
        using var ms = new MemoryStream([1, 2]);
        res.WriteStreamResponse(ms);
        Assert.Equal(ms, res.Body);
        Assert.Equal("application/octet-stream", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteRedirectResponse_SetsHeaders()
    {
        var res = NewRes();
        res.WriteRedirectResponse("/foo", "go");
        Assert.Equal("/foo", res.RedirectUrl);
        Assert.Equal("go", res.Body);
        Assert.Equal("/foo", res.Headers["Location"]);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteErrorResponse_FromMessage()
    {
        var res = NewRes();
        res.WriteErrorResponse("oops");
        Assert.Equal(StatusCodes.Status500InternalServerError, res.StatusCode);
        Assert.NotNull(res.Body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteErrorResponse_FromException()
    {
        var res = NewRes();
        res.WriteErrorResponse(new Exception("bad"));
        Assert.Equal(StatusCodes.Status500InternalServerError, res.StatusCode);
        Assert.NotNull(res.Body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_WritesHttpResponse()
    {
        var res = NewRes();
        res.WriteTextResponse("hi");
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await res.ApplyTo(ctx.Response);
        ctx.Response.Body.Position = 0;
        var text = new StreamReader(ctx.Response.Body).ReadToEnd();
        Assert.Equal("hi", text);
        Assert.Equal("text/plain; charset=utf-8", ctx.Response.ContentType);
    }

    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("application/json", true)]
    [InlineData("application/octet-stream", false)]
    public void IsTextBasedContentType_Works(string type, bool expected) => Assert.Equal(expected, KestrunResponse.IsTextBasedContentType(type));
}

public partial class KestrunResponseTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void WriteCsvResponse_Respects_Config_NoHeader_And_Semicolon()
    {
        var res = NewRes();
        var cfg = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = ";"
        };

        res.WriteCsvResponse(new[] { new Person { Name = "bob", Age = 7 } }, config: cfg);

        var body = Assert.IsType<string>(res.Body);
        var normalized = body.Replace("\r\n", "\n").TrimEnd('\n');
        Assert.DoesNotContain("Name;Age", normalized);
        Assert.Contains("bob;7", normalized);
        Assert.Contains("text/csv", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteCsvResponseAsync_Tweak_NewLine_Uses_Custom()
    {
        var res = NewRes();
        await res.WriteCsvResponseAsync(new[] { new Person { Name = "a", Age = 1 }, new Person { Name = "b", Age = 2 } },
            tweak: c => c.NewLine = "\n");

        var body = Assert.IsType<string>(res.Body);
        // Expect LF newlines only
        Assert.DoesNotContain("\r\n", body);
        Assert.Contains("\n", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_Text_Includes_Details()
    {
        var ctx = MakeCtx("text/plain");
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("oops", details: "more info");

        Assert.Equal(StatusCodes.Status500InternalServerError, res.StatusCode);
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("Details:", body);
        Assert.Contains("more info", body);
        Assert.Contains("text/plain", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_Json_Includes_Details_Field()
    {
        var ctx = MakeCtx("application/json");
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("boom", details: "xyz");

        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("\"details\": \"xyz\"", body);
        Assert.Contains("application/json", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_Json_Default_Is_HumanReadable()
    {
        var ctx = MakeCtx("application/json");
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("error happened", details: "more");
        var body = Assert.IsType<string>(res.Body);
        // Human readable JSON should contain newlines and indentation for properties
        Assert.Contains("\n", body);
        Assert.Contains("  \"status\"", body);
        Assert.Contains("\"details\": \"more\"", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_Yaml_Includes_Details_Field()
    {
        var ctx = MakeCtx("application/yaml");
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("boom", contentType: "application/yaml", details: "xyz");

        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("details: xyz", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("application/yaml", res.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_Xml_Includes_Details_Field()
    {
        var ctx = MakeCtx("application/xml");
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("boom", contentType: "application/xml", details: "xyz");

        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("<Details>xyz</Details>", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("application/xml", res.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteRedirectResponse_NoBody_Sets_302_And_Location_Only()
    {
        var res = NewRes();
        res.WriteRedirectResponse("/foo");

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await res.ApplyTo(ctx.Response);

        Assert.Equal(StatusCodes.Status302Found, ctx.Response.StatusCode);
        Assert.Equal("/foo", ctx.Response.Headers.Location.ToString());
        Assert.True(ctx.Response.Body.Length == 0);
    }
}

public partial class KestrunResponseTests
{
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Sends_File_With_Length_LastModified_And_Disposition_Filename()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"kestr_file_{Guid.NewGuid():N}.txt");
        var content = "hello world";
        await File.WriteAllBytesAsync(temp, Encoding.UTF8.GetBytes(content));

        try
        {
            var krCtx = MakeCtx("text/plain");
            var res = krCtx.Response;
            res.ContentDisposition = new ContentDispositionOptions { Type = ContentDispositionType.Attachment };

            res.WriteFileResponse(temp, contentType: null);

            var ctx = new DefaultHttpContext();
            using var ms = new MemoryStream();
            ctx.Response.Body = ms;

            await res.ApplyTo(ctx.Response);

            // headers
            Assert.True(ctx.Response.Headers.ContainsKey("Last-Modified"));
            Assert.True(ctx.Response.Headers.ContainsKey("Content-Disposition"));
            Assert.Contains(Path.GetFileName(temp), ctx.Response.Headers["Content-Disposition"].ToString());
            Assert.Contains("text/plain", ctx.Response.ContentType);

            // content-length and body
            var fi = new FileInfo(temp);
            Assert.Equal(fi.Length, ctx.Response.ContentLength);
            ctx.Response.Body.Position = 0;
            var readBack = new StreamReader(ctx.Response.Body, Encoding.UTF8).ReadToEnd();
            Assert.Equal(content, readBack);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteFileResponse_NotFound_Sets_404_And_TextBody()
    {
        var res = NewRes();
        var missing = Path.Combine(Path.GetTempPath(), $"nope_{Guid.NewGuid():N}.txt");
        res.WriteFileResponse(missing, contentType: null);

        Assert.Equal(StatusCodes.Status404NotFound, res.StatusCode);
        Assert.Contains("text/plain", res.ContentType);
        _ = Assert.IsType<string>(res.Body);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await res.ApplyTo(ctx.Response);
        ctx.Response.Body.Position = 0;
        var body = new StreamReader(ctx.Response.Body).ReadToEnd();
        Assert.Contains("File not found", body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Writes_Seekable_Stream_With_ContentLength()
    {
        var data = Encoding.UTF8.GetBytes("stream-data");
        var res = NewRes();
        using var ms = new MemoryStream(data);
        res.WriteStreamResponse(ms);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await res.ApplyTo(ctx.Response);

        Assert.Equal(data.Length, ctx.Response.ContentLength);
        ctx.Response.Body.Position = 0;
        var got = new byte[data.Length];
        _ = ctx.Response.Body.Read(got, 0, got.Length);
        Assert.Equal(data, got);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Writes_NonSeekable_Stream_Without_ContentLength()
    {
        var data = Encoding.UTF8.GetBytes("chunked-data");
        var res = NewRes();
        using var ns = new NonSeekableStream(data);
        res.WriteStreamResponse(ns);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await res.ApplyTo(ctx.Response);

        Assert.Null(ctx.Response.ContentLength);
        ctx.Response.Body.Position = 0;
        var got = new byte[data.Length];
        _ = ctx.Response.Body.Read(got, 0, got.Length);
        Assert.Equal(data, got);
    }

    [Theory]
    [InlineData("application/json", "\"status\": 500")]
    [InlineData("application/yaml", "status: 500")]
    [InlineData("application/xml", "<Status>500</Status>")]
    public async Task WriteErrorResponse_Respects_Accept_ContentType_For_Message(string accept, string marker)
    {
        var ctx = MakeCtx(accept);
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("oops");

        Assert.Equal(StatusCodes.Status500InternalServerError, res.StatusCode);
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains(marker, body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(accept.Split('/')[1].Split('+')[0], res.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_Text_Fallback_When_Accept_TextPlain()
    {
        var ctx = MakeCtx("text/plain");
        var res = ctx.Response;
        await res.WriteErrorResponseAsync("uh oh", statusCode: 418);

        Assert.Equal(418, res.StatusCode);
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("Status: 418", body);
        Assert.Contains("uh oh", body);
        Assert.Contains("text/plain", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteErrorResponse_FromException_Includes_Or_Omits_Stack_Based_On_Flag()
    {
        var ctx = MakeCtx("text/plain");
        var res = ctx.Response;

        await res.WriteErrorResponseAsync(new InvalidOperationException("broken"), includeStack: false);
        var bodyNoStack = Assert.IsType<string>(res.Body);
        Assert.DoesNotContain("StackTrace:", bodyNoStack);

        await res.WriteErrorResponseAsync(new InvalidOperationException("broken"), includeStack: true);
        var bodyWithStack = Assert.IsType<string>(res.Body);
        Assert.Contains("StackTrace:", bodyWithStack);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Uses_AcceptCharset_For_String_Body_Bytes()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "text/plain";
        ctx.Request.Headers["Accept-Charset"] = "utf-16";

        var krCtx = TestRequestFactory.CreateContext(headers: ctx.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString()), form: []);
        var res = krCtx.Response;
        await res.WriteTextResponseAsync("hi");

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        await res.ApplyTo(http.Response);

        http.Response.Body.Position = 0;
        var bytes = ((MemoryStream)http.Response.Body).ToArray();
        Assert.Equal(Encoding.Unicode.GetBytes("hi"), bytes);
        Assert.Contains("text/plain", http.Response.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_Charset_Matches_When_Encoding_Set_To_Header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "text/plain";
        ctx.Request.Headers["Accept-Charset"] = "iso-8859-1";

        var krCtx = TestRequestFactory.CreateContext(headers: ctx.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString()), form: []);
        var res = krCtx.Response;
        res.Encoding = Encoding.GetEncoding("iso-8859-1");
        await res.WriteTextResponseAsync("café");

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        await res.ApplyTo(http.Response);

        http.Response.Body.Position = 0;
        var bytes = ((MemoryStream)http.Response.Body).ToArray();
        Assert.Equal(Encoding.GetEncoding("iso-8859-1").GetBytes("café"), bytes);
        Assert.Contains("charset=iso-8859-1", http.Response.ContentType, StringComparison.OrdinalIgnoreCase);
    }
}

public partial class KestrunResponseTests
{

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    [Fact]
    [Trait("Category", "Models")]
    public void WriteCsvResponse_Writes_Header_And_Row()
    {
        var res = NewRes();
        res.WriteCsvResponse(new[] { new Person { Name = "alice", Age = 42 } });

        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("Name,Age", body.Replace("\r\n", "\n"));
        Assert.Contains("alice,42", body.Replace("\r\n", "\n"));
        Assert.Contains("text/csv", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteCborResponseAsync_Sets_Bytes_And_ContentType()
    {
        var res = NewRes();
        await res.WriteCborResponseAsync(new { a = 1 });
        var bytes = Assert.IsType<byte[]>(res.Body);
        Assert.NotEmpty(bytes);
        Assert.Equal("application/cbor", res.ContentType);

        // null -> empty byte[]
        await res.WriteCborResponseAsync(null);
        bytes = Assert.IsType<byte[]>(res.Body);
        Assert.Empty(bytes);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteBsonResponseAsync_Sets_Bytes_And_ContentType()
    {
        var res = NewRes();
        await res.WriteBsonResponseAsync(new { a = 1 });
        var bytes = Assert.IsType<byte[]>(res.Body);
        Assert.NotEmpty(bytes);
        Assert.Equal("application/bson", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteResponse_Chooses_Csv_For_Accept()
    {
        var ctx = MakeCtx("text/csv");
        var res = ctx.Response;

        await res.WriteResponseAsync(new[] { new Person { Name = "alice", Age = 42 } }, StatusCodes.Status200OK);

        Assert.Contains("text/csv", res.ContentType);
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("Name,Age", body.Replace("\r\n", "\n"));
        Assert.Contains("alice,42", body.Replace("\r\n", "\n"));
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteHtmlResponseAsync_Renders_Template_With_Vars()
    {
        var res = NewRes();
        var vars = new Dictionary<string, object?>
        {
            ["User"] = new { Name = "alice" }
        };

        await res.WriteHtmlResponseAsync("<p>Hello {{User.Name}}</p>", vars);
        var body = Assert.IsType<string>(res.Body);
        Assert.Contains("Hello alice", body);
        Assert.Contains("text/html", res.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task WriteHtmlResponseFromFileAsync_Renders_Template_From_File()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"kestr_{Guid.NewGuid():N}.html");
        try
        {
            await File.WriteAllTextAsync(temp, "<h1>{{title}}</h1> <div>{{user.name}}</div>");

            var res = NewRes();
            var vars = new Dictionary<string, object?>
            {
                ["title"] = "Welcome",
                ["user"] = new { name = "alice" }
            };

            await res.WriteHtmlResponseFromFileAsync(temp, vars);
            var body = Assert.IsType<string>(res.Body);
            Assert.Contains("<h1>Welcome</h1>", body);
            Assert.Contains("alice", body);
            Assert.Contains("text/html", res.ContentType);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
