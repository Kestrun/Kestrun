#using module 'Kestrun'

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module -Name Kestrun)) {
    Import-Module Kestrun
}
#Import-Module -Force 'C:\Users\m_dan\Documents\GitHub\kestrun\Kestrun\src\PowerShell\Kestrun\Kestrun.psm1'

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
# Optional helpers for OpenAPI-friendly attributes
# No 'using namespace' needed—attributes are global

# 2. Create server host
$srv = New-KrServer -Name 'Lifecycle Demo' -PassThru

# Define OpenAPI schema components (class-first; one class per schema)
[OpenApiSchemaComponent(Description = 'Mailing address schema', Deprecated = $true)]
[OpenApiSchemaComponent(Required = 'Street')]
[OpenApiSchemaComponent(Required = 'City')]
[OpenApiSchemaComponent(Required = 'PostalCode')]
class Address {
    [OpenApiPropertyAttribute(Description = 'The street address'  , title = 'Street Address', Pattern = '^[\w\s\d]+$' , XmlName = 'StreetAddress', MaxLength = 100 )]
    [string]$Street = '123 Main St'

    [OpenApiPropertyAttribute(Description = 'The city name' , title = 'City Name', Pattern = '^[\w\s\d]+$' , XmlName = 'CityName' )]
    [string]$City = 'Anytown'

    [OpenApiPropertyAttribute(Description = 'The postal code' , title = 'Postal Code', Pattern = '^\d{5}(-\d{4})?$' , XmlName = 'PostalCode' )]
    [string]$PostalCode = '12345'

    [OpenApiPropertyAttribute(Description = 'The apartment number' , title = 'Apartment Number', Minimum = 1 , XmlName = 'AptNumber' )]
    [int]$ApartmentNumber = 101
}

[OpenApiSchemaComponent(Required = 'Name')]
class UserInfoResponse {
    [OpenApiPropertyAttribute(Description = 'User identifier' )]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Id

    [OpenApiPropertyAttribute(Description = 'Display name' )]
    [ValidateLength(1, 50)]
    [string]$Name

    [OpenApiPropertyAttribute(Description = 'Age in years' )]
    [ValidateRange(0, 120)]
    [long]$Age

    [OpenApiPropertyAttribute(Description = 'Counter' )]
    [nullable[long]]$Counter

    [OpenApiPropertyAttribute(Description = 'Mailing address')]
    [Address]$Address

    [OpenApiPropertyAttribute(Description = 'The country name')]
    [ValidateSet('USA', 'Canada', 'Mexico')]
    [string]$Country = 'USA'
}


# Parameters (class-first; one class per parameter; no Name= needed — property name is used)
[OpenApiParameterComponent()]
class Param_Name {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query)]
    [OpenApiPropertyAttribute(Description = 'Filter by name (contains)')]
    [string]$Name = 'John'
}

# Age parameters (class-first; grouped parameters)
[OpenApiParameterComponent( JoinClassName = '-')]
class Param_Age {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Cookie, Description = 'Minimum age parameter')]
    [long]$MinAge = 20
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Cookie, Description = 'Maximum age parameter')]
    [long]$MaxAge = 120
}
[OpenApiParameterComponent()]
class Param_Address {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Header, Description = 'Address parameter')]
    [OpenApiExampleRefAttribute(Key = 'Basic', ReferenceId = 'AddressExample_Basic')]
    [OpenApiExampleRefAttribute(Key = 'WithApt', ReferenceId = 'WithApt')]
    [OpenApiExampleRefAttribute(Key = 'NoApt', ReferenceId = 'AddressExample_NoApt')]
    [Address]$Param_Address

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Cookie, Description = 'Location parameter')]
    [OaParameterLocation]$Location = [OaParameterLocation]::Cookie

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Locations parameter')]
    [OaParameterLocation[]]$Locations = [OaParameterLocation]::Cookie

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'object array parameter')]
    [object[]]$ob = [OaParameterLocation]::Cookie

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'object')]
    [OpenApiPropertyAttribute(Description = 'An object parameter', AdditionalPropertiesAllowed = $true)]
    [object]$ob1
}

