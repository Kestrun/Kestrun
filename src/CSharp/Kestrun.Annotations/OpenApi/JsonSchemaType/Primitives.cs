// ——— String & friends ———
public interface IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.String)]
public class OpenApiString : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "uuid")]
public class OpenApiUuid : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "date")]
public class OpenApiDate : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "date-time")]
public class OpenApiDateTime : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "email")]
public class OpenApiEmail : OpenApiString { }

// Handy for raw uploads; see usage below
[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "binary")]
public class OpenApiBinary : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "hostname")]
public class OpenApiHostname : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "ipv4")]
public class OpenApiIpv4 : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "ipv6")]
public class OpenApiIpv6 : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "uri")]
public class OpenApiUri : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "url")]
public class OpenApiUrl : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "byte")]
public class OpenApiByte : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "password")]
public class OpenApiPassword : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "regex")]
public class OpenApiRegex : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "json")]
public class OpenApiJson : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "xml")]
public class OpenApiXml : OpenApiString { }

[OpenApiSchemaComponent(Type = OaSchemaType.String, Format = "yaml")]
public class OpenApiYaml : OpenApiString { }

// ——— Integers / Numbers / Boolean ———
[OpenApiSchemaComponent(Type = OaSchemaType.Integer)]
public class OpenApiInteger : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Integer, Format = "int32")]
public class OpenApiInt32 : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Integer, Format = "int64")]
public class OpenApiInt64 : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Number)]
public class OpenApiNumber : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Number, Format = "float")]
public class OpenApiFloat : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Number, Format = "double")]
public class OpenApiDouble : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Boolean)]
public class OpenApiBoolean : IOpenApiType { }

/// <summary>
///  OpenAPI Schema primitive/object kinds.
/// </summary>
public class OaString { }
/// <summary>
/// OpenAPI Schema primitive/object kinds.
/// </summary>
public class OaNumber { }
/// <summary>
/// OpenAPI Schema primitive/object kinds.
/// </summary>
public class OaInteger { }
/// <summary>
/// OpenAPI Schema primitive/object kinds.
/// </summary>
public class OaBoolean { }
