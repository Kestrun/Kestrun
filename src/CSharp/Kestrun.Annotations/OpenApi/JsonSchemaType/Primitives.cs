// ——— Core ———

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

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "uuid")]
public class OpenApiUuid(string value) : OpenApiString(value)
{
    public OpenApiUuid() : this(string.Empty) { }

    public static implicit operator OpenApiUuid(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "date")]
public class OpenApiDate(string value) : OpenApiString(value)
{
    public OpenApiDate() : this(string.Empty) { }

    public static implicit operator OpenApiDate(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "date-time")]
public class OpenApiDateTime(string value) : OpenApiString(value)
{
    public OpenApiDateTime() : this(string.Empty) { }

    public static implicit operator OpenApiDateTime(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "email")]
public class OpenApiEmail(string value) : OpenApiString(value)
{
    public OpenApiEmail() : this(string.Empty) { }

    public static implicit operator OpenApiEmail(string s)
    {
        return new(s);
    }
}

/// <summary>Handy for raw uploads; OpenAPI string/binary.</summary>
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

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "hostname")]
public class OpenApiHostname(string value) : OpenApiString(value)
{
    public OpenApiHostname() : this(string.Empty) { }

    public static implicit operator OpenApiHostname(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "ipv4")]
public class OpenApiIpv4(string value) : OpenApiString(value)
{
    public OpenApiIpv4() : this(string.Empty) { }

    public static implicit operator OpenApiIpv4(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "ipv6")]
public class OpenApiIpv6(string value) : OpenApiString(value)
{
    public OpenApiIpv6() : this(string.Empty) { }

    public static implicit operator OpenApiIpv6(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "uri")]
public class OpenApiUri(string value) : OpenApiString(value)
{
    public OpenApiUri() : this(string.Empty) { }

    public static implicit operator OpenApiUri(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "url")]
public class OpenApiUrl(string value) : OpenApiString(value)
{
    public OpenApiUrl() : this(string.Empty) { }

    public static implicit operator OpenApiUrl(string s)
    {
        return new(s);
    }
}

/// <summary>
/// OpenAPI string/byte (base64). Represented as bytes for convenience.
/// </summary>
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

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "password")]
public class OpenApiPassword(string value) : OpenApiString(value)
{
    public OpenApiPassword() : this(string.Empty) { }

    public static implicit operator OpenApiPassword(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "regex")]
public class OpenApiRegex(string value) : OpenApiString(value)
{
    public OpenApiRegex() : this(string.Empty) { }

    public static implicit operator OpenApiRegex(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "json")]
public class OpenApiJson(string value) : OpenApiString(value)
{
    public OpenApiJson() : this(string.Empty) { }

    public static implicit operator OpenApiJson(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "xml")]
public class OpenApiXml(string value) : OpenApiString(value)
{
    public OpenApiXml() : this(string.Empty) { }

    public static implicit operator OpenApiXml(string s)
    {
        return new(s);
    }
}

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "yaml")]
public class OpenApiYaml(string value) : OpenApiString(value)
{
    public OpenApiYaml() : this(string.Empty) { }

    public static implicit operator OpenApiYaml(string s)
    {
        return new(s);
    }
}

// ——— Integers / Numbers / Boolean ———

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

