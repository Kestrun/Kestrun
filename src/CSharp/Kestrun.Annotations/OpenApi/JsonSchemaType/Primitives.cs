// ——— String & friends ———
public interface IOpenApiType { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.String)]
public class OpenApiString : IOpenApiType { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.String, Format = "uuid")]
public class OpenApiUuid : OpenApiString { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.String, Format = "date")]
public class OpenApiDate : OpenApiString { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.String, Format = "date-time")]
public class OpenApiDateTime : OpenApiString { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.String, Format = "email")]
public class OpenApiEmail : OpenApiString { }

// Handy for raw uploads; see usage below
[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.String, Format = "binary")]
public class OpenApiBinary : OpenApiString { }

// ——— Integers / Numbers / Boolean ———

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.Integer, Format = "int32")]
public class OpenApiInt32 : IOpenApiType { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.Integer, Format = "int64")]
public class OpenApiInt64 : IOpenApiType { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.Number, Format = "float")]
public class OpenApiFloat : IOpenApiType { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.Number, Format = "double")]
public class OpenApiDouble : IOpenApiType { }

[OpenApiSchemaComponent]
[OpenApiProperty(Type = OaSchemaType.Boolean)]
public class OpenApiBoolean : IOpenApiType { }
