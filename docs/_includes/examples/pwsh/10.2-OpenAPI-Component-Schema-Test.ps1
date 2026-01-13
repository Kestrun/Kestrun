<#
    Sample: OpenAPI Component Schemas
    Purpose: Demonstrate reusable component schemas for request and response payloads.
    File:    10.2-OpenAPI-Component-Schema.ps1
    Notes:   Shows schema component definition with required fields, property attributes, and content type negotiation.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---
New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'OpenAPI Component Schema'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Component Schema API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable component schemas passed as parameters and returned as responses.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================
enum HeadColorType {
    Red
    Green
    Blue
    Black
    White
    Brown
    Blonde
}

[OpenApiSchemaComponent( Description = 'Daily operating hours for the museum.',
    RequiredProperties = ('date', 'timeOpen', 'timeClose'))]
class MuseumDailyHours {
    [OpenApiProperty(Description = 'Date the operating hours apply to.', Example = '2024-12-31')]
    [Date]$date

    [OpenApiProperty(Description = 'Time the museum opens on a specific date. Uses 24 hour time format (`HH:mm`).', Example = '09:00')]
    [ValidatePattern('^([01]\d|2[0-3]):([0-5]\d)$')]
    [string]$timeOpen

    [OpenApiProperty(Description = 'Time the museum closes on a specific date. Uses 24 hour time format (`HH:mm`).', Example = '18:00')]
    [ValidatePattern('^([01]\d|2[0-3]):([0-5]\d)$')]
    [string]$timeClose
}

[OpenApiSchemaComponent(
    Description = 'List of museum operating hours for consecutive days.',
    Array = $true
)]
class GetMuseumHoursResponse:MuseumDailyHours {}

[OpenApiSchemaComponent( Example = '2023-10-29')]
class Date:OpenApiDate {}

[OpenApiSchemaComponent(RequiredProperties = ('age', 'gender'))]
class PersonalityTrait {

    [OpenApiPropertyAttribute(Description = 'Colors of the user''s wigs', Example = ('Brown', 'Red'))]
    [HeadColorType[]]$wigsColors

    [OpenApiPropertyAttribute(Description = 'Alternative Emails', Example = ('john.doe@example.com', 'jane.doe@example.com'))]
    [OpenApiEmail[]]$alternativeEmails

    [OpenApiPropertyAttribute(Description = 'Color of the user''s hair', Example = 'Brown')]
    [HeadColorType]$hairColor

    [OpenApiPropertyAttribute(Description = 'User date of birth', Example = '1990-11-30')]
    [OpenApiDate]$dateOfBirth

    [OpenApiPropertyAttribute(Description = 'List of user interests', Example = ('sports', 'music'))]
    [string[]]$interests

    [OpenApiPropertyAttribute(Description = 'Gender of the user', Enum = ('male', 'female', 'other'), Example = 'male')]
    [string]$gender

    [OpenApiPropertyAttribute(Description = 'User age', Minimum = 0, Maximum = 150, Example = 30)]
    [int]$age


}
# Request schema: User input for creating a user
[OpenApiSchemaComponent(RequiredProperties = ('firstName', 'lastName', 'email'))]
class CreateUserRequest:PersonalityTrait {

    [OpenApiPropertyAttribute(Description = 'First name of the user', Example = 'John')]
    [string]$firstName

    [OpenApiPropertyAttribute(Description = 'Last name of the user', Example = 'Doe')]
    [string]$lastName

    [OpenApiPropertyAttribute(Description = 'Email address', Example = 'john.doe@example.com')]
    [OpenApiEmail]$email

    [OpenApiPropertyAttribute(Description = 'Subordinate user information' )]
    [CreateUserRequest[]]$subordinates
}

# Response schema: User data returned from server
[OpenApiSchemaComponent(RequiredProperties = ('id'))]
class UserResponse:CreateUserRequest {
    [OpenApiPropertyAttribute(Description = 'Unique user identifier', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
    [OpenApiUuid]$id

    [OpenApiPropertyAttribute(Description = 'ISO 8601 creation timestamp', Format = 'date-time')]
    [string]$createdAt
}

#[OpenApiSchemaComponent(Description = 'List of user object',    Array = $true)]
#class UserArray:CreateUserRequest {}

[OpenApiSchemaComponent(Description = 'List of user object',RequiredProperties = ('contractId'), Array = $true)]
class UserArray2:CreateUserRequest {
    [OpenApiPropertyAttribute(Description = 'The contract identifier', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
    [OpenApiUuid]$contractId
}

[OpenApiRequestBodyComponent(Description = 'List of user object', Required = $true, ContentType = 'application/json' , Array = $true)]
class UserArrayRequest:CreateUserRequest {}
[OpenApiRequestBodyComponent(Description = 'List of user object', Required = $true, ContentType = 'application/json' , Array = $true)]
class UserArrayRequest2:CreateUserRequest {
    [OpenApiPropertyAttribute(Description = 'The contract identifier', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
    [OpenApiUuid]$contractId
}
<#
[OpenApiSchemaComponent(Description = 'List of user responses', Array = $true)]
class UserResponseArray:UserResponse {}

[OpenApiSchemaComponent(
    Description = 'Type of ticket being purchased. Use `general` for regular entry and `event` for special events.',
    Enum = ('event', 'general'), Example = 'event')]
class TicketType:OpenApiUuid {}

[OpenApiSchemaComponent(
    Description = 'Unique identifier for museum ticket. Generated when purchased.',
    Format = 'uuid', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
class TicketId:OpenApiString {}


[OpenApiSchemaComponent(Description = 'Price of a ticket for the special event',
    Format = 'float', Example = 25)]
class EventPrice:OpenApiNumber {}


[OpenApiSchemaComponent(Description = 'Prices for special event tickets', Array = $true)]
class EventPrices:EventPrice {}
#>
# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# POST endpoint: Accept CreateUserRequest, return UserResponse
<#
.SYNOPSIS
    Create a new user.
.DESCRIPTION
    Accepts user information and returns the created user with an assigned ID.
.PARAMETER body
    User creation request payload
#>
function createUser {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/users')]
    # [OpenApiResponse(StatusCode = '201', Description = 'User created successfully', Schema = [UserResponse], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/yaml', 'application/x-www-form-urlencoded'))]
        [CreateUserRequest]$body
    )

    # Simple validation
    if (-not $body.firstName -or -not $body.lastName -or -not $body.email) {
        Write-KrJsonResponse @{error = 'firstName, lastName, and email are required' } -StatusCode 400
        return
    }

    # Create response
    $response = @{
        id = 1
        firstName = $body.firstName
        lastName = $body.lastName
        email = $body.email
        age = $body.age -as [int]
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $response -StatusCode 201
}

# GET endpoint: Return a user by ID as UserResponse
<#
.SYNOPSIS
    Get user by ID.
.DESCRIPTION
    Retrieves a user resource by its identifier.
.PARAMETER userId
    The user ID to retrieve
#>
<#
function getUser {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/users/{userId}')]
    [OpenApiResponse(StatusCode = '200', Description = 'User found', Schema = [UserResponse], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponse(StatusCode = '404', Description = 'User not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [int]$userId
    )

    # Mock user data
    [UserResponse]$response = [UserResponse]::new()
    $response.id = $userId
    $response.firstName = 'John'
    $response.lastName = 'Doe'
    $response.email = 'john.doe@example.com'
    $response.age = 30
    $response.createdAt = (Get-Date).AddDays(-1).ToUniversalTime().ToString('o')

    Write-KrResponse $response -StatusCode 200
}
#>
# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================


Start-KrServer -CloseLogsOnExit
