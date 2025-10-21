#using assembly C:\Users\mdaneri\Documents\GitHub\Kestrun\src\PowerShell\Kestrun\lib\net8.0\Kestrun.Annotations.dll
#using module 'C:\Users\m_dan\Documents\GitHub\kestrun\Kestrun\src\PowerShell\Kestrun\Kestrun.psd1'
<#
    15.1 Start / Stop Patterns
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

#Import-Module -Force 'C:\Users\m_dan\Documents\GitHub\kestrun\Kestrun\src\PowerShell\Kestrun\Kestrun.psm1'

New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
# Optional helpers for OpenAPI-friendly attributes
# No 'using namespace' needed—attributes are global



[OpenApiModelKind([OpenApiModelKind]::Schema)]
[OpenApiSchema(Required = 'Street')]
[OpenApiSchema(Required = 'City')]
[OpenApiSchema(Required = 'PostalCode')]
class Address {
    [OpenApiSchemaAttribute(Description = 'The street address'  , title = 'Street Address', Pattern = '^[\w\s\d]+$' , XmlName = 'StreetAddress', MaxLength = 100 )]
    [string]$Street = '123 Main St'

    [OpenApiSchemaAttribute(Description = 'The city name' , title = 'City Name', Pattern = '^[\w\s\d]+$' , XmlName = 'CityName' )]
    [string]$City = 'Anytown'

    [OpenApiSchemaAttribute(Description = 'The postal code' , title = 'Postal Code', Pattern = '^\d{5}(-\d{4})?$' , XmlName = 'PostalCode' )]
    [string]$PostalCode = '12345'

    [OpenApiSchemaAttribute(Description = 'The apartment number' , title = 'Apartment Number', Minimum = 1 , XmlName = 'AptNumber' )]
    [int]$ApartmentNumber = 101
}




[OpenApiModelKind([OpenApiModelKind]::Schema)]
[OpenApiSchema(Required = 'Name')]
class UserInfoResponse {
    [OpenApiSchema(Description = 'User identifier' )]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Id

    [OpenApiSchema(Description = 'Display name' )]
    [ValidateLength(1, 50)]
    [string]$Name

    [OpenApiSchema(Description = 'Age in years' )]
    [ValidateRange(0, 120)]
    [long]$Age

    [OpenApiSchema(Description = 'Counter' )]
    [nullable[long]]$Counter

    [OpenApiSchema(Description = 'Mailing address')]
    [Address]$Address

    [OpenApiSchema(Description = 'The country name')]
    [ValidateSet('USA', 'Canada', 'Mexico')]
    [string]$Country = 'USA'
}


# Parameters (class-first; one class per parameter; no Name= needed — property name is used)
[OpenApiModelKind([OpenApiModelKind]::Parameters)]
class Param_Name {
    [OpenApiParameter(In = [OaParameterLocation]::Query)]
    [OpenApiSchema(Description = 'Filter by name (contains)')]
    [string]$Name = 'John'
}

[OpenApiModelKind([OpenApiModelKind]::Parameters)]
class Param_MinAge {
    [OpenApiParameter(In = [OaParameterLocation]::Cookie)]
    [OpenApiSchema(Description = 'Minimum age', Minimum = 0)]
    [long]$MinAge = 20
}

[OpenApiModelKind([OpenApiModelKind]::Parameters)]
class Param_MaxAge {
    [OpenApiParameter(In = [OaParameterLocation]::Cookie)]
    [OpenApiSchema(Description = 'Maximum age', Minimum = 0)]
    [long]$MaxAge = 120
}
# Response components (member-level responses; JoinClassName ties class+member in key)
[OpenApiModelKind([OpenApiModelKind]::Response, JoinClassName = '-')]
class AddressResponse {
    [OpenApiResponse( Description = 'Successful retrieval of address', SchemaRef = 'Address' )]
    $OK
    [OpenApiResponse( Description = 'Address not found' )]
    $NotFound
}

