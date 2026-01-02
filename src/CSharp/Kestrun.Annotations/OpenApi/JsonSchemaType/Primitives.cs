// ——— Core ———

using System.Net;
using System.Net.Sockets;
using System.Xml;

public interface IOpenApiType
{
    object? RawValue { get; }
}

/// <summary>
/// Generic strongly-typed OpenAPI wrapper that is PowerShell-friendly.
/// </summary>
public abstract class OpenApiValue<T>(T value) : IOpenApiType
{
    public T Value { get; } = value;

    public object? RawValue => Value;

    public override string ToString() => Value?.ToString() ?? string.Empty;

    // Note: operators are per closed generic type (OpenApiValue<string>, OpenApiValue<int>, etc.)
    // Derived types will typically redeclare operators for best ergonomics if needed.
}

// ——— String & friends ———

/// <summary>OpenAPI string primitive type.</summary>
/// <param name="value">The string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String)]
public class OpenApiString(string value) : OpenApiValue<string>(value)
{
    public OpenApiString() : this(string.Empty) { }

    public static implicit operator string(OpenApiString s)
    {
        return s.Value;
    }

    public static implicit operator OpenApiString(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/uuid format.
/// </summary>
/// <param name="value">The UUID string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "uuid")]
public class OpenApiUuid(string value) : OpenApiString(value)
{
    public OpenApiUuid() : this(string.Empty) { }

    public static implicit operator OpenApiUuid(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/date format.
/// </summary>
/// <param name="value">The date string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "date")]
public class OpenApiDate(string value) : OpenApiString(value)
{
    public OpenApiDate() : this(string.Empty) { }

    public static implicit operator OpenApiDate(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/date-time format.
/// </summary>
/// <param name="value">The date-time string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "date-time")]
public class OpenApiDateTime(string value) : OpenApiString(value)
{
    public OpenApiDateTime() : this(string.Empty) { }

    public static implicit operator OpenApiDateTime(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/email format.
/// </summary>
/// <param name="value">The email string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "email")]
public class OpenApiEmail(string value) : OpenApiString(value)
{
    public OpenApiEmail() : this(string.Empty) { }

    public static implicit operator OpenApiEmail(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/binary format.
/// </summary>
/// <param name="value">The binary byte array value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "binary")]
public class OpenApiBinary(byte[] value) : OpenApiValue<byte[]>(value)
{
    public OpenApiBinary() : this([]) { }

    public static implicit operator byte[](OpenApiBinary b)
    {
        return b.Value;
    }

    public static implicit operator OpenApiBinary(byte[] b)
    {
        return new(b);
    }

    public override string ToString() => $"byte[{Value.Length}]";
}

/// <summary>
/// OpenAPI string/hostname format.
/// </summary>
/// <param name="value">The hostname string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "hostname")]
public class OpenApiHostname(string value) : OpenApiString(value)
{
    public OpenApiHostname() : this(string.Empty) { }

    public static implicit operator OpenApiHostname(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/ipv4 format.
/// </summary>
/// <param name="value">The IPv4 string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "ipv4")]
public class OpenApiIpv4(string value) : OpenApiString(value)
{
    public OpenApiIpv4() : this(string.Empty) { }

    public OpenApiIpv4(IPAddress ip) : this(ForceIpv4(ip)) { }

    public static implicit operator OpenApiIpv4(string s)
    {
        return new(s);
    }

    private static string ForceIpv4(IPAddress ip) =>
        ip.AddressFamily != AddressFamily.InterNetwork ? throw new ArgumentException("IP address must be IPv4") : ip.ToString();
}

/// <summary>
/// OpenAPI string/ipv6 format.
/// </summary>
/// <param name="value">The IPv6 string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "ipv6")]
public class OpenApiIpv6(string value) : OpenApiString(value)
{
    public OpenApiIpv6(IPAddress ip) : this(ForceIpv6(ip)) { }

    public OpenApiIpv6() : this(string.Empty) { }

    public static implicit operator OpenApiIpv6(string s)
    {
        return new(s);
    }

    private static string ForceIpv6(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException("IP address must be IPv6");
        }

        // Reject IPv4-mapped IPv6 addresses (optional but recommended)
        return ip.IsIPv4MappedToIPv6
            ? throw new ArgumentException("IPv4-mapped addresses are not allowed. Provide a real IPv6 address.")
            : ip.ToString();
    }
}


/// <summary>
/// OpenAPI string/uri format.
/// </summary>
/// <param name="value">The URI string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "uri")]
public class OpenApiUri(string value) : OpenApiString(value)
{
    public OpenApiUri(Uri uri) : this(uri.ToString()) { }
    public OpenApiUri() : this(string.Empty) { }

    public static implicit operator OpenApiUri(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/url format.
/// </summary>
/// <param name="value">The URL string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "url")]
public class OpenApiUrl(string value) : OpenApiString(value)
{
    public OpenApiUrl(Uri url) : this(url.ToString()) { }
    public OpenApiUrl() : this(string.Empty) { }

    public static implicit operator OpenApiUrl(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/byte (base64). Represented as bytes for convenience.
/// </summary>
/// <param name="value">The byte array value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "byte")]
public class OpenApiByte(byte[] value) : OpenApiValue<byte[]>(value)
{
    public OpenApiByte() : this([]) { }

    public static implicit operator byte[](OpenApiByte b)
    {
        return b.Value;
    }

    public static implicit operator OpenApiByte(byte[] b)
    {
        return new(b);
    }
    public override string ToString() => $"base64(byte[{Value.Length}])";
}


/// <summary>
/// OpenAPI string/password format.
/// </summary>
/// <param name="value">The password string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "password")]
public class OpenApiPassword(string value) : OpenApiString(value)
{
    public OpenApiPassword() : this(string.Empty) { }

    public static implicit operator OpenApiPassword(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/regex format.
/// </summary>
/// <param name="value">The regex string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "regex")]
public class OpenApiRegex(string value) : OpenApiString(value)
{
    public OpenApiRegex() : this(string.Empty) { }

    public static implicit operator OpenApiRegex(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/json format.
/// </summary>
/// <param name="value">The JSON string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "json")]
public class OpenApiJson(string value) : OpenApiString(value)
{
    public OpenApiJson() : this(string.Empty) { }

    public static implicit operator OpenApiJson(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/xml format.
/// </summary>
/// <param name="value">The XML string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "xml")]
public class OpenApiXml(string value) : OpenApiString(value)
{
    public OpenApiXml(XmlElement xml) : this(xml.OuterXml) { }
    public OpenApiXml() : this(string.Empty) { }

    public static implicit operator OpenApiXml(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/yaml format.
/// </summary>
/// <param name="value">The YAML string value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "yaml")]
public class OpenApiYaml(string value) : OpenApiString(value)
{
    public OpenApiYaml() : this(string.Empty) { }

    public static implicit operator OpenApiYaml(string s)
    {
        return new(s);
    }
}

#region  ——— Numeric & Boolean ———

/// <summary>
/// OpenAPI integer primitive type.
/// </summary>
/// <param name="value">The integer value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Integer)]
public class OpenApiInteger(long value) : OpenApiValue<long>(value)
{
    public OpenApiInteger() : this(0) { }

    public static implicit operator long(OpenApiInteger x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiInteger(long x)
    {
        return new(x);
    }
}

/// <summary>
/// OpenAPI integer/int32 format.
/// </summary>
/// <param name="value">The int32 value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Integer, Format = "int32")]
public class OpenApiInt32(int value) : OpenApiValue<int>(value)
{
    public OpenApiInt32() : this(0) { }

    public static implicit operator int(OpenApiInt32 x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiInt32(int x)
    {
        return new(x);
    }
}

/// <summary>
/// OpenAPI integer/int64 format.
/// </summary>
/// <param name="value">The int64 value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Integer, Format = "int64")]
public class OpenApiInt64(long value) : OpenApiValue<long>(value)
{
    public OpenApiInt64() : this(0) { }

    public static implicit operator long(OpenApiInt64 x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiInt64(long x)
    {
        return new(x);
    }
}

/// <summary>
/// OpenAPI number primitive type.
/// </summary>
/// <param name="value">The number value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Number)]
public class OpenApiNumber(double value) : OpenApiValue<double>(value)
{
    public OpenApiNumber() : this(0d) { }

    public static implicit operator double(OpenApiNumber x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiNumber(double x)
    {
        return new(x);
    }
}

/// <summary>
/// OpenAPI number/float format.
/// </summary>
/// <param name="value">The float value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Number, Format = "float")]
public class OpenApiFloat(float value) : OpenApiValue<float>(value)
{
    public OpenApiFloat() : this(0f) { }

    public static implicit operator float(OpenApiFloat x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiFloat(float x)
    {
        return new(x);
    }
}

/// <summary>
/// OpenAPI number/double format.
/// </summary>
/// <param name="value">The double value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Number, Format = "double")]
public class OpenApiDouble(double value) : OpenApiValue<double>(value)
{
    public OpenApiDouble() : this(0d) { }

    public static implicit operator double(OpenApiDouble x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiDouble(double x)
    {
        return new(x);
    }
}

/// <summary>
/// OpenAPI boolean primitive type.
/// </summary>
/// <param name="value">The boolean value.</param>
[OpenApiSchemaComponent(Type = OaSchemaType.Boolean)]
public class OpenApiBoolean(bool value) : OpenApiValue<bool>(value)
{
    public OpenApiBoolean() : this(false) { }

    public static implicit operator bool(OpenApiBoolean x)
    {
        return x.Value;
    }

    public static implicit operator OpenApiBoolean(bool x)
    {
        return new(x);
    }
}
#endregion
