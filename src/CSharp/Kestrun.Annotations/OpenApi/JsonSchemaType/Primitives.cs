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

// ——— Integers / Numbers / Boolean ———

[OpenApiSchemaComponent(Type = OaSchemaType.Integer, Format = "int32")]
public class OpenApiInt32 : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Integer, Format = "int64")]
public class OpenApiInt64 : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Number, Format = "float")]
public class OpenApiFloat : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Number, Format = "double")]
public class OpenApiDouble : IOpenApiType { }

[OpenApiSchemaComponent(Type = OaSchemaType.Boolean)]
public class OpenApiBoolean : IOpenApiType { }