# Example components (class-first; one class per example; defaults become the example object)
[OpenApiModelKind([OpenApiModelKind]::Example)]
class AddressExample_Basic {
    [string]$Street = '123 Main St'
    [string]$City = 'Anytown'
    [string]$PostalCode = '12345'
    [int]$ApartmentNumber = 101
}

[OpenApiModelKind([OpenApiModelKind]::Example)]
class AddressExample_NoApt {
    [string]$Street = '456 2nd Ave'
    [string]$City = 'Metropolis'
    [string]$PostalCode = '10001'
}

# Request body components (class-first; one class per request body; defaults become the example)
[OpenApiModelKind([OpenApiModelKind]::RequestBody, InlineSchema = $false)]
class CreateAddressBody {
    [OpenApiSchema(Description = 'The street address')]
    [string]$Street = '123 Main St'

    [OpenApiSchema(Description = 'The city name')]
    [string]$City = 'Anytown'

    [OpenApiSchema(Description = 'The postal code')]
    [string]$PostalCode = '12345'

    [OpenApiSchema(Description = 'The apartment number')]
    [int]$ApartmentNumber = 101

    [OpenApiSchema(Description = 'Additional mailing address')]
    [Address]$AdditionalAddress

    [OpenApiSchema(Description = 'The country name')]
    [string]$Country = 'USA'

    [OpenApiSchema(Description = 'The request identifier')]
    [guid]$RequestId = [guid]::NewGuid()

    [OpenApiSchema(Description = 'Seconds since epoch')]
    [long]$SecondsSinceEpoch = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

    [dateTime]$CreatedAt = [DateTime]::UtcNow
}

[OpenApiModelKind([OpenApiModelKind]::RequestBody)]
class PatchAddressBody {
    [string]$City = 'Gotham'
}

# Header components (member-level; default values become examples)
[OpenApiModelKind([OpenApiModelKind]::Header, JoinClassName = '-')]
class CommonHeaders {
    [OpenApiHeader( Description = 'Tenant identifier', Required = $true )]
    $TenantId = 'contoso'

    [OpenApiHeader( Description = 'Correlation id for tracing' )]
    $CorrelationId = 'abc-123'
}

