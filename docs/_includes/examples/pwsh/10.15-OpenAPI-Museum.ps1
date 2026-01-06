<#
    Sample:  Sample Museum Application with Reusable Request Headers and Multiple Content Types
    Purpose: To demonstrate the use of reusable request headers and handling multiple content types in a Museum application.
    File:    10.15-OpenAPI-Museum.ps1
    Notes:   This script demonstrates how to create a Museum application that handles multiple content types and
             uses reusable request headers. It includes functions for creating, updating, and deleting special events,
             as well as purchasing museum tickets and retrieving ticket QR codes.
#>

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

$srv2 = @(2, 2)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI (3.1.0)
# =========================================================

Add-KrOpenApiInfo -Title 'Redocly Museum API' `
    -Version '1.1.1' `
    -Description 'Imaginary, but delightful Museum API for interacting with museum services and information. Built with love by Redocly.'

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


[OpenApiSchemaComponent(
    Description = 'Request payload for creating new special events at the museum.',
    RequiredProperties = ('name', 'location', 'eventDescription', 'dates', 'price')
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
    RequiredProperties = ('eventId', 'name', 'location', 'eventDescription', 'dates', 'price')
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
    [OpenApiProperty(Description = 'Name of the special event' , Example = 'Fossil lecture')]
    [string]$name

    [OpenApiProperty(Description = 'Location where the special event is held' , Example = 'Lecture theatre')]
    [string]$location

    [OpenApiProperty(Description = 'Description of the special event' ,
        Example = 'Our panel of experts will share their favorite fossils and explain why they are so great.')]
    [string]$eventDescription

    [OpenApiProperty(Description = 'List of planned dates for the special event' , Example = ('2024-03-29'))]
    [datetime[]]$dates

    [OpenApiProperty(Description = 'List of planned dates for the special event', Example = '12.50')]
    [double]$price
}

[OpenApiSchemaComponent(Description = 'A list of upcoming special events.', Array = $true)]
class ListSpecialEventsResponse:SpecialEventResponse {}

[OpenApiSchemaComponent(
    Description = 'Type of ticket being purchased. Use `general` for regular entry and `event` for special events.',
    Enum = ('event', 'general'), Example = 'event')]
class TicketType:OpenApiString {}

[OpenApiSchemaComponent(
    Description = 'Unique identifier for museum ticket. Generated when purchased.',
    Format = 'uuid', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
class TicketId:OpenApiString {}

[OpenApiSchemaComponent(
    Description = 'Confirmation message after a ticket purchase.',
    Example = 'Museum general entry ticket purchased')]
class TicketMessage:OpenApiString {}

[OpenApiSchemaComponent(
    Description = 'Unique confirmation code used to verify ticket purchase.',
    Example = 'ticket-event-a98c8f-7eb12')]
class TicketConfirmation:OpenApiString {}

[OpenApiSchemaComponent(Description = 'Identifier for a special event.',
    Format = 'uuid', Example = '3be6453c-03eb-4357-ae5a-984a0e574a54'
)]
class eventId:OpenApiString {}

[OpenApiSchemaComponent(Description = 'Name of the special event',
    Example = 'Pirate Coding Workshop'
)]
class EventName:OpenApiString {}

[OpenApiSchemaComponent(Description = 'Location where the special event is held.',
    Example = 'Computer Room')]
class EventLocation :OpenApiString {}

[OpenApiSchemaComponent(Description = 'Description of the special event',
    Example = 'Captain Blackbeard shares his love of the C...language. And possibly Arrrrr (R lang).')]
class EventDescription:OpenApiString {}

[OpenApiSchemaComponent(Description = 'Price of a ticket for the special event',
    Format = 'float', Example = 25)]
class EventPrice:OpenApiNumber {}

[OpenApiSchemaComponent(
    Format = 'date', Example = '2023-10-29')]
class Date:OpenApiString {}

[OpenApiSchemaComponent( Description = 'List of planned dates for the special event',
    Array = $true)]
class EventDates:Date {}

[OpenApiSchemaComponent(Description = 'Email address for ticket purchaser.',
    Format = 'email', Example = 'museum-lover@example.com')]
class Email :OpenApiString {}

[OpenApiSchemaComponent(Description = 'Phone number for the ticket purchaser (optional).',
    Example = '+1(234)-567-8910')]
class Phone :OpenApiString {}

[OpenApiSchemaComponent(Description = 'Request payload used for purchasing museum tickets.',
    RequiredProperties = ('ticketType', 'ticketDate', 'email'))]
class BuyMuseumTicketsRequest {
    [TicketType]$ticketType

    [OpenApiProperty(Format = 'uuid', Description = "Unique identifier for a special event. Required if purchasing tickets for the museum's special events.")]
    [EventId]$eventId

    [OpenApiProperty(Description = 'Date that the ticket is valid for.')]
    [Date]$ticketDate

    [OpenApiProperty(Format = 'email')]
    [Email]$email

    [Phone]$phone
}


[OpenApiSchemaComponent(Description = 'Details for a museum ticket after a successful purchase.',
    RequiredProperties = ('message', 'ticketId', 'ticketType', 'ticketDate', 'confirmationCode'))]
class BuyMuseumTicketsResponse {
    [TicketMessage]$message
    [EventName]$eventName

    [TicketId]$ticketId

    [TicketType]$ticketType

    [OpenApiProperty(description = 'Date that the ticket is valid for.')]
    [Date]$ticketDate

    [TicketConfirmation]$confirmationCode
}


[OpenApiSchemaComponent(
    Description = 'An image of a ticket with a QR code used for museum or event entry.',
    Type = 'string', Format = 'binary' )]
class GetTicketCodeResponse {
}

#region Examples
# =========================================================
#                 COMPONENT EXAMPLES
# =========================================================

# --- Ticket purchase examples ---
New-KrOpenApiExample -Summary 'General entry ticket' -Value ([ordered]@{
        ticketType = 'general'
        ticketDate = '2023-09-07'
        email = 'todd@example.com'
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsRequestExample'

# --- Event ticket purchase examples ---
New-KrOpenApiExample -Summary 'Special event ticket' -Value ([ordered]@{
        ticketType = 'general'  # keeping exactly as your YAML even if it looks odd
        eventId = 'dad4bce8-f5cb-4078-a211-995864315e39'
        ticketDate = '2023-09-05'
        email = 'todd@example.com'
    }) | Add-KrOpenApiComponent -Name 'BuyEventTicketsRequestExample'

# --- Ticket purchase response examples ---
New-KrOpenApiExample -Summary 'General entry ticket' -Value ([ordered]@{
        message = 'Museum general entry ticket purchased'
        ticketId = '382c0820-0530-4f4b-99af-13811ad0f17a'
        ticketType = 'general'
        ticketDate = '2023-09-07'
        confirmationCode = 'ticket-general-e5e5c6-dce78'
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsResponseExample'

# --- Ticket purchase response examples ---
New-KrOpenApiExample -Summary 'Special event ticket' -Value ([ordered]@{
        message = 'Museum special event ticket purchased'
        ticketId = 'b811f723-17b2-44f7-8952-24b03e43d8a9'
        eventName = 'Mermaid Treasure Identification and Analysis'
        ticketType = 'event'
        ticketDate = '2023-09-05'
        confirmationCode = 'ticket-event-9c55eg-8v82a'
    }) | Add-KrOpenApiComponent -Name 'BuyEventTicketsResponseExample'

# --- Special events examples ---

New-KrOpenApiExample -Summary 'Create special event' -Value ([ordered]@{
        name = 'Mermaid Treasure Identification and Analysis'
        location = 'Under the seaaa 🦀 🎶 🌊.'
        eventDescription = @'
Join us as we review and classify a rare collection of 20
thingamabobs, gadgets, gizmos, whoosits, and whatsits, kindly donated
by Ariel.
'@.Trim()
        dates = @('2023-09-05', '2023-09-08')
        price = 0
    }) | Add-KrOpenApiComponent -Name 'CreateSpecialEventRequestExample'

New-KrOpenApiExample -Summary 'Special event created' -Value ([ordered]@{
        eventId = 'dad4bce8-f5cb-4078-a211-995864315e39'
        name = 'Mermaid Treasure Identification and Analysis'
        location = 'Under the seaaa 🦀 🎶 🌊.'
        eventDescription = @'
Join us as we review and classify a rare collection of 20
thingamabobs, gadgets, gizmos, whoosits, and whatsits, kindly donated
by Ariel.
'@.Trim()
        dates = @('2023-09-05', '2023-09-08')
        price = 30
    }) | Add-KrOpenApiComponent -Name 'CreateSpecialEventResponseExample'

New-KrOpenApiExample -Summary 'Get special event' -Value ([ordered]@{
        eventId = '6744a0da-4121-49cd-8479-f8cc20526495'
        name = 'Time Traveler Tea Party'
        location = 'Temporal Tearoom'
        eventDescription = 'Sip tea with important historical figures.'
        dates = @('2023-11-18', '2023-11-25', '2023-12-02')
        price = 60
    }) | Add-KrOpenApiComponent -Name 'GetSpecialEventResponseExample'

New-KrOpenApiExample -Summary 'List of special events' -Value @(
    [ordered]@{
        eventId = 'f3e0e76e-e4a8-466e-ab9c-ae36c15b8e97'
        name = 'Sasquatch Ballet'
        location = 'Seattle... probably'
        eventDescription = @'
They're big, they're hairy, but they're also graceful. Come learn
how the biggest feet can have the lightest touch.
'@.Trim()
        dates = @('2023-12-15', '2023-12-22')
        price = 40
    }
    [ordered]@{
        eventId = '2f14374a-9c65-4ee5-94b7-fba66d893483'
        name = 'Solar Telescope Demonstration'
        location = 'Far from the sun.'
        eventDescription = 'Look at the sun without going blind!'
        dates = @('2023-09-07', '2023-09-14')
        price = 50
    }
    [ordered]@{
        eventId = '6aaa61ba-b2aa-4868-b803-603dbbf7bfdb'
        name = 'Cook like a Caveman'
        location = 'Fire Pit on East side'
        eventDescription = 'Learn to cook on an open flame.'
        dates = @('2023-11-10', '2023-11-17', '2023-11-24')
        price = 5
    }
    [ordered]@{
        eventId = '602b75e1-5696-4ab8-8c7a-f9e13580f910'
        name = 'Underwater Basket Weaving'
        location = 'Rec Center Pool next door.'
        eventDescription = 'Learn to weave baskets underwater.'
        dates = @('2023-09-12', '2023-09-15')
        price = 15
    }
    [ordered]@{
        eventId = 'dad4bce8-f5cb-4078-a211-995864315e39'
        name = 'Mermaid Treasure Identification and Analysis'
        location = 'Room Sea-12'
        eventDescription = @'
Join us as we review and classify a rare collection of 20
thingamabobs, gadgets, gizmos, whoosits, and whatsits — kindly
donated by Ariel.
'@.Trim()
        dates = @('2023-09-05', '2023-09-08')
        price = 30
    }
    [ordered]@{
        eventId = '6744a0da-4121-49cd-8479-f8cc20526495'
        name = 'Time Traveler Tea Party'
        location = 'Temporal Tearoom'
        eventDescription = 'Sip tea with important historical figures.'
        dates = @('2023-11-18', '2023-11-25', '2023-12-02')
        price = 60
    }
    [ordered]@{
        eventId = '3be6453c-03eb-4357-ae5a-984a0e574a54'
        name = 'Pirate Coding Workshop'
        location = 'Computer Room'
        eventDescription = @'
Captain Blackbeard shares his love of the C...language. And possibly
Arrrrr (R lang).
'@.Trim()
        dates = @('2023-10-29', '2023-10-30', '2023-10-31')
        price = 45
    }
    [ordered]@{
        eventId = '9d90d29a-2af5-4206-97d9-9ea9ceadcb78'
        name = 'Llama Street Art Through the Ages'
        location = 'Auditorium'
        eventDescription = "Llama street art?! Alpaca my bags -- let's go!"
        dates = @('2023-10-29', '2023-10-30', '2023-10-31')
        price = 45
    }
    [ordered]@{
        eventId = 'a3c7b2c4-b5fb-4ef7-9322-00a919864957'
        name = 'The Great Parrot Debate'
        location = 'Outdoor Amphitheatre'
        eventDescription = 'See leading parrot minds discuss important geopolitical issues.'
        dates = @('2023-11-03', '2023-11-10')
        price = 35
    }
    [ordered]@{
        eventId = 'b92d46b7-4c5d-422b-87a5-287767e26f29'
        name = 'Eat a Bunch of Corn'
        location = 'Cafeteria'
        eventDescription = 'We accidentally bought too much corn. Please come eat it.'
        dates = @('2023-11-10', '2023-11-17', '2023-11-24')
        price = 5
    }
) | Add-KrOpenApiComponent -Name 'ListSpecialEventsResponseExample'

New-KrOpenApiExample -Summary 'Update special event request' -Value ([ordered]@{
        location = 'On the beach.'
        price = 15
    }) | Add-KrOpenApiComponent -Name 'UpdateSpecialEventRequestExample'

New-KrOpenApiExample -Summary 'Update special event' -Value ([ordered]@{
        eventId = 'dad4bce8-f5cb-4078-a211-995864315e39'
        name = 'Mermaid Treasure Identification and Analysis'
        location = 'On the beach.'
        eventDescription = @'
Join us as we review and classify a rare collection of 20
thingamabobs, gadgets, gizmos, whoosits, and whatsits, kindly donated
by Ariel.
'@.Trim()
        dates = @('2023-09-05', '2023-09-08')
        price = 15
    }) | Add-KrOpenApiComponent -Name 'UpdateSpecialEventResponseExample'

# --- Museum hours example ---
$museumHoursValue = @(
    [ordered]@{ date = '2023-09-11'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-12'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-13'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-14'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-15'; timeOpen = '10:00'; timeClose = '16:00' }
    [ordered]@{ date = '2023-09-18'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-19'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-20'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-21'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-22'; timeOpen = '10:00'; timeClose = '16:00' }
)

New-KrOpenApiExample -Summary 'Get hours response' -Value $museumHoursValue |
    Add-KrOpenApiComponent -Name 'GetMuseumHoursResponseExample'
#endregion
#region Parameters
# =========================================================
#                 COMPONENT PARAMETERS
# =========================================================

# These model components.parameters from museum.yml.
# NOTE: we approximate with a class + property decorated as a parameter.
#       The ReferenceId used by OpenApiParameterRefAttribute matches the class name.




New-KrOpenApiExample -Summary 'Pagination limit example' -Value (10) | Add-KrOpenApiInline -Name 'PaginationLimitExample'
New-KrOpenApiExample -Summary 'Pagination limit Extra example' -Value (100) | Add-KrOpenApiComponent -Name 'PaginationLimitExampleExtra'


[OpenApiParameterComponent(In = 'Query',
    Description = 'The number of days per page.')]
[OpenApiParameterExampleRef( Key = 'default_example', ReferenceId = 'PaginationLimitExample')]
[OpenApiParameterExampleRef( Key = 'extra_example', ReferenceId = 'PaginationLimitExampleExtra' , Inline = $true)]
[int]$paginationLimit = 20

[OpenApiParameterComponent(In = 'Query',
    Description = 'The page number to retrieve.' , Minimum = 0, Maximum = 150, Example = 30)]
[int]$paginationPage

[OpenApiParameterComponent(In = 'Query', ContentType = 'application/json',
    Description = "The starting date to retrieve future operating hours from. Defaults to today's date.")]
[Nullable[datetime]]$startDate

[OpenApiParameterComponent(In = 'Path', Required = $true,
    Description = 'An identifier for a special event.', Example = 'dad4bce8-f5cb-4078-a211-995864315e39')]
[Nullable[guid]]$eventId

[OpenApiParameterComponent(In = 'Query',
    Description = 'The end of a date range to retrieve special events for. Defaults to 7 days after startDate.')]
[Nullable[datetime]]$endDate

[OpenApiParameterComponent(In = [OaParameterLocation]::Path, Required = $true,
    Description = 'An identifier for a ticket to a museum event. Used to generate ticket image.')]
[Nullable[guid]]$ticketId

#endregion
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

    [OpenApiResponse(StatusCode = '200', SchemaRef = 'GetMuseumHoursResponse' , Description = 'Success')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'default_example', ReferenceId = 'GetMuseumHoursResponseExample')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]
    # TODO: 400/404 responses are inline in museum.yml; you could introduce response components and use OpenApiResponseRefAttribute.

    param(
        [OpenApiParameterRef(ReferenceId = 'startDate')]
        [datetime]$startDate,

        [OpenApiParameterRef(ReferenceId = 'paginationPage')]
        [int]$paginationPage = 1,

        [OpenApiParameterRef(ReferenceId = 'paginationLimit')]
        [int]$paginationLimit = 10
    )

    Write-Host "getMuseumHours called startDate='$startDate' page='$paginationPage' limit='$paginationLimit'"

    # Dummy payload approximating GetMuseumHoursResponse: a plain array of MuseumDailyHours objects.
    $hours = @(
        [MuseumDailyHours]@{
            date = $(if ($startDate) { $startDate } else { (Get-Date).ToString('yyyy-MM-dd') })
            timeOpen = '09:00'
            timeClose = '18:00'
        }
    )
    $resp = @()
    $resp += $hours
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

    [OpenApiResponse(StatusCode = '201', Description = 'Created.', SchemaRef = 'SpecialEventResponse')]
    [OpenApiResponseExampleRef(StatusCode = '201', Key = 'default_example', ReferenceId = 'CreateSpecialEventResponseExample')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [OpenApiRequestBodyExampleRef( Key = 'default_example', ReferenceId = 'CreateSpecialEventRequestExample')]
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

    [OpenApiResponse(StatusCode = '200', SchemaRef = 'ListSpecialEventsResponse' , Description = 'Success')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'default_example', ReferenceId = 'ListSpecialEventsResponseExample')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameter( In = [OaParameterLocation]::Query, Example = '2023-02-23')]

        [DateTime]$startDate,

        [OpenApiParameter( In = [OaParameterLocation]::Query, Example = '2023-04-18')]
        [DateTime]$endDate,

        [OpenApiParameter(In = [OaParameterLocation]::Query, Example = '2')]
        [int]$page = 1,

        [OpenApiParameter(In = [OaParameterLocation]::Query, Example = '15')]
        # TODO Enable        [OpenApiProperty(Maximum = 30)]
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

    [OpenApiResponse(StatusCode = '200', Description = 'Success', SchemaRef = 'SpecialEventResponse')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'default_example', ReferenceId = 'GetSpecialEventResponseExample')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRef(ReferenceId = 'eventId')]
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

    [OpenApiResponse(StatusCode = '200', Description = 'Success', Schema = [SpecialEventResponse])]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'default_example', ReferenceId = 'UpdateSpecialEventResponseExample')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRef(ReferenceId = 'eventId')]
        [Guid]$eventId,

        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [OpenApiRequestBodyExampleRef( Key = 'default_example', ReferenceId = 'UpdateSpecialEventRequestExample')]
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

    [OpenApiResponse(StatusCode = '204', Description = 'Success - no content')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '401', Description = 'Unauthorized')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]
    # TODO: consider introducing ResponseComponents for 400/401/404 and using OpenApiResponseRefAttribute.

    param(
        [OpenApiParameterRef(ReferenceId = 'eventId')]
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

    [OpenApiResponse(StatusCode = '200', Description = 'Success', SchemaRef = 'BuyMuseumTicketsResponse')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'general_entry', ReferenceId = 'BuyGeneralTicketsResponseExample')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'event_entry', ReferenceId = 'BuyEventTicketsResponseExample')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [OpenApiRequestBodyExampleRef( Key = 'general_entry', ReferenceId = 'BuyGeneralTicketsRequestExample')]
        [OpenApiRequestBodyExampleRef( Key = 'event_entry', ReferenceId = 'BuyEventTicketsRequestExample')]
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

    [OpenApiResponse(StatusCode = '200', Description = 'Scannable event ticket in image format',
        SchemaRef = 'GetTicketCodeResponse',
        ContentType = 'image/png')]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad request')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]

    param(
        [OpenApiParameterRef(ReferenceId = 'ticketId')]
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
Start-KrServer -Server $srv -CloseLogsOnExit
