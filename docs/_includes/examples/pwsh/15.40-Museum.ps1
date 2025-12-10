param(
    [int]$Port = 5000,
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


[OpenApiSchemaComponent( Description = 'Daily operating hours for the museum.',
    Required = ('date', 'timeOpen', 'timeClose'))]
class MuseumDailyHours {
    [OpenApiPropertyAttribute(Description = 'Date the operating hours apply to.', Example = '2024-12-31')]
    [Date]$date

    [OpenApiPropertyAttribute(Description = 'Time the museum opens on a specific date. Uses 24 hour time format (`HH:mm`).', Example = '09:00')]
    [ValidatePattern('^([01]\d|2[0-3]):([0-5]\d)$')]
    [string]$timeOpen

    [OpenApiPropertyAttribute(Description = 'Time the museum closes on a specific date. Uses 24 hour time format (`HH:mm`).', Example = '18:00')]
    [ValidatePattern('^([01]\d|2[0-3]):([0-5]\d)$')]
    [string]$timeClose
}

[OpenApiSchemaComponent(
    Description = 'List of museum operating hours for consecutive days.',
    Array = $true
)]
class GetMuseumHoursResponse:MuseumDailyHours {}


[OpenApiSchemaComponent(
    Description = 'Request payload for creating new special events at the museum.',
    Required = ('name', 'location', 'eventDescription', 'dates', 'price')
)]
class CreateSpecialEventRequest {
    [EventName]$name
    [EventLocation]$location
    [EventDescription]$eventDescription
    [EventDates]$dates
    [EventPrice]$price  # format: float
}


[OpenApiSchemaComponent(
    Description = 'Request payload for updating an existing special event. Only included fields are updated in the event.'
)]
class UpdateSpecialEventRequest {
    [EventName]$name
    [EventLocation]$location
    [EventDescription]$eventDescription
    [EventDates]$dates
    [EventPrice]$price
}


[OpenApiSchemaComponent(  Description = 'Information about a special event.',
    Required = ('eventId', 'name', 'location', 'eventDescription', 'dates', 'price')
)]
class SpecialEventResponse {
    [EventId]$eventId

    [EventName]$name
    [EventLocation]$location
    [EventDescription]$eventDescription
    [EventDates]$dates
    [EventPrice]$price
}

[OpenApiSchemaComponent( Description = 'Request payload for creating new special events at the museum.')]
class SpecialEvent {
    [OpenApiPropertyAttribute(Description = 'Name of the special event' , Example = 'Fossil lecture')]
    [string]$name

    [OpenApiPropertyAttribute(Description = 'Location where the special event is held' , Example = 'Lecture theatre')]
    [string]$location

    [OpenApiPropertyAttribute(Description = 'Description of the special event' ,
        Example = 'Our panel of experts will share their favorite fossils and explain why they are so great.')]
    [string]$eventDescription

    [OpenApiPropertyAttribute(Description = 'List of planned dates for the special event' , Example = ('2024-03-29'))]
    [datetime[]]$dates

    [OpenApiPropertyAttribute(Description = 'List of planned dates for the special event', Example = '12.50')]
    [double]$price
}

[OpenApiSchemaComponent(Description = 'A list of upcoming special events.', Array = $true)]
class ListSpecialEventsResponse:SpecialEventResponse {}

[OpenApiSchemaComponent(
    Description = 'Type of ticket being purchased. Use `general` for regular entry and `event` for special events.',
    Enum = ('event', 'general'), Example = 'event')]
class TicketType:OaString {}

[OpenApiSchemaComponent(
    Description = 'Unique identifier for museum ticket. Generated when purchased.',
    Format = 'uuid', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
class TicketId:OaString {}

[OpenApiSchemaComponent(
    Description = 'Confirmation message after a ticket purchase.',
    Example = 'Museum general entry ticket purchased')]
class TicketMessage:OaString {}

[OpenApiSchemaComponent(
    Description = 'Unique confirmation code used to verify ticket purchase.',
    Example = 'ticket-event-a98c8f-7eb12')]
class TicketConfirmation:OaString {}

[OpenApiSchemaComponent(Description = 'Identifier for a special event.',
    Format = 'uuid', Example = '3be6453c-03eb-4357-ae5a-984a0e574a54'
)]
class eventId:OaString {}

[OpenApiSchemaComponent(Description = 'Name of the special event',
    Example = 'Pirate Coding Workshop'
)]
class EventName:OaString {}

[OpenApiSchemaComponent(Description = 'Location where the special event is held.',
    Example = 'Computer Room')]
class EventLocation :OaString {}

[OpenApiSchemaComponent(Description = 'Description of the special event',
    Example = 'Captain Blackbeard shares his love of the C...language. And possibly Arrrrr (R lang).')]
class EventDescription:OaString {}

[OpenApiSchemaComponent(Description = 'Price of a ticket for the special eventt',
    format = 'float', Example = 25)]
class EventPrice:OaNumber {}

[OpenApiSchemaComponent(
    Format = 'date', Example = '2023-10-29')]
class Date:OaString {}

[OpenApiSchemaComponent( Description = 'List of planned dates for the special event',
    Array = $true)]
class EventDates:Date {}

[OpenApiSchemaComponent(Description = 'Email address for ticket purchaser.',
    Format = 'email', Example = 'museum-lover@example.com')]
class Email :OaString {}

[OpenApiSchemaComponent(Description = 'Phone number for the ticket purchaser (optional).',
    Example = '+1(234)-567-8910')]
class Phone :OaString {}

[OpenApiSchemaComponent(Description = 'Request payload used for purchasing museum tickets.',
    Required = ('ticketType', 'ticketDate', 'email'))]
class BuyMuseumTicketsRequest {
    [TicketType]$ticketType

    [OpenApiPropertyAttribute(Format = 'uuid', Description = "Unique identifier for a special event. Required if purchasing tickets for the museum's special events.")]
    [EventId]$eventId

    [OpenApiPropertyAttribute(Format = 'date', Description = 'Date that the ticket is valid for.')]
    [Date]$ticketDate

    [OpenApiPropertyAttribute(Format = 'email')]
    [Email]$email

    [Phone]$phone
}


[OpenApiSchemaComponent(Description = 'Details for a museum ticket after a successful purchase.',
    Required = ('message', 'ticketId', 'ticketType', 'ticketDate', 'confirmationCode'))]
class BuyMuseumTicketsResponse {
    [TicketMessage]$message
    [EventName]$eventName

    [TicketId]$ticketId

    [TicketType]$ticketType

    [OpenApiPropertyAttribute(description = 'Date that the ticket is valid for.')]
    [Date]$ticketDate

    [TicketConfirmation]$confirmationCode
}


[OpenApiSchemaComponent(
    Description = 'An image of a ticket with a QR code used for museum or event entry.',
    Type = 'string', Format = 'binary' )]
class GetTicketCodeResponse {
}


# =========================================================
#                 COMPONENT PARAMETERS
# =========================================================

# These model components.parameters from museum.yml.
# NOTE: we approximate with a class + property decorated as a parameter.
#       The ReferenceId used by OpenApiParameterRefAttribute matches the class name.



[OpenApiParameterComponent()]
class MuseumParameters {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = 'The number of days per page.')]
    [int]$paginationLimit

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = 'The page number to retrieve.')]
    [int]$paginationPage

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = "The starting date to retrieve future operating hours from. Defaults to today's date.")]
    [datetime]$startDate

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true,
        Description = 'An identifier for a special event.', Example = 'dad4bce8-f5cb-4078-a211-995864315e39')]
    [guid]$eventId

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,
        Description = 'The end of a date range to retrieve special events for. Defaults to 7 days after startDate.')]
    [OpenApiPropertyAttribute(Format = 'date')]
    [string]$endDate

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true,
        Description = 'An identifier for a ticket to a museum event. Used to generate ticket image.')]
    [Guid]$ticketId
}
<#
[OpenApiExampleComponent()]
class listSpecialEventsExample {
    $summary = 'Get special event'
    $value = @{
        eventId = 6744a0da-4121-49cd-8479-f8cc20526495
        name = Time Traveler Tea Party
        location = Temporal Tearoom
        eventDescription = Sip tea with important historical figures.
        dates = @(
            '2023 - 11 - 18'
            '2023 - 11 - 25'
            '2023 - 12 - 02'
        )
        price = 60
    }
}#>
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


