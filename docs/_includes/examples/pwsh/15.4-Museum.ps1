param(
    [int]$Port = 5001,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'Redocly Museum API' -PassThru

# =========================================================
#                 TOP-LEVEL OPENAPI (3.1.0)
# =========================================================

Add-KrOpenApiInfo -Title 'Redocly Museum API' `
    -Version '1.0.0' `
    -Description @'
An imaginary, but delightful Museum API for interacting with museum services and information.
Built with love by Redocly.
'@

Add-KrOpenApiContact -Email 'team@redocly.com' -Url 'https://redocly.com/docs/cli/'
Add-KrOpenApiLicense -Name 'MIT' -Url 'https://opensource.org/license/mit/'
Add-KrOpenApiServer -Url 'https://api.fake-museum-example.com/v1'

# TODO: info.x-logo is not modeled yet (url + altText). Add an extension attribute when available.

# Tags
Add-KrOpenApiTag -Name 'Operations' -Description 'Operational information about the museum.'  # x-displayName: About the museum
Add-KrOpenApiTag -Name 'Events' -Description 'Special events hosted by the Museum.'      # x-displayName: Upcoming events
Add-KrOpenApiTag -Name 'Tickets' -Description 'Museum tickets for general entrance or special events.' # x-displayName: Buy tickets

# TODO: x-tagGroups (Plan your visit / Purchases) not modeled yet. Add tag-group extension support later.

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

<#
.SYNOPSIS
    Daily operating hours for the museum.
.DESCRIPTION
    Mirrors components.schemas.MuseumDailyHours.
.PARAMETER date
    Date the operating hours apply to (YYYY-MM-DD).
.PARAMETER timeOpen
    Time the museum opens on a specific date (HH:mm, 24h).
.PARAMETER timeClose
    Time the museum closes on a specific date (HH:mm, 24h).
#>
[OpenApiSchemaComponent(Description = 'Daily operating hours for the museum.', Required = 'date,timeOpen,timeClose')]
class MuseumDailyHours {
    [datetime]$date


    [ValidatePattern('^([01]\d|2[0-3]):([0-5]\d)$')]
    [string]$timeOpen


    [ValidatePattern('^([01]\d|2[0-3]):([0-5]\d)$')]
    [string]$timeClose
}

<#
.SYNOPSIS
    List of museum operating hours for consecutive days.
.DESCRIPTION
    This approximates components.schemas.GetMuseumHoursResponse, which in the original spec is:
    type: array, items: MuseumDailyHours
#>
[OpenApiSchemaComponent(Description = 'List of museum operating hours for consecutive days.')]
class GetMuseumHoursResponse {
    # TODO: original schema is a bare array; here we wrap it in an object with "items".
    [MuseumDailyHours[]]$items
}

<#
.SYNOPSIS
    Request payload for creating new special events at the museum.
.DESCRIPTION
    Mirrors components.schemas.CreateSpecialEventRequest.
.PARAMETER name
    Name of the special event.
.PARAMETER location
    Location where the special event is held.
.PARAMETER eventDescription
    Description of the special event.
.PARAMETER dates
    List of planned dates for the special event (YYYY-MM-DD).
.PARAMETER price
    Price of a ticket for the special event.
#>
[OpenApiSchemaComponent(Description = 'Request payload for creating new special events at the museum.',
    Required = 'name,location,eventDescription,dates,price')]
class CreateSpecialEventRequest {
    [string]$name
    [string]$location
    [string]$eventDescription

    [OpenApiPropertyAttribute(Array = $true, Format = 'date')]
    [string[]]$dates

    [double]$price  # format: float
}

<#
.SYNOPSIS
    Request payload for updating an existing special event.
.DESCRIPTION
    Mirrors components.schemas.UpdateSpecialEventRequest.
    Only included fields are updated in the event.
.PARAMETER name
    New name of the special event.
.PARAMETER location
    New location where the special event is held.
.PARAMETER eventDescription
    New description of the special event.
.PARAMETER dates
    New list of planned dates for the special event.
.PARAMETER price
    New price of a ticket for the special event.
#>
[OpenApiSchemaComponent(Description = 'Request payload for updating an existing special event. Only included fields are updated.')]
class UpdateSpecialEventRequest {
    [string]$name
    [string]$location
    [string]$eventDescription

    [OpenApiPropertyAttribute(Array = $true, Format = 'date')]
    [string[]]$dates

    [double]$price
}

<#
.SYNOPSIS
    Information about a special event.
.DESCRIPTION
    Mirrors components.schemas.SpecialEventResponse.
.PARAMETER eventId
    Identifier for a special event (UUID).
.PARAMETER name
    Name of the special event.
.PARAMETER location
    Location where the special event is held.
.PARAMETER eventDescription
    Description of the special event.
.PARAMETER dates
    List of planned dates for the special event.
.PARAMETER price
    Price of a ticket for the special event.
#>
[OpenApiSchemaComponent(Description = 'Information about a special event.',
    Required = 'eventId,name,location,eventDescription,dates,price')]
class SpecialEventResponse {
    [OpenApiPropertyAttribute(Format = 'uuid')]
    [string]$eventId

    [string]$name
    [string]$location
    [string]$eventDescription

    [OpenApiPropertyAttribute(Array = $true, Format = 'date')]
    [string[]]$dates

    [double]$price
}

<#
.SYNOPSIS
    A list of upcoming special events.
.DESCRIPTION
    Mirrors components.schemas.ListSpecialEventsResponse (array of SpecialEventResponse).
#>
[OpenApiSchemaComponent(Description = 'A list of upcoming special events.')]
class ListSpecialEventsResponse {
    # TODO: original schema is a bare array; here we wrap it in an object with "items".
    [OpenApiPropertyAttribute(Array = $true)]
    [SpecialEventResponse[]]$items
}

<#
.SYNOPSIS
    Type of ticket being purchased.
.DESCRIPTION
    Mirrors components.schemas.TicketType (string enum).
.PARAMETER ticketType
    Type of ticket being purchased (general or event).
#>
[OpenApiSchemaComponent(Description = 'Type of ticket being purchased. Use `general` for regular entry and `event` for special events.')]
class TicketTypeWrapper {
    # TODO: original schema is a bare string enum; here we wrap it in an object.
    [ValidateSet('event', 'general')]
    [string]$ticketType
}

<#
.SYNOPSIS
    Request payload used for purchasing museum tickets.
.DESCRIPTION
    Mirrors components.schemas.BuyMuseumTicketsRequest.
.PARAMETER ticketType
    Type of ticket being purchased (general or event).
.PARAMETER eventId
    Unique identifier for a special event (required when ticketType is event).
.PARAMETER ticketDate
    Date that the ticket is valid for (YYYY-MM-DD).
.PARAMETER email
    Email address for ticket purchaser.
.PARAMETER phone
    Phone number for the ticket purchaser (optional).
#>
[OpenApiSchemaComponent(Description = 'Request payload used for purchasing museum tickets.',
    Required = 'ticketType,ticketDate,email')]
class BuyMuseumTicketsRequest {
    [ValidateSet('event', 'general')]
    [string]$ticketType

    [OpenApiPropertyAttribute(Format = 'uuid')]
    [string]$eventId

    [OpenApiPropertyAttribute(Format = 'date')]
    [string]$ticketDate

    [OpenApiPropertyAttribute(Format = 'email')]
    [string]$email

    [string]$phone
}

<#
.SYNOPSIS
    Details for a museum ticket after a successful purchase.
.DESCRIPTION
    Mirrors components.schemas.BuyMuseumTicketsResponse.
.PARAMETER message
    Confirmation message after ticket purchase.
.PARAMETER eventName
    Name of the special event (for event tickets).
.PARAMETER ticketId
    Unique identifier for the museum ticket (UUID).
.PARAMETER ticketType
    Type of ticket (general or event).
.PARAMETER ticketDate
    Date the ticket is valid for (YYYY-MM-DD).
.PARAMETER confirmationCode
    Unique confirmation code used to verify ticket purchase.
#>
[OpenApiSchemaComponent(Description = 'Details for a museum ticket after a successful purchase.',
    Required = 'message,ticketId,ticketType,ticketDate,confirmationCode')]
class BuyMuseumTicketsResponse {
    [string]$message
    [string]$eventName

    [OpenApiPropertyAttribute(Format = 'uuid')]
    [string]$ticketId

    [ValidateSet('event', 'general')]
    [string]$ticketType

    [OpenApiPropertyAttribute(Format = 'date')]
    [string]$ticketDate

    [string]$confirmationCode
}


# TODO: components.schemas.GetTicketCodeResponse is a binary string (image).
#       Kestrun does not yet support bare string/array schema components referenced by $ref.
#       Here we model it as a class with no properties, and handle the binary response in
<#
.SYNOPSIS
    Scannable ticket with QR code.
.DESCRIPTION
    Mirrors components.schemas.GetTicketCodeResponse (type: string, format: binary).
#>
[OpenApiSchemaComponent(
    Description = 'An image of a ticket with a QR code used for museum or event entry.')]
#[OpenApiParameterAttribute(Type = 'string', Format = 'binary' )]
class GetTicketCodeResponse {
}

# TODO: Alias-style schemas (Date, Email, Phone, TicketId, TicketMessage, TicketConfirmation, EventId, EventName, EventLocation, EventDescription, EventDates, EventPrice)
#       are not modeled as separate schema components here. They are flattened into properties.
#       When Kestrun supports bare string/array schema components referenced by $ref, you can introduce them explicitly.

# =========================================================
#                 COMPONENT PARAMETERS
# =========================================================

# These model components.parameters from museum.yml.
# NOTE: we approximate with a class + property decorated as a parameter.
#       The ReferenceId used by OpenApiParameterRefAttribute matches the class name.

[OpenApiParameterComponent()]
class PaginationPage {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = 'The page number to retrieve.')]
    [int]$page
}