# Link component via helper functions (preferred authoring)
$serverVars = (New-KrOpenApiServerVariable -Name 'env' -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name')

$linkServer = New-KrOpenApiServer -Url 'https://{env}.api.example.com' -Description 'Target API endpoint' -Variables $serverVars
$linkParams = @{ id = '$response.body#/id'; verbose = '$request.query.verbose' }
$linkRequestBody = @{ email = '$request.body#/email'; locale = '$request.body#/locale' }
$link_UserById = New-KrOpenApiLink -OperationRef '#/paths/~1users~1{id}/get' -OperationId 'getUserById' `
    -Description 'Link to fetch user details using the id from the response body.' -Parameters $linkParams -Server $linkServer -RequestBody $linkRequestBody



# Callback component (class-first). Discovered via [OpenApiModelKind(Callback)].
# The generator will emit components.callbacks["Callback_UserCreated"] with the expression as the key.
[OpenApiModelKind([OpenApiModelKind]::Callback)]
class Callback_UserCreated {
    # Expression pointing to a URL provided by the inbound request body
    [string]$Expression = '$request.body#/callbackUrl'
    # Optional description; used on the synthesized PathItem when none is provided
    [string]$Description = 'Callback invoked after a user is created.'
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


# (You can also set: Examples, RequestBodies, Headers, SecuritySchemes, Links, Callbacks, Extensions)




# 2. Create server host
$srv = New-KrServer -Name 'Lifecycle Demo' -PassThru

# 3. Add loopback listener on port 5000 (auto unlinks existing file if present)
# This listener will be used to demonstrate server limits configuration.
Add-KrEndpoint -Port $Port -IPAddress $IPAddress


# 6. Finalize configuration and set server limits
Enable-KrConfiguration


New-KrMapRouteBuilder  -Verbs @('GET', 'HEAD') -Pattern '/status' |
    Add-KrMapRouteScriptBlock -ScriptBlock {
        Write-KrLog -Level Debug -Message 'Health check'
        Write-KrJsonResponse -InputObject @{ status = 'healthy' }
    } |
    Add-KrMapRouteOpenApiInfo -Summary 'Health check endpoint' -Description 'Returns the health status of the service.'   |
     Add-KrMapRouteOpenApiInfo -Verbs 'GET' -OperationId 'GetStatus' |
    Add-KrMapRouteOpenApiServer -Server (New-KrOpenApiServer -Url 'https://api.example.com/v1' -Description 'Production Server') |
    Add-KrMapRouteOpenApiServer -Server (New-KrOpenApiServer -Url 'https://staging-api.example.com/v1' -Description 'Staging Server') |
    #Add-KrMapRouteOpenApiResponse -StatusCode '200' -Description 'Healthy status' |
    #  Add-KrMapRouteOpenApiResponse -StatusCode '503' -Description 'Service unavailable' |
    Build-KrMapRoute




# 4) Generate & serialize
$components = [Kestrun.OpenApi.OpenApiSchemaDiscovery]::GetOpenApiTypesAuto()
$doc = [Kestrun.OpenApi.OpenApiV2Generator]::Generate($components, $srv, 'Kestrun API', '1.0.0')
if ($null -eq $doc.Components.Links) {
    $doc.Components.Links = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.IOpenApiLink]]::new()
}
$doc.components.Links.Add('UserById', $link_UserById)
$cbKeys = if ($null -ne $doc.Components.Callbacks) { ($doc.Components.Callbacks.Keys -join ', ') } else { '' }
if ($cbKeys) { Write-Host "Discovered callback components: $cbKeys" }
$doc.Servers.Add((New-KrOpenApiServer -Description 'Development server' -Url 'https://dev.api.example.com'))

$json = [Kestrun.OpenApi.OpenApiV2Generator]::ToJson($doc, $true)  # OpenAPI 3.1 JSON

$yml = [Kestrun.OpenApi.OpenApiV2Generator]::ToYaml($doc, $true)  # OpenAPI 3.1 YAML

$yml | Set-Content -Encoding UTF8 -Path "$PSScriptRoot\openapi.yaml"
$json | Set-Content -Encoding UTF8 -Path "$PSScriptRoot\openapi.json"
# Helper: Project OpenApiPathItem to a PS-friendly view so ConvertTo-Json shows operations
function ConvertTo-OpenApiPathView {
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        $PathItem
    )
    process {
        $ops = @{}
        if ($null -ne $PathItem.Operations) {
            foreach ($entry in $PathItem.Operations.GetEnumerator()) {
                $key = ($entry.Key.ToString()).ToLowerInvariant()
                $ops[$key] = $entry.Value
            }
        }
        [pscustomobject]@{
            Summary = $PathItem.Summary
            Description = $PathItem.Description
            Operations = $ops
            Servers = $PathItem.Servers
            Parameters = $PathItem.Parameters
            Extensions = $PathItem.Extensions
        }
    }
}

# Preview the /status path with visible operations in JSON
if ($doc.Paths.ContainsKey('/status')) {
    $view = $doc.Paths['/status'] | ConvertTo-OpenApiPathView
    $view | ConvertTo-Json -Depth 10 | Write-Host
}
# 7. Map a simple info route to demonstrate server options in action
#Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
#   Write-KrLog -Level Debug -Message 'Health check'
#  Write-KrJsonResponse -InputObject @{ status = 'healthy' }
#}

# 8. Start the server (runs asynchronously; press Ctrl+C to stop)
Start-KrServer -Server $srv -NoWait



Write-KrLog -Level Information -Message 'Server started (non-blocking).'
Write-Host 'Server running for 30 seconds'
Start-Sleep 30

Stop-KrServer -Server $srv
Write-KrLog -Level Information -Message 'Server stopped.'
Close-KrLogger