function getMuseumHours {
    <#
.SYNOPSIS
    Get museum hours.
.DESCRIPTION
    Get upcoming museum operating hours.
#>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/museum-hours', Tags = 'Operations')]

    [OpenApiResponseAttribute(StatusCode = '200', SchemaRef = 'GetMuseumHoursResponse' , Description = 'Success')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]
    # TODO: 400/404 responses are inline in museum.yml; you could introduce response components and use OpenApiResponseRefAttribute.

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'startDate')]
        [datetime]$startDate,

        [OpenApiParameterRefAttribute(ReferenceId = 'paginationPage')]
        [int]$paginationPage = 1,

        [OpenApiParameterRefAttribute(ReferenceId = 'paginationLimit')]
        [int]$paginationLimit = 10
    )

    Write-Host "getMuseumHours called startDate='$startDate' page='$paginationPage' limit='$paginationLimit'"

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


function createSpecialEvent {
    <#
.SYNOPSIS
    Create special event.
#>
    [OpenApiPath(HttpVerb = 'post', Pattern = '/special-events', Tags = 'Events')]
    # TODO: museum.yml sets security: [] here (no auth); add per-operation override when supported.

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Created.',
        SchemaRef = 'SpecialEventResponse', ContentType = 'application/json')]
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


function listSpecialEvents {
    <#
.SYNOPSIS
    List special events.
.DESCRIPTION
    Return a list of upcoming special events at the museum.
.PARAMETER startDate
    The starting date to retrieve future special events from. Defaults to today's date.
.PARAMETER endDate
    The ending date to retrieve future special events up to. Defaults to no end date.
.PARAMETER page
    The page number to retrieve. Default is 1.
.PARAMETER limit
    The number of special events per page. Default is 10, maximum is 30.
#>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/special-events', Tags = 'Events')]
    # TODO: museum.yml sets security: [] here (no auth); add per-operation override when supported.

    [OpenApiResponseAttribute(StatusCode = '200', SchemaRef = 'ListSpecialEventsResponse' , Description = 'Success' , #examples = 'listSpecialEventsExample' ,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Query, Example = '2023-02-23')]
        [DateTime]$startDate,

        [OpenApiParameterAttribute( In = [OaParameterLocation]::Query, Example = '2023-04-18')]
        [DateTime]$endDate,

        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Example = '2')]
        [int]$page = 1,

        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Example = '15')]
        # TODO Enable        [OpenApiPropertyAttribute(Maximum = 30)]
        [int]$limit = 10
    )

    Write-Host "listSpecialEvents called startDate='$startDate' endDate='$endDate' page='$page' limit='$limit'"
    $specialEvent = [SpecialEventResponse]::new()
    $specialEvent.eventId = [Guid]::NewGuid().ToString()
    $specialEvent.name = 'Sample Event'
    $specialEvent.location = 'Main Hall'
    $specialEvent.eventDescription = 'Sample special event description.'
    $specialEvent.dates = @((Get-Date).ToString('yyyy-MM-dd'))
    $specialEvent.price = 25

    $resp = [ListSpecialEventsResponse]::new()
    $resp.items = @($specialEvent)
    Write-KrJsonResponse $resp -StatusCode 200
}