[OpenApiParameterComponent()]
class PaginationLimit {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = 'The number of days per page.')]
    [int]$limit
}

[OpenApiParameterComponent()]
class EventId {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true,
        Description = 'An identifier for a special event.')]
    [OpenApiPropertyAttribute(Format = 'uuid')]
    [string]$eventId
}

[OpenApiParameterComponent()]
class StartDate {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = "The starting date to retrieve future operating hours from. Defaults to today's date.")]
    [datetime]$startDate
}

[OpenApiParameterComponent()]
class EndDate {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = 'The end of a date range to retrieve special events for. Defaults to 7 days after startDate.')]
    [OpenApiPropertyAttribute(Format = 'date')]
    [string]$endDate
}

[OpenApiParameterComponent()]
class TicketId {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true,
        Description = 'An identifier for a ticket to a museum event. Used to generate ticket image.')]
    [OpenApiPropertyAttribute(Format = 'uuid')]
    [string]$ticketId
}

# =========================================================
#                 SECURITY SCHEMES
# =========================================================

# components.securitySchemes.MuseumPlaceholderAuth:
#   type: http
#   scheme: basic

Add-KrBasicAuthentication -AuthenticationScheme 'MuseumPlaceholderAuth' -AllowInsecureHttp -ScriptBlock {
    param($username, $password)

    # Placeholder authentication logic.
    # In a real implementation, validate username/password against a user store.
    if ($username -eq 'guest' -and $password -eq 'guestpass') {
        return $true
    } else {
        return $false
    }
}

