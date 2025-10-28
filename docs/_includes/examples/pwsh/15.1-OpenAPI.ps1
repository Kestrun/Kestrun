#using module 'Kestrun'

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module -Name Kestrun)) {
    Import-Module Kestrun
}
#Import-Module -Force 'C:\Users\m_dan\Documents\GitHub\kestrun\Kestrun\src\PowerShell\Kestrun\Kestrun.psm1'

New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
# Optional helpers for OpenAPI-friendly attributes
# No 'using namespace' needed—attributes are global



# 2. Create server host
$srv = New-KrServer -Name 'Lifecycle Demo' -PassThru

[OpenApiModelKindAttribute([OpenApiModelKind]::Schema)]
[OpenApiSchemaAttribute(Required = 'Street')]
[OpenApiSchemaAttribute(Required = 'City')]
[OpenApiSchemaAttribute(Required = 'PostalCode')]
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




[OpenApiModelKindAttribute([OpenApiModelKind]::Schema)]
[OpenApiSchemaAttribute(Required = 'Name')]
class UserInfoResponse {
    [OpenApiSchemaAttribute(Description = 'User identifier' )]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Id

    [OpenApiSchemaAttribute(Description = 'Display name' )]
    [ValidateLength(1, 50)]
    [string]$Name

    [OpenApiSchemaAttribute(Description = 'Age in years' )]
    [ValidateRange(0, 120)]
    [long]$Age

    [OpenApiSchemaAttribute(Description = 'Counter' )]
    [nullable[long]]$Counter

    [OpenApiSchemaAttribute(Description = 'Mailing address')]
    [Address]$Address

    [OpenApiSchemaAttribute(Description = 'The country name')]
    [ValidateSet('USA', 'Canada', 'Mexico')]
    [string]$Country = 'USA'
}


# Parameters (class-first; one class per parameter; no Name= needed — property name is used)
[OpenApiModelKindAttribute([OpenApiModelKind]::Parameters)]
class Param_Name {
    [OpenApiParameter(In = [OaParameterLocation]::Query)]
    [OpenApiSchema(Description = 'Filter by name (contains)')]
    [string]$Name = 'John'
}

# Age parameters (class-first; grouped parameters)
[OpenApiModelKindAttribute([OpenApiModelKind]::Parameters)]
class Param_Age {
    [OpenApiParameter(In = [OaParameterLocation]::Cookie)]
    [OpenApiSchema(Description = 'Minimum age', Minimum = 0)]
    [long]$MinAge = 20
    [OpenApiParameter(In = [OaParameterLocation]::Cookie)]
    [OpenApiSchema(Description = 'Maximum age', Minimum = 0)]
    [long]$MaxAge = 120
}

# Response components (member-level responses; JoinClassName ties class+member in key)
[OpenApiModelKindAttribute([OpenApiModelKind]::Response, JoinClassName = '-')]
class AddressResponse {
    [OpenApiResponseAttribute( Description = 'Successful retrieval of address' )]# ,ExampleRef = 'AddressExample_Basic')]
    [OpenApiExampleRefAttribute(key = 'Basic', refId = 'AddressExample_Basic')]
    [OpenApiHeaderRefAttribute( Key = 'X-Request-ID', RefId = 'X-Request-ID')]
    [OpenApiHeaderRefAttribute(Key = 'X-RateLimit-Remaining', RefId = 'X-RateLimit-Remaining')]
    [Address] $OK

    [OpenApiResponseAttribute( Description = 'Address not found' , ContentType = 'application/json')]
    [OpenApiContentTypeAttribute( ContentType = 'application/yaml')]
    [OpenApiContentTypeAttribute( ContentType = 'application/xml', SchemaRef = 'Address', Inline = $true)]
    [OpenApiContentTypeAttribute( ContentType = 'application/json')]
    [OpenApiLinkRefAttribute( Key = 'GetUserById', RefId = 'GetUserByIdLink')]
    [OpenApiLinkRefAttribute( Key = 'GetUserById2', RefId = 'GetUserByIdLink2')]
    [OpenApiExampleRefAttribute( contentType = 'application/json', key = 'NotFoundExample', refId = 'AddressExample_NoApt')]
    [OpenApiExampleRefAttribute( key = 'NotFoundExample', refId = 'AddressExample_NoApt' , Inline = $true)]
    [OpenApiHeaderRefAttribute( Key = 'X-Request-ID', RefId = 'X-Request-ID')]
    [OpenApiHeaderRefAttribute( Key = 'X-RateLimit-Remaining', RefId = 'X-RateLimit-Remaining')]
    [UserInfoResponse] $NotFound

