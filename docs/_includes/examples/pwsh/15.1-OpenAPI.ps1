
<#
    15.1 Start / Stop Patterns
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)


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

    [OpenApiSchema(Description = 'Age in years', Type = [OaSchemaType]::Integer, Minimum = 0, Maximum = 120)]
    [int]$Age

    [OpenApiSchema(Description = 'Mailing address')]
    [Address]$Address
}

[OpenApiModelKind([OpenApiModelKind]::Parameters)]
class UserListQueryParams {
    [OpenApiParameter(In = [OaParameterLocation]::Query, Name = 'name')]
    [OpenApiSchema(Description = 'Filter by name (contains)')]
    [string]$Name

    [OpenApiParameter(In = [OaParameterLocation]::Query, Name = 'minAge')]
    [OpenApiSchema(Description = 'Minimum age', Type = [OaSchemaType]::Integer, Format = 'int32', Minimum = 0)]
    [int]$MinAge
}

(New-KrOpenApiSchemasJson -Title 'Kestrun API' -Version '1.0.0') |
    Set-Content -Encoding UTF8 -Path "$PSScriptRoot\openapi.json"

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
