
<#
    15.1 Start / Stop Patterns
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)


# Ensure Kestrun module (which contains the OpenAPI attribute types) is loaded
if (-not (Get-Module -Name Kestrun)) {
    $modulePath = Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\src\PowerShell\Kestrun\Kestrun.psm1'
    if (Test-Path $modulePath) {
        Import-Module -Force $modulePath
    } else {
        Write-Warning "Kestrun module not found at $modulePath. OpenAPI attributes may not resolve."
    }
}


New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
# Optional helpers for OpenAPI-friendly attributes
# No 'using namespace' needed—attributes are global



[OpenApiModelKind([OpenApiModelKind]::Schema)]
[OpenApiSchema(Required = 'Street')]
[OpenApiSchema(Required = 'City')]
[OpenApiSchema(Required = 'PostalCode')]
class Address {
    [OpenApiSchemaAttribute(Description = 'The street address' , Default = '123 Main St', title = 'Street Address', example = '123 Main St', Pattern = '^[\w\s\d]+$' , XmlName = 'StreetAddress', MaxLength = 100 )]
    [string]$Street

    [OpenApiSchemaAttribute(Description = 'The city name' , Default = 'Anytown', title = 'City Name', example = 'Anytown', Pattern = '^[\w\s\d]+$' , XmlName = 'CityName' )]
    [string]$City

    [OpenApiSchemaAttribute(Description = 'The postal code' , Default = '12345', title = 'Postal Code', example = '12345', Pattern = '^\d{5}(-\d{4})?$' , XmlName = 'PostalCode' )]
    [string]$PostalCode

    [OpenApiSchemaAttribute(Description = 'The apartment number' , Default = 101, title = 'Apartment Number', example = 101, Minimum = 1 , XmlName = 'AptNumber' )]
    [int]$ApartmentNumber
}




[OpenApiModelKind([OpenApiModelKind]::Schema)]
[OpenApiSchema(Required = 'Name')]
class UserInfoResponse {
    [OpenApiSchema(Description = 'User identifier', Type = [OaSchemaType]::Integer, Format = 'int32', Minimum = 0)]
    [int]$Id

    [OpenApiSchema(Description = 'Display name', MaxLength = 50)]
    [string]$Name

    [OpenApiSchema(Description = 'Age in years', Minimum = 0, Maximum = 120)]
    [long]$Age

    [OpenApiSchema(Description = 'Mailing address')]
    [Address]$Address
}

[OpenApiModelKind([OpenApiModelKind]::Parameters)] #, JoinClassName = '.')]
class UserListQueryParams {
    [OpenApiParameter(In = [OaParameterLocation]::Query, Name = 'name')]
    [OpenApiSchema(Description = 'Filter by name (contains)')]
    [string]$Name

    [OpenApiParameter(In = [OaParameterLocation]::Cookie, Name = 'minAge')]
    [OpenApiSchema(Description = 'Minimum age', Type = [OaSchemaType]::Integer, Format = 'int32', Minimum = 0)]
    [int]$MinAge
}
# Response components (member-level responses; JoinClassName ties class+member in key)
[OpenApiModelKind([OpenApiModelKind]::Response, JoinClassName = '-')]
class AddressResponse {
    [OpenApiResponse( Description = 'Successful retrieval of address', SchemaRef = 'Address' )]
    $OK
    [OpenApiResponse( Description = 'Address not found' )]
    $NotFound
}

# Example components (member-level examples; JoinClassName ties class+member in key)
[OpenApiModelKind([OpenApiModelKind]::Example, JoinClassName = '-')]
class AddressExamples {
    [OpenApiExample( Description = 'Sample address'  )]
    $Basic = @{ Street = '123 Main St'; City = 'Anytown'; PostalCode = '12345'; ApartmentNumber = 101 }

    [OpenApiExample( Description = 'Address without apartment'  )]
    $NoApt = @{ Street = '456 2nd Ave'; City = 'Metropolis'; PostalCode = '10001' }
}