    [OpenApiResponse( Description = 'Address not found'  )]
    $Error
}

# Example components (class-first; one class per example; defaults become the example object)
[OAExampleComponent(  Summary = 'Basic address example', Description = 'An example address with all fields populated.' )]
class AddressExample_Basic {
    [string]$Street = '123 Main St'
    [string]$City = 'Anytown'
    [string]$PostalCode = '12345'
    [int]$ApartmentNumber = 101
}
[OAExampleComponent( Name = 'WithApt', Summary = 'Address with apartment number', Description = 'An example address that includes an apartment number.' )]
class AddressExample_WithApt {
    [string]$Street = '789 Elm St'
    [string]$City = 'Springfield'
    [string]$PostalCode = '54321'
    [int]$ApartmentNumber = 202
}

#[OpenApiModelKindAttribute([OpenApiModelKind]::Example)]
[OAExampleComponent( Summary = 'Address without apartment number', Description = 'An example address that does not include an apartment number.' )]
class AddressExample_NoApt {
    [string]$Street = '456 2nd Ave'
    [string]$City = 'Metropolis'
    [string]$PostalCode = '10001'
}

# Request body components (class-first; one class per request body; defaults become the example)
[OpenApiModelKindAttribute([OpenApiModelKind]::RequestBody, Inline = $true)]
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

[OpenApiModelKindAttribute([OpenApiModelKind]::RequestBody)]
class PatchAddressBody {
    [string]$City = 'Gotham'
}

# Header components (member-level; default values become examples)
[OpenApiModelKindAttribute([OpenApiModelKind]::Header, JoinClassName = '-')]
class CommonHeaders {
    [OpenApiHeader( Description = 'Tenant identifier', Required = $true )]
    $TenantId = 'contoso'

    [OpenApiHeader( Description = 'Correlation id for tracing' )]
    [int] $CorrelationId = 12345
}

[OpenApiModelKindAttribute([OpenApiModelKind]::Header )]
class RateHeaders {
    [OpenApiHeader( Description = 'Correlation id for tracing', Name = 'X-Request-ID' )]
    [string] $xRequestId = 'abc-123'

    [OpenApiHeader( Description = 'Correlation id for tracing', Name = 'X-RateLimit-Remaining' )]
    [string] $xRateLimitRemaining = '10'
}

