using System.Net;
using System.Net.Sockets;
using System.Xml;
using Xunit;

namespace KestrunTests.Annotations.OpenApi.JsonSchemaType;

public class PrimitivesTests
{
    [Fact]
    public void OpenApiString_DefaultCtor_IsEmpty_AndRawValueMatches()
    {
        var s = new OpenApiString();

        Assert.Equal(string.Empty, s.Value);
        Assert.Equal(string.Empty, s.RawValue);
        Assert.Equal(string.Empty, s.ToString());
    }

    [Fact]
    public void OpenApiString_ImplicitConversions_Work()
    {
        OpenApiString wrapped = "hello";
        string unwrapped = wrapped;

        Assert.Equal("hello", wrapped.Value);
        Assert.Equal("hello", unwrapped);
        Assert.Equal("hello", wrapped.RawValue);
    }

    [Fact]
    public void OpenApiInteger_Number_Boolean_ImplicitConversions_Work()
    {
        OpenApiInteger i = 123L;
        long il = i;

        OpenApiNumber n = 12.5;
        double nd = n;

        OpenApiBoolean b = true;
        bool bb = b;

        Assert.Equal(123L, i.Value);
        Assert.Equal(123L, il);

        Assert.Equal(12.5, n.Value);
        Assert.Equal(12.5, nd);

        Assert.True(b.Value);
        Assert.True(bb);

        Assert.Equal(123L, i.RawValue);
        Assert.Equal(12.5, n.RawValue);
        Assert.Equal(true, b.RawValue);
    }

    [Fact]
    public void OpenApiBinary_ImplicitConversions_Work_AndToStringIsFriendly()
    {
        var bytes = new byte[] { 1, 2, 3 };
        OpenApiBinary b = bytes;

        byte[] back = b;

        Assert.Equal(bytes, b.Value);
        Assert.Equal(bytes, back);
        Assert.Equal(bytes, b.RawValue);
        Assert.Equal("byte[3]", b.ToString());
    }

    [Fact]
    public void OpenApiByte_ImplicitConversions_Work_AndToStringIsFriendly()
    {
        var bytes = new byte[] { 4, 5 };
        OpenApiByte b = bytes;

        byte[] back = b;

        Assert.Equal(bytes, b.Value);
        Assert.Equal(bytes, back);
        Assert.Equal(bytes, b.RawValue);
        Assert.Equal("base64(byte[2])", b.ToString());
    }

    [Fact]
    public void OpenApiIpv4_FromIPAddress_RejectsNonIpv4()
    {
        var ipv6 = IPAddress.IPv6Loopback;

        var ex = Assert.Throws<ArgumentException>(() => new OpenApiIpv4(ipv6));
        Assert.Contains("IPv4", ex.Message);
    }

    [Fact]
    public void OpenApiIpv6_FromIPAddress_RejectsNonIpv6()
    {
        var ipv4 = IPAddress.Loopback;

        var ex = Assert.Throws<ArgumentException>(() => new OpenApiIpv6(ipv4));
        Assert.Contains("IPv6", ex.Message);
    }

    [Fact]
    public void OpenApiIpv6_FromIPAddress_RejectsIpv4Mapped()
    {
        var mapped = IPAddress.Parse("::ffff:127.0.0.1");
        Assert.Equal(AddressFamily.InterNetworkV6, mapped.AddressFamily);
        Assert.True(mapped.IsIPv4MappedToIPv6);

        var ex = Assert.Throws<ArgumentException>(() => new OpenApiIpv6(mapped));
        Assert.Contains("IPv4-mapped", ex.Message);
    }

    [Fact]
    public void OpenApiUri_AndOpenApiUrl_FromUri_Work()
    {
        var uri = new Uri("https://example.com/path?q=1");

        var oaUri = new OpenApiUri(uri);
        var oaUrl = new OpenApiUrl(uri);

        Assert.Equal(uri.ToString(), oaUri.Value);
        Assert.Equal(uri.ToString(), oaUrl.Value);
        Assert.Equal(uri.ToString(), oaUri.RawValue);
        Assert.Equal(uri.ToString(), oaUrl.RawValue);
    }

    [Fact]
    public void OpenApiXml_FromXmlElement_UsesOuterXml()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><child /></root>");

        var el = doc.DocumentElement!;
        var x = new OpenApiXml(el);

        Assert.Equal(el.OuterXml, x.Value);
        Assert.Equal(el.OuterXml, x.RawValue);
    }

    [Fact]
    public void FormatSpecificOpenApiStrings_ConstructAndConvert()
    {
        OpenApiUuid uuid = "a54a57ca-36f8-421b-a6b4-2e8f26858a4c";
        OpenApiEmail email = "user@example.com";
        OpenApiDate date = "2023-10-29";
        OpenApiDateTime dt = "2023-10-29T10:11:12Z";

        Assert.Equal("a54a57ca-36f8-421b-a6b4-2e8f26858a4c", uuid.Value);
        Assert.Equal("user@example.com", email.Value);
        Assert.Equal("2023-10-29", date.Value);
        Assert.Equal("2023-10-29T10:11:12Z", dt.Value);

        // These inherit OpenApiString so ToString/RawValue should behave like a string wrapper.
        Assert.Equal(date.Value, date.ToString());
        Assert.Equal(dt.Value, dt.RawValue);
    }
}