# TODO: museum.yml defines global security:
#   security:
#     - MuseumPlaceholderAuth: []
# and overrides with "security: []" for /special-events (post/get).
# Kestrun mapping for per-operation security override is not used here.
# When available, decorate createSpecialEvent/listSpecialEvents with an attribute to disable auth.

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================
Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc
Add-KrApiDocumentationRoute -DocumentType Scalar
Add-KrApiDocumentationRoute -DocumentType Rapidoc
Add-KrApiDocumentationRoute -DocumentType Elements

# --------------------------------------
# GET /museum-hours
# --------------------------------------
<#
.SYNOPSIS
    Get museum hours.
.DESCRIPTION
    Get upcoming museum operating hours.
.PARAMETER startDate
    The starting date to retrieve future operating hours from. Defaults to today's date.
.PARAMETER page
    The page number to retrieve.
.PARAMETER limit
    The number of days per page.
#>
function getMuseumHours {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/museum-hours', Tags = 'Operations')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        SchemaRef = 'GetMuseumHoursResponse', Inline = $true,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]
    # TODO: 400/404 responses are inline in museum.yml; you could introduce response components and use OpenApiResponseRefAttribute.

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'StartDate')]
        [datetime]$startDate,

        [OpenApiParameterRefAttribute(ReferenceId = 'PaginationPage')]
        [int]$page = 1,

        [OpenApiParameterRefAttribute(ReferenceId = 'PaginationLimit')]
        [int]$limit = 10
    )

    Write-Host "getMuseumHours called startDate='$startDate' page='$page' limit='$limit'"

    # Dummy payload approximating GetMuseumHoursResponse (wrapped object with items[]).
    $hours = @(
        [MuseumDailyHours]@{
            date = (if ($startDate) { $startDate } else { (Get-Date).ToString('yyyy-MM-dd') })
            timeOpen = '09:00'
            timeClose = '18:00'
        }
    )
    $resp = [GetMuseumHoursResponse]::new()
    $resp.items = $hours
    Write-KrJsonResponse $resp -StatusCode 200
}