# --------------------------------------
# /special-events/{eventId}
#   GET getSpecialEvent
#   PATCH updateSpecialEvent
#   DELETE deleteSpecialEvent
# --------------------------------------


function getSpecialEvent {
    <#
.SYNOPSIS
    Get special event.
.DESCRIPTION
    Get details about a special event.
#>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/special-events/{eventId}', Tags = 'Events')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        SchemaRef = 'SpecialEventResponse' ,
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'eventId')]
        [Guid]$eventId
    )

    Write-Host "getSpecialEvent called eventId='$eventId'"

    $specialEvent = [SpecialEventResponse]::new()
    $specialEvent.eventId = $eventId
    $specialEvent.name = 'Time Traveler Tea Party'
    $specialEvent.location = 'Temporal Tearoom'
    $specialEvent.eventDescription = 'Sip tea with important historical figures.'
    $specialEvent.dates = @('2023-11-18', '2023-11-25', '2023-12-02')
    $specialEvent.price = 60

    Write-KrJsonResponse $specialEvent -StatusCode 200
}


function updateSpecialEvent {
    <#
.SYNOPSIS
    Update special event.
.DESCRIPTION
    Update the details of a special event.
#>
    [OpenApiPath(HttpVerb = 'patch', Pattern = '/special-events/{eventId}', Tags = 'Events')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        Schema = [SpecialEventResponse],
        ContentType = 'application/json')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'eventId')]
        [Guid]$eventId,

        [OpenApiRequestBodyAttribute(Required = $true,
            ContentType = 'application/json')]
        [UpdateSpecialEventRequest]$Body
    )

    Write-Host "updateSpecialEvent called eventId='$eventId' body:"
    $Body | ConvertTo-Json -Depth 5 | Write-Host

    $specialEvent = [SpecialEventResponse]::new()
    $specialEvent.eventId = $eventId
    $specialEvent.name = $Body.name
    $specialEvent.location = $Body.location
    $specialEvent.eventDescription = $Body.eventDescription
    $specialEvent.dates = $Body.dates
    $specialEvent.price = $Body.price

    Write-KrJsonResponse $specialEvent -StatusCode 200
}