# Link component via helper functions (preferred authoring)
$serverVars = (New-KrOpenApiServerVariable -Name 'env' -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name')

Add-KrOpenApiServer -Url 'https://{env}.api.example.com' -Description 'Target API endpoint' -Variables $serverVars

$linkParams = @{ id = '$response.body#/id'; verbose = '$request.query.verbose' ; email = '$request.body#/email'; locale = '$request.body#/locale' }
$linkRequestBody = @{  locale = '$request.body#/locale' }
Add-KrOpenApiLink -LinkName 'GetUserByIdLink' -OperationRef '#/paths/users/{id}/get' -OperationId 'getUserById1' `
    -Description 'Link to fetch user details using the id from the response body.' -Parameters $linkParams -Server $linkServer #-RequestBody $linkRequestBody

Add-KrOpenApiLink -LinkName 'GetUserByIdLink2' -OperationRef '#/paths/users2/{id}/get' -OperationId 'getUserById2' `
    -Description 'Link to fetch user details using the id from the response body.' -RequestBody '$request.body#/locale'


# Callback component (class-first). Discovered via [OpenApiModelKindAttribute(Callback)].
# The generator will emit components.callbacks["Callback_UserCreated"] with the expression as the key.
[OpenApiModelKindAttribute([OpenApiModelKind]::Callback)]
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




# 3. Add loopback listener on port 5000 (auto unlinks existing file if present)
# This listener will be used to demonstrate server limits configuration.
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# 4. Add OpenAPI info
Add-KrOpenApiInfo -Title 'My API' -Version '1.0.0' -Description 'This is my API.' `
    -Summary 'A brief summary of my API.' -TermsOfService 'https://example.com/terms'
# 5. Add contact information
Add-KrOpenApiContact -Name 'John Doe' -Url 'https://johndoe.com' -Email 'john.doe@example.com'
# 6. Add license information
Add-KrOpenApiLicense -Name 'MIT License' -Url 'https://opensource.org/licenses/MIT' -Identifier 'MIT'


# Add a tag to the default document
Add-KrOpenApiTag -Name 'MyTag' -Description 'This is my tag.' `
    -ExternalDocs (New-KrOpenApiExternalDoc -Description 'More info' -Url 'https://example.com/tag-info')

Add-KrOpenApiExternalDoc -Description 'Find more info here' -Url 'https://example.com/docs'

# 7. Finalize configuration and set server limits
Enable-KrConfiguration

Add-KrSwaggerUiRoute

New-KrMapRouteBuilder -Verbs @('GET', 'HEAD', 'POST', 'TRACE') -Pattern '/status' |
    Add-KrMapRouteScriptBlock -ScriptBlock {
        Write-KrLog -Level Debug -Message 'Health check'
        Write-KrJsonResponse -InputObject @{ status = 'healthy' }
    } |
    Add-KrMapRouteOpenApiTag -Tag 'MyTag' |
    Add-KrMapRouteOpenApiInfo -Summary 'Health check endpoint' -Description 'Returns the health status of the service.' |
    Add-KrMapRouteOpenApiInfo -Verbs 'GET' -OperationId 'GetStatus' |
    Add-KrMapRouteOpenApiServer -Server (New-KrOpenApiServer -Url 'https://api.example.com/v1' -Description 'Production Server') |
    Add-KrMapRouteOpenApiServer -Server (New-KrOpenApiServer -Url 'https://staging-api.example.com/v1' -Description 'Staging Server') |
    Add-KrMapRouteOpenApiRequestBody -Verbs @('POST', 'GET', 'TRACE') -Description 'Healthy status2' -Reference 'CreateAddressBody' -Embed |
    Add-KrMapRouteOpenApiExternalDoc -Description 'Find more info here' -url 'https://example.com/docs' |
    Add-KrMapRouteOpenApiParameter -Verbs @('GET', 'HEAD', 'POST') -Reference 'Name' -Embed |
    # Add-KrMapRouteOpenApiResponse -StatusCode '200' -Description 'Healthy status' -ReferenceId 'Address' |
    Add-KrMapRouteOpenApiResponse -StatusCode '503' -Description 'Service unavailable' |
    Add-KrMapRouteOpenApiResponse -StatusCode '500' -ReferenceId 'AddressResponse-NotFound' |
    Build-KrMapRoute

Add-KrOpenApiRoute -Pattern '/openapi/{version}/openapi.{format}'

Add-KrMapRoute -Pattern '/openapi2/{version}/openapi.{format}' -Method 'GET' -ScriptBlock {
    $version = Get-KrRequestRouteParam -Name 'version' -AsString
    $format = Get-KrRequestRouteParam -Name 'format' -AsString
    $refresh = Get-KrRequestQuery -Name 'refresh' -AsBool
    # 3. Validate requested version and format
    try {
        $specVersion = [Kestrun.OpenApi.OpenApiSpecVersionExtensions]::FromString($version)
        if ($format -notin @('json', 'yaml')) {
            throw "Unsupported OpenAPI format requested: $format"
        }
    } catch {
        Write-KrLog -Level Warning -ErrorRecord $_ -Message 'Invalid OpenAPI version or format requested: {Version}, {Format}' -Values @($version, $format)
        Write-KrStatusResponse -StatusCode 404
        return
    }
    # 4. Build OpenAPI document
    if ($refresh) {
        Write-KrLog -Level Information -Message 'Refreshing OpenAPI document cache as requested.'
        # Invalidate cached document descriptors to force rebuild
        Build-KrOpenApiDocument
    }

    if ($format -eq 'yaml') {
        $yml = Export-KrOpenApiDocument -Format 'yaml' -Version $specVersion # OpenAPI 3.1 YAML
        Write-KrTextResponse -InputObject $yml -ContentType 'application/yaml'
        return
    } else {
        $json = Export-KrOpenApiDocument -Format 'json' -Version $specVersion # OpenAPI 3.1 JSON
        Write-KrTextResponse -InputObject $json -ContentType 'application/json'
    }
}

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# Preview the /status path with visible operations in JSON
#if ($doc.Paths.ContainsKey('/status')) {
#   $view = $doc.Paths['/status'] | ConvertTo-OpenApiPathView
#  $view | ConvertTo-Json -Depth 10 | Write-Host
#}
# 7. Map a simple info route to demonstrate server options in action
#Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
#   Write-KrLog -Level Debug -Message 'Health check'
#  Write-KrJsonResponse -InputObject @{ status = 'healthy' }
#}

# 8. Start the server (runs asynchronously; press Ctrl+C to stop)
Start-KrServer -Server $srv -CloseLogsOnExit #-NoWait

<#

Write-KrLog -Level Information -Message 'Server started (non-blocking).'
Write-Host 'Server running for 30 seconds'
Start-Sleep 30

Stop-KrServer -Server $srv
Write-KrLog -Level Information -Message 'Server stopped.'
Close-KrLogger
#>