# --------------------------------------
# /special-events (POST Create, GET List)
# --------------------------------------

<#
.SYNOPSIS
    Create special event.
.DESCRIPTION
    Create a new special event at the museum.
.PARAMETER Body
    Request payload describing the special event to create.
#>
function createSpecialEvent {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/special-events', Tags = 'Events')]
    # TODO: museum.yml sets security: [] here (no auth); add per-operation override when supported.

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'success',
        SchemaRef = 'SpecialEventResponse', Inline = $true,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiRequestBodyAttribute(Required = $true,
            ContentType = 'application/json')]
        [CreateSpecialEventRequest]$Body
    )

    Write-Host 'createSpecialEvent called with body:'
    $Body | ConvertTo-Json -Depth 5 | Write-Host

    $resp = [SpecialEventResponse]::new()
    $resp.eventId = [Guid]::NewGuid().ToString()
    $resp.name = $Body.name
    $resp.location = $Body.location
    $resp.eventDescription = $Body.eventDescription
    $resp.dates = $Body.dates
    $resp.price = $Body.price

    Write-KrJsonResponse $resp -StatusCode 200
}

<#
.SYNOPSIS
    List special events.
.DESCRIPTION
    Return a list of upcoming special events at the museum.
.PARAMETER startDate
    The starting date to retrieve future operating hours from. Defaults to today's date.
.PARAMETER endDate
    The end of a date range to retrieve special events for. Defaults to 7 days after startDate.
.PARAMETER page
    The page number to retrieve.
.PARAMETER limit
    The number of days per page.
#>
function listSpecialEvents {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/special-events', Tags = 'Events')]
    # TODO: museum.yml sets security: [] here (no auth); add per-operation override when supported.

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        SchemaRef = 'ListSpecialEventsResponse', Inline = $true,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'StartDate')]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query)]
        [OpenApiPropertyAttribute(Format = 'date')]
        [string]$startDate,

        [OpenApiParameterRefAttribute(ReferenceId = 'EndDate')]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query)]
        [OpenApiPropertyAttribute(Format = 'date')]
        [string]$endDate,

        [OpenApiParameterRefAttribute(ReferenceId = 'PaginationPage')]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query)]
        [int]$page = 1,

        [OpenApiParameterRefAttribute(ReferenceId = 'PaginationLimit')]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query)]
        [int]$limit = 10
    )

    Write-Host "listSpecialEvents called startDate='$startDate' endDate='$endDate' page='$page' limit='$limit'"

    $event = [SpecialEventResponse]::new()
    $event.eventId = [Guid]::NewGuid().ToString()
    $event.name = 'Sample Event'
    $event.location = 'Main Hall'
    $event.eventDescription = 'Sample special event description.'
    $event.dates = @((Get-Date).ToString('yyyy-MM-dd'))
    $event.price = 25

    $resp = [ListSpecialEventsResponse]::new()
    $resp.items = @($event)

    Write-KrJsonResponse $resp -StatusCode 200
}

# --------------------------------------
# /special-events/{eventId}
#   GET getSpecialEvent
#   PATCH updateSpecialEvent
#   DELETE deleteSpecialEvent
# --------------------------------------

<#
.SYNOPSIS
    Get special event.
.DESCRIPTION
    Get details about a special event.
.PARAMETER eventId
    Identifier for a special event (UUID).
#>
function getSpecialEvent {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/special-events/{eventId}', Tags = 'Events')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        SchemaRef = 'SpecialEventResponse', Inline = $true,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'EventId')]
        [Guid]$eventId
    )

    Write-Host "getSpecialEvent called eventId='$eventId'"

    $event = [SpecialEventResponse]::new()
    $event.eventId = $eventId
    $event.name = 'Time Traveler Tea Party'
    $event.location = 'Temporal Tearoom'
    $event.eventDescription = 'Sip tea with important historical figures.'
    $event.dates = @('2023-11-18', '2023-11-25', '2023-12-02')
    $event.price = 60

    Write-KrJsonResponse $event -StatusCode 200
}