# Response components (member-level responses; JoinClassName ties class+member in key)
#[OpenApiModelKindAttribute([OpenApiModelKind]::Response, JoinClassName = '-')]
[OpenApiResponseComponent(JoinClassName = '-', Description = 'Response containing address information')]
class AddressResponse {
    [OpenApiResponseAttribute( Description = 'Successful retrieval of address' )]# ,ExampleRef = 'AddressExample_Basic')]
    [OpenApiExampleRefAttribute(Key = 'Basic', ReferenceId = 'AddressExample_Basic')]
    [OpenApiHeaderRefAttribute(Key = 'X-Request-ID', ReferenceId = 'X-Request-ID')]
    [OpenApiHeaderRefAttribute(Key = 'X-RateLimit-Remaining', ReferenceId = 'X-RateLimit-Remaining')]
    [Address] $OK

    [OpenApiResponseAttribute( Description = 'Address not found' , ContentType = 'application/json')]
    [OpenApiContentTypeAttribute( ContentType = 'application/yaml')]
    [OpenApiContentTypeAttribute( ContentType = 'application/xml', ReferenceId = 'Address', Inline = $true)]
    [OpenApiContentTypeAttribute( ContentType = 'application/json')]
    [OpenApiLinkRefAttribute( Key = 'GetUserById', ReferenceId = 'GetUserByIdLink')]
    [OpenApiLinkRefAttribute( Key = 'GetUserById2', ReferenceId = 'GetUserByIdLink2')]
    [OpenApiExampleRefAttribute( ContentType = 'application/json', Key = 'NotFoundExample', ReferenceId = 'AddressExample_NoApt')]
    [OpenApiExampleRefAttribute( Key = 'NotFoundExample', ReferenceId = 'AddressExample_NoApt' , Inline = $true)]
    [OpenApiHeaderRefAttribute( Key = 'X-Request-ID', ReferenceId = 'X-Request-ID')]
    [OpenApiHeaderRefAttribute( Key = 'X-RateLimit-Remaining', ReferenceId = 'X-RateLimit-Remaining')]
    [UserInfoResponse] $NotFound

    [OpenApiResponse()]
    $Error
}

# Example components (class-first; one class per example; defaults become the example object)
[OpenApiExampleComponent(  Summary = 'Basic address example', Description = 'An example address with all fields populated.' )]
class AddressExample_Basic {
    [string]$Street = '123 Main St'
    [string]$City = 'Anytown'
    [string]$PostalCode = '12345'
    [int]$ApartmentNumber = 101
}
[OpenApiExampleComponent( Key = 'WithApt', Summary = 'Address with apartment number', Description = 'An example address that includes an apartment number.' )]
class AddressExample_WithApt {
    [string]$Street = '789 Elm St'
    [string]$City = 'Springfield'
    [string]$PostalCode = '54321'
    [int]$ApartmentNumber = 202
}

#[OpenApiModelKindAttribute([OpenApiModelKind]::Example)]
[OpenApiExampleComponent( Summary = 'Address without apartment number', Description = 'An example address that does not include an apartment number.' )]
class AddressExample_NoApt {
    [string]$Street = '456 2nd Ave'
    [string]$City = 'Metropolis'
    [string]$PostalCode = '10001'
}

# Request body components (class-first; one class per request body; defaults become the example)
[OpenApiRequestBodyComponent( Description = 'Request body for creating an address', Inline = $true, Required = $true )]
[OpenApiRequestBodyComponent( ContentType = 'application/json' )]
[OpenApiRequestBodyComponent( ContentType = 'application/yaml' )]
[OpenApiRequestBodyComponent( ContentType = 'application/xml' )]
#[OpenApiExampleRefAttribute( Key = 'demo', ReferenceId = 'X-Request-ID', Inline = $true )]
class CreateAddressBody {
    [OpenApiPropertyAttribute(Description = 'The street address')]
    [string]$Street = '123 Main St'

