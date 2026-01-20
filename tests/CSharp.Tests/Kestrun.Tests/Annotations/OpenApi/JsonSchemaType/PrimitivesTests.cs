using System.Net;
using System.Net.Sockets;
using System.Xml;
using Xunit;

namespace KestrunTests.Annotations.OpenApi.JsonSchemaType;

public class PrimitivesTests
{
    private sealed class NullableStringValue(string? value) : OpenApiScalar<string?>(value) { }

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
    public void OpenApiInt32_Int64_Float_Double_ImplicitConversions_Work()
    {
        OpenApiInt32 i32 = 42;
        int i32v = i32;

        OpenApiInt64 i64 = 42000000000L;
        long i64v = i64;

        OpenApiFloat f = 1.25f;
        float fv = f;

        OpenApiDouble d = 2.5d;
        double dv = d;

        Assert.Equal(42, i32.Value);
        Assert.Equal(42, i32v);

        Assert.Equal(42000000000L, i64.Value);
        Assert.Equal(42000000000L, i64v);

        Assert.Equal(1.25f, f.Value);
        Assert.Equal(1.25f, fv);

        Assert.Equal(2.5d, d.Value);
        Assert.Equal(2.5d, dv);
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
    public void OpenApiHostname_Password_Regex_Json_Yaml_ImplicitFromString_Works()
    {
        OpenApiHostname host = "api.example.com";
        OpenApiPassword pass = "secret";
        OpenApiRegex regex = "^a+$";
        OpenApiJson json = /*lang=json,strict*/ "{\"x\":1}";
        OpenApiYaml yaml = "x: 1";

        Assert.Equal("api.example.com", host.Value);
        Assert.Equal("secret", pass.Value);
        Assert.Equal("^a+$", regex.Value);
        Assert.Equal(/*lang=json,strict*/ "{\"x\":1}", json.Value);
        Assert.Equal("x: 1", yaml.Value);

        // All of these inherit OpenApiString.
        Assert.Equal(host.Value, host.RawValue);
        Assert.Equal(pass.Value, pass.ToString());
    }

    [Fact]
    public void OpenApiIpv4_FromIPAddress_RejectsNonIpv4()
    {
        var ipv6 = IPAddress.IPv6Loopback;

        var ex = Assert.Throws<ArgumentException>(() => new OpenApiIpv4(ipv6));
        Assert.Contains("IPv4", ex.Message);
    }

    [Fact]
    public void OpenApiIpv4_FromIPAddress_AcceptsIpv4()
    {
        var ipv4 = IPAddress.Parse("127.0.0.1");
        var v = new OpenApiIpv4(ipv4);

        Assert.Equal("127.0.0.1", v.Value);
        Assert.Equal("127.0.0.1", v.RawValue);
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
    public void OpenApiIpv6_FromIPAddress_AcceptsRealIpv6()
    {
        var ipv6 = IPAddress.Parse("2001:db8::1");
        var v = new OpenApiIpv6(ipv6);

        Assert.Equal("2001:db8::1", v.Value);
        Assert.Equal("2001:db8::1", v.RawValue);
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
    public void OpenApiXml_FromString_UsesSameValue()
    {
        OpenApiXml x = "<a />";

        Assert.Equal("<a />", x.Value);
        Assert.Equal("<a />", x.RawValue);
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

    [Fact]
    public void OpenApiValue_ToString_HandlesNullValue()
    {
        var v = new NullableStringValue(null);

        Assert.Null(v.Value);
        Assert.Null(v.RawValue);
        Assert.Equal(string.Empty, v.ToString());
    }
}