# Request body components (member-level; default values become examples)
[OpenApiModelKind([OpenApiModelKind]::RequestBody, JoinClassName = '-')]
class AddressRequestBodies {
    # Required JSON body that references the Address schema
    [OpenApiRequestBody( Description = 'Create address payload', SchemaRef = 'Address', Required = $true )]
    $Create = @{ Street = '123 Main St'; City = 'Anytown'; PostalCode = '12345'; ApartmentNumber = 101 }

    # Optional JSON body; inline example from default
    [OpenApiRequestBody( Description = 'Partial update payload' )]
    $Patch = @{ City = 'Gotham' }
}

# Header components (member-level; default values become examples)
[OpenApiModelKind([OpenApiModelKind]::Header, JoinClassName = '-')]
class CommonHeaders {
    [OpenApiHeader( Description = 'Tenant identifier', Required = $true )]
    $TenantId = 'contoso'

    [OpenApiHeader( Description = 'Correlation id for tracing' )]
    $CorrelationId = 'abc-123'
}


<#
$schemaTypes = [Kestrun.OpenApi.OpenApiSchemaDiscovery]::GetOpenApiSchemaTypes()       # schemas
$parameterTypes = [Kestrun.OpenApi.OpenApiSchemaDiscovery]::GetOpenApiParameterTypes()  # parameters

# 2) Build extra component dictionaries (typed, not Hashtables)
$responses = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.IOpenApiResponse]]::new()
$parameters = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.IOpenApiParameter]]::new()
$pathItems = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.IOpenApiPathItem]]::new()

# 2a) Response: 200 that returns Address
$respOK = [Kestrun.OpenApi.OpenApiBuilders]::JsonResponseRef('Address', 'Address payload')
$responses.Add('AddressOk', $respOK)

# 2b) A reusable query parameter
$paramName = [Kestrun.OpenApi.OpenApiBuilders]::QueryParam('name', 'Filter by name')
$parameters.Add('name', $paramName)

# 2c) A reusable PathItem (OpenAPI v2.x types live under Microsoft.OpenApi.* root namespace)
$pi = [Microsoft.OpenApi.OpenApiPathItem]::new()
$pi.Description = 'Reusable stuff'
$pathItems.Add('ReusableThing', $pi)

# 3) Compose a single component set
$components = [Kestrun.OpenApi.OpenApiComponentSet]::new()
$components.SchemaTypes = [Type[]]($schemaTypes | ForEach-Object { [Type]$_ })
$components.ParameterTypes = [Type[]]($parameterTypes | ForEach-Object { [Type]$_ })

# Optional component maps
$components.ResponseTypes = $responses
$components.ParameterTypes = $parameters
$components.PathItemTypes = $pathItems#>
$components = [Kestrun.OpenApi.OpenApiSchemaDiscovery]::GetOpenApiTypesAuto()

# (You can also set: Examples, RequestBodies, Headers, SecuritySchemes, Links, Callbacks, Extensions)

# 4) Generate & serialize
$doc = [Kestrun.OpenApi.OpenApiV2Generator]::Generate($components, 'Kestrun API', '1.0.0')
$json = [Kestrun.OpenApi.OpenApiV2Generator]::ToJson($doc, $true)  # OpenAPI 3.1 JSON
$json | Set-Content -Encoding UTF8 -Path "$PSScriptRoot\openapi.json"

# 2. Create server host
$srv = New-KrServer -Name 'Lifecycle Demo' -PassThru

# 3. Add loopback listener on port 5000 (auto unlinks existing file if present)
# This listener will be used to demonstrate server limits configuration.
Add-KrEndpoint -Port $Port -IPAddress $IPAddress


# 6. Finalize configuration and set server limits
Enable-KrConfiguration

# 7. Map a simple info route to demonstrate server options in action
Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Health check'
    Write-KrJsonResponse -InputObject @{ status = 'healthy' }
}

# 8. Start the server (runs asynchronously; press Ctrl+C to stop)
Start-KrServer -Server $srv -NoWait



Write-KrLog -Level Information -Message 'Server started (non-blocking).'
Write-Host 'Server running for 30 seconds'
Start-Sleep 30

Stop-KrServer -Server $srv
Write-KrLog -Level Information -Message 'Server stopped.'
Close-KrLogger