    [OpenApiPropertyAttribute(Description = 'The city name')]
    [string]$City = 'Anytown'

    [OpenApiPropertyAttribute(Description = 'The postal code')]
    [string]$PostalCode = '12345'

    [OpenApiPropertyAttribute(Description = 'The apartment number')]
    [int]$ApartmentNumber = 101

    [OpenApiPropertyAttribute(Description = 'Additional mailing address')]
    [Address]$AdditionalAddress

    [OpenApiPropertyAttribute(Description = 'The country name')]
    [string]$Country = 'USA'

    [OpenApiPropertyAttribute(Description = 'The request identifier')]
    [guid]$RequestId = [guid]::NewGuid()

    [OpenApiPropertyAttribute(Description = 'Seconds since epoch')]
    [long]$SecondsSinceEpoch = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

    [dateTime]$CreatedAt = [DateTime]::UtcNow
}

[OpenApiRequestBodyComponent( Description = 'Request body for updating an address')]
class UpdateAddressBody {
    [string]$City = 'Gotham'
}

[OpenApiRequestBodyComponent( Description = 'Request body for creating a new user account', Inline = $false )]
[OpenApiExampleRefAttribute( Key = 'userDemo', ReferenceId = 'UserExample_Basic', Inline = $false )]
[OpenApiRequestBodyComponent( ContentType = 'application/yaml' )]
class CreateUserBody {
    [OpenApiPropertyAttribute(Description = 'The unique username for the account')]
    [string]$Username = 'jdoe'

    [OpenApiPropertyAttribute(Description = 'The email address associated with the account')]
    [string]$Email = 'jdoe@example.com'

    [OpenApiPropertyAttribute(Description = 'The password for the account (hashed or plaintext depending on policy)')]
    [string]$Password = 'P@ssw0rd123'

    [OpenApiPropertyAttribute(Description = 'The display name of the user')]
    [string]$FullName = 'John Doe'

    [OpenApiPropertyAttribute(Description = "The user's preferred locale code")]
    [string]$Locale = 'en-US'

    [OpenApiPropertyAttribute(Description = 'The date of birth of the user')]
    [datetime]$DateOfBirth = [datetime]'1990-01-01'

    [OpenApiPropertyAttribute(Description = 'Metadata for account creation')]
    [AccountMetadata]$Metadata

    [OpenApiPropertyAttribute(Description = 'The request correlation identifier')]
    [guid]$RequestId = [guid]::NewGuid()

    [OpenApiPropertyAttribute(Description = 'UNIX timestamp when the request was generated')]
    [long]$Timestamp = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

    [dateTime]$CreatedAt = [DateTime]::UtcNow
}

# Example of a nested metadata type used in CreateUserBody
[OpenApiSchemaComponent(Required = 'Name', Description = 'Additional metadata about the account creation')]
class AccountMetadata {
    [OpenApiPropertyAttribute(Description = 'Whether the account was created via API or UI')]
    [string]$Source = 'API'

    [OpenApiPropertyAttribute(Description = 'Client application version')]
    [string]$ClientVersion = '1.0.0'

    [OpenApiPropertyAttribute(Description = 'Indicates if email verification is required')]
    [bool]$RequiresVerification = $true
}


[OpenApiExampleComponent(
    Summary = 'Basic user creation example',
    Description = 'An example of a user registration request with common fields populated.'
)]
class UserExample_Basic {
    [string]$Username = 'jdoe'
    [string]$Email = 'jdoe@example.com'
    [string]$Password = 'P@ssw0rd123'
    [string]$FullName = 'John Doe'
    [string]$Locale = 'en-US'
    [datetime]$DateOfBirth = [datetime]'1990-01-01'
    [AccountMetadata]$Metadata = [AccountMetadata]@{
        Source = 'API'
        ClientVersion = '1.0.0'
        RequiresVerification = $true
    }
    [guid]$RequestId = [guid]'123e4567-e89b-12d3-a456-426614174000'
    [long]$Timestamp = 1730380000
    [datetime]$CreatedAt = [datetime]'2025-10-31T09:00:00Z'
}