function deleteSpecialEvent {
    <#
.SYNOPSIS
    Delete special event.
.DESCRIPTION
    Delete a special event from the collection. Allows museum to cancel planned events.
#>
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/special-events/{eventId}', Tags = 'Events')]

    [OpenApiResponseAttribute(StatusCode = '204', Description = 'Success - no content')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '401', Description = 'Unauthorized')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]
    # TODO: consider introducing ResponseComponents for 400/401/404 and using OpenApiResponseRefAttribute.

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'eventId')]
        [Guid]$eventId
    )

    Write-Host "deleteSpecialEvent called eventId='$eventId'"
    Write-KrStatusResponse -StatusCode 204
}

# --------------------------------------
# POST /tickets
# --------------------------------------

function buyMuseumTickets {
    <#
.SYNOPSIS
    Buy museum tickets.
.DESCRIPTION
    Purchase museum tickets for general entry or special events.
.PARAMETER Body
    Request payload describing the ticket purchase.
#>
    [OpenApiPath(HttpVerb = 'post', Pattern = '/tickets', Tags = 'Tickets')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Success',
        SchemaRef = 'BuyMuseumTicketsResponse',
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

function getTicketCode {
    <#
.SYNOPSIS
    Get ticket QR code.
.DESCRIPTION
    Return an image of your ticket with scannable QR code. Used for event entry.
.PARAMETER ticketId
    Identifier for a museum ticket (UUID).
#>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/tickets/{ticketId}/qr', Tags = 'Tickets')]

    [OpenApiResponseAttribute(StatusCode = '200', Description = 'Scannable event ticket in image format',
        SchemaRef = 'GetTicketCodeResponse',
        ContentType = 'image/png')]
    [OpenApiResponseAttribute(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponseAttribute(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'ticketId')]
        [Guid]$ticketId
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