<#
.SYNOPSIS
    Update special event.
.DESCRIPTION
    Update the details of a special event.
.PARAMETER eventId
    Identifier for a special event (UUID).
.PARAMETER Body
    Request payload with changes to apply to the special event.
#>
function updateSpecialEvent {
    [OpenApiPath(HttpVerb = 'patch', Pattern = '/special-events/{eventId}', Tags = 'Events')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        Schema = [SpecialEventResponse],
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'EventId')]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true)]
        [OpenApiPropertyAttribute(Format = 'uuid')]
        [string]$eventId,

        [OpenApiRequestBodyAttribute(Required = $true,
            ContentType = 'application/json')]
        [UpdateSpecialEventRequest]$Body
    )

    Write-Host "updateSpecialEvent called eventId='$eventId' body:"
    $Body | ConvertTo-Json -Depth 5 | Write-Host

    $event = [SpecialEventResponse]::new()
    $event.eventId = $eventId
    $event.name = $Body.name
    $event.location = $Body.location
    $event.eventDescription = $Body.eventDescription
    $event.dates = $Body.dates
    $event.price = $Body.price

    Write-KrJsonResponse $event -StatusCode 200
}

<#
.SYNOPSIS
    Delete special event.
.DESCRIPTION
    Delete a special event from the collection. Allows museum to cancel planned events.
.PARAMETER eventId
    Identifier for a special event (UUID).
#>
function deleteSpecialEvent {
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/special-events/{eventId}', Tags = 'Events')]

    [OpenApiResponseAttribute(StatusCode = '204', Description = 'Success - no content')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '401', Description = 'Unauthorized')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]
    # TODO: consider introducing ResponseComponents for 400/401/404 and using OpenApiResponseRefAttribute.

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'EventId')]
        [Guid]$eventId
    )

    Write-Host "deleteSpecialEvent called eventId='$eventId'"
    Write-KrStatusResponse -StatusCode 204
}

# --------------------------------------
# POST /tickets
# --------------------------------------
<#
.SYNOPSIS
    Buy museum tickets.
.DESCRIPTION
    Purchase museum tickets for general entry or special events.
.PARAMETER Body
    Request payload describing the ticket purchase.
#>
function buyMuseumTickets {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/tickets', Tags = 'Tickets')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        SchemaRef = 'BuyMuseumTicketsResponse', Inline = $true,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiRequestBodyAttribute(Required = $true,
            ContentType = 'application/json')]
        [BuyMuseumTicketsRequest]$Body
    )

    Write-Host 'buyMuseumTickets called with body:'
    $Body | ConvertTo-Json -Depth 5 | Write-Host

    $resp = [BuyMuseumTicketsResponse]::new()
    $resp.message = if ($Body.ticketType -eq 'event') {
        'Museum special event ticket purchased'
    } else {
        'Museum general entry ticket purchased'
    }

    $resp.eventName = if ($Body.ticketType -eq 'event') {
        'Mermaid Treasure Identification and Analysis'
    } else {
        $null
    }

    $resp.ticketId = [Guid]::NewGuid().ToString()
    $resp.ticketType = $Body.ticketType
    $resp.ticketDate = $Body.ticketDate
    $resp.confirmationCode = "ticket-$($Body.ticketType)-" + ([Guid]::NewGuid().ToString().Substring(0, 8))

    Write-KrJsonResponse $resp -StatusCode 200
}

# --------------------------------------
# GET /tickets/{ticketId}/qr
# --------------------------------------
<#
.SYNOPSIS
    Get ticket QR code.
.DESCRIPTION
    Return an image of your ticket with scannable QR code. Used for event entry.
.PARAMETER ticketId
    Identifier for a museum ticket (UUID).
#>
function getTicketCode {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/tickets/{ticketId}/qr', Tags = 'Tickets')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Scannable event ticket in image format',
        SchemaRef = 'GetTicketCodeResponse', Inline = $true,
        ContentType = 'image/png')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'TicketId')]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true)]
        [OpenApiPropertyAttribute(Format = 'uuid')]
        [string]$ticketId
    )

    Write-Host "getTicketCode called ticketId='$ticketId'"

    # TODO: return real QR image. For now, placeholder PNG bytes.
    $pngBytes = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)  # PNG header only (not a valid full image).
    Write-KrBinaryResponse -Body $pngBytes -ContentType 'image/png' -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Start-KrServer -Server $srv -CloseLogsOnExit