[OpenApiExampleComponent( Summary = 'X-Request-ID header example', Description = 'An example X-Request-ID header value.', Key = 'X-Request-ID' )]
class XRequestID {
    [string] $Value = [guid]::NewGuid().ToString()
}

# Header components (member-level; default values become examples)
[OpenApiHeaderComponent (JoinClassName = '-')]
class CommonHeaders {
    [OpenApiHeaderAttribute(Key = 'X-Request-ID', Description = 'Unique request identifier for tracing')  ]
    [OpenApiExampleRefAttribute( Key = 'demo', ReferenceId = 'X-Request-ID' )]
    [OpenApiExampleAttribute(Key = 'demo2', Summary = 'Another example ID', Value = '12345678-90ab-cdef-1234-567890abcdef')]
    [string] $xRequestId = [guid]::NewGuid().ToString()

    [OpenApiHeader( Description = 'Tenant identifier', Required = $true )]
    $TenantId = 'contoso'

    [OpenApiHeader( Description = 'Correlation id for tracing' )]
    [int] $CorrelationId = 12345
}

[OpenApiHeaderComponent()]
class RateHeaders {
    [OpenApiHeader( Description = 'Correlation id for tracing', Key = 'X-Request-ID' )]
    [string] $xRequestId = 'abc-123'

    [OpenApiHeader( Description = 'Correlation id for tracing', Key = 'X-RateLimit-Remaining' )]
    [string] $xRateLimitRemaining = '10'
}

# Link component via helper functions (preferred authoring)
$serverVars = (New-KrOpenApiServerVariable -Name 'env' -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name')

Add-KrOpenApiServer -Url 'https://{env}.api.example.com' -Description 'Target API endpoint' -Variables $serverVars

#$linkParams = @{ id = '$response.body#/id'; verbose = '$request.query.verbose' ; email = '$request.body#/email'; locale = '$request.body#/locale' }
#$linkRequestBody = @{  locale = '$request.body#/locale' }
#Add-KrOpenApiLink -LinkName 'GetUserByIdLink' -OperationRef '#/paths/users/{id}/get' -OperationId 'getUserById1' `
#   -Description 'Link to fetch user details using the id from the response body.' -Parameters $linkParams -Server $linkServer #-RequestBody $linkRequestBody

#Add-KrOpenApiLink -LinkName 'GetUserByIdLink2' -OperationRef '#/paths/users2/{id}/get' -OperationId 'getUserById2' `
#  -Description 'Link to fetch user details using the id from the response body.' -RequestBody '$request.body#/locale'

[OpenApiLinkComponent()]
class MyLinks {
    [OpenApiLinkAttribute( Description = 'Link to fetch user details using the id from the response body.' )]
    [OpenApiLinkAttribute( OperationId = 'getUserById1'  )]
    [OpenApiLinkAttribute( MapKey = 'userid', MapValue = '$request.path.id' )]
    [OpenApiLinkAttribute(RequestBodyExpression = '$request.body#/profile')]
    $GetUserByIdLink

    [OpenApiLinkAttribute( Description = 'Link to fetch user details using the id from the response body.' )]
    [OpenApiLinkAttribute( OperationRef = 'https://na2.gigantic-server.com/#/paths/~12.0~1repositories~1%7Busername%7D/get' )]
    [OpenApiLinkAttribute( MapKey = 'id', MapValue = '$response.body#/id' )]
    $GetUserByIdLink2
}
<#
Add-KrBasicAuthentication -AuthenticationScheme 'PowershellBasic' -Realm 'Demo' -AllowInsecureHttp -ScriptBlock {
    param($Username, $Password)
    $Username -eq 'admin' -and $Password -eq 'password'
}
Add-KrApiKeyAuthentication -AuthenticationScheme 'ApiKeyCS' -AllowInsecureHttp -ApiKeyName 'X-Api-Key' -Code @'
    return providedKey == "my-secret-api-key";
'@ -In Query

New-KrCookieBuilder -Name 'KestrunAuth' -HttpOnly -SecurePolicy Always -SameSite Strict |
    Add-KrCookiesAuthentication -AuthenticationScheme 'Cookies' -LoginPath '/cookies/login' -LogoutPath '/cookies/logout' -AccessDeniedPath '/cookies/denied' `
        -SlidingExpiration -ExpireTimeSpan (New-TimeSpan -Minutes 30)
#>
if ((Test-Path -Path .\.env.json)) {
    & .\Utility\Import-EnvFile.ps1
    $GitHubClientId = $env:GITHUB_CLIENT_ID
    $GitHubClientSecret = $env:GITHUB_CLIENT_SECRET
    $OktaClientId = $env:OKTA_CLIENT_ID
    $OktaClientSecret = $env:OKTA_CLIENT_SECRET
    $OktaAuthority = $env:OKTA_AUTHORITY

    Add-KrGitHubAuthentication -AuthenticationScheme 'GitHub' -ClientId $GitHubClientId `
        -ClientSecret $GitHubClientSecret -CallbackPath '/signin-oauth'


    $claimPolicy = New-KrClaimPolicy |
        Add-KrClaimPolicy -PolicyName 'openid' -Scope -Description 'OpenID Connect scope' |
        Add-KrClaimPolicy -PolicyName 'profile' -Scope -Description 'Profile scope' |
        Build-KrClaimPolicy


    $options = [Kestrun.Authentication.OidcOptions]::new()

    # Map the 'name' claim from OIDC token to Identity.Name
    # Okta sends the email in the 'name' claim, which gets mapped to ClaimTypes.Name
    $options.TokenValidationParameters.NameClaimType = 'name'

    # 5) OAuth2 scheme (AUTH CHALLENGE) — signs into the 'Cookies' scheme above
    Add-KrOpenIdConnectAuthentication -AuthenticationScheme 'Okta' `
        -Authority $OktaAuthority `
        -ClientId $OktaClientId `
        -ClientSecret $OktaClientSecret `
        -CallbackPath '/signin-oidc' `
        -SignedOutCallbackPath '/signout-callback-oidc' `
        -SaveTokens `
        -UsePkce `
        -GetClaimsFromUserInfoEndpoint `
        -ClaimPolicy $claimPolicy `
        -Options $options
}

# 6. Build JWT configuration
$jwtBuilder = New-KrJWTBuilder |
    Add-KrJWTIssuer -Issuer 'KestrunApi' |
    Add-KrJWTAudience -Audience 'KestrunClients' |
    Protect-KrJWT -HexadecimalKey '6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0' -Algorithm HS256
$result = Build-KrJWT -Builder $jwtBuilder
$validation = $result | Get-KrJWTValidationParameter

# 7. Register bearer scheme
# Add-KrJWTBearerAuthentication -AuthenticationScheme 'Bearer' -ValidationParameter $validation -MapInboundClaims -SaveToken




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


# Add a tag to the default document with external documentation
Add-KrOpenApiTag -Name 'MyTag' -Description 'This is my tag.' `
    -ExternalDocs (New-KrOpenApiExternalDoc -Description 'More info' -Url 'https://example.com/tag-info')

Add-KrOpenApiExternalDoc -Description 'Find more info here' -Url 'https://example.com/docs'

# 7. Finalize configuration and set server limits
Enable-KrConfiguration

Add-KrApiDocumentationRoute

New-KrMapRouteBuilder -Verbs @('GET', 'HEAD', 'POST', 'TRACE') -Pattern '/status' |
    Add-KrMapRouteScriptBlock -ScriptBlock {
        Write-KrLog -Level Debug -Message 'Health check'
        Write-KrJsonResponse -InputObject @{ status = 'healthy' }
    } |
    Add-KrMapRouteOpenApiTag -Tag 'MyTag' |
    #Add-KrMapRouteAuthorization -Schema 'PowershellBasic', 'ApiKeyCS', 'Cookies' |
    Add-KrMapRouteOpenApiInfo -Summary 'Health check endpoint' -Description 'Returns the health status of the service.' |
    Add-KrMapRouteOpenApiInfo -Verbs 'GET' -OperationId 'GetStatus' |
    Add-KrMapRouteOpenApiServer -Server (New-KrOpenApiServer -Url 'https://api.example.com/v1' -Description 'Production Server') |
    Add-KrMapRouteOpenApiServer -Server (New-KrOpenApiServer -Url 'https://staging-api.example.com/v1' -Description 'Staging Server') |
    Add-KrMapRouteOpenApiRequestBody -Verbs @('POST', 'GET', 'TRACE') -Description 'Healthy status2' -Reference 'CreateAddressBody' -Embed -Force |
    Add-KrMapRouteOpenApiExternalDoc -Description 'Find more info here' -url 'https://example.com/docs' |
    Add-KrMapRouteOpenApiParameter -Verbs @('GET', 'HEAD', 'POST') -Reference 'Name' |
    Add-KrMapRouteOpenApiParameter -Verbs @(  'POST') -Reference 'Param_Address' |
    # Add-KrMapRouteOpenApiResponse -StatusCode '200' -Description 'Healthy status' -ReferenceId 'Address' |
    Add-KrMapRouteOpenApiResponse -StatusCode '503' -Description 'Service unavailable' |
    Add-KrMapRouteOpenApiResponse -StatusCode '500' -ReferenceId 'AddressResponse-NotFound' |
    Build-KrMapRoute



New-KrMapRouteBuilder -Verbs @('GET' ) -Pattern '/address' |
    Add-KrMapRouteScriptBlock -ScriptBlock {
        Write-KrLog -Level Debug -Message 'Health check'
        Write-KrJsonResponse -InputObject @{ status = 'healthy' }
    } |
    Add-KrMapRouteOpenApiTag -Tag 'MyTag' |
    Add-KrMapRouteAuthorization -Policy 'read:user' | #'openid' |
    Add-KrMapRouteOpenApiInfo -Summary 'Address info endpoint' -Description 'Returns information about a specific address.' |
    Add-KrMapRouteOpenApiInfo -OperationId 'GetAddressInfo' |
    Add-KrMapRouteOpenApiExternalDoc -Description 'Find more info here' -url 'https://example.com/docs' |
    Add-KrMapRouteOpenApiParameter -Reference 'Param_Address' |
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

<#
.SYNOPSIS
    Test divide operation with OpenAPI annotations.
.DESCRIPTION
    This function demonstrates the use of OpenAPI annotations to document an API operation.
    It performs a division operation and includes metadata for the OpenAPI specification.
.EXAMPLE
    TestDivide -number1 10 -number2 2
    Returns 5.
.PARAMETER number1
    The dividend number.
.PARAMETER number2
    The divisor number.
#>
function TestDivide {
    [OpenApiPath(HttpVerb = 'POST', Pattern = '/divide')]
    [OpenApiResponseRefAttribute( StatusCode = '200', ReferenceId = 'AddressResponse-OK' )]
    [OpenApiResponseAttribute( Description = 'Address not found' , ContentType = 'application/yaml' , SchemaRef = 'UserInfoResponse', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '404', ReferenceId = 'AddressResponse-NotFound' )]
    [OpenApiRequestBodyRefAttribute( ReferenceId = 'CreateAddressBody', Inline = $true )]
    param(
        [OpenApiParameterAttribute(In = 'Query', Description = 'The dividend number')]
        #   [OpenApiPropertyAttribute(Example = 10)]
        [int]$number1,
        [OpenApiParameterAttribute(In = 'Query', Description = 'The divisor number')]
        # [OpenApiPropertyAttribute(Example = 2)]
        [int]$number2
    )
    if ($number2 -eq 0) {
        throw 'Division by zero is not allowed.'
    }
    return $number1 / $number2
}

# 8. Build and test the OpenAPI document
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
