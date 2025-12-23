<#
    Sample: OpenAPI Examples (Components + Inline) - Attribute-driven
    Purpose: Demonstrate example components + inline examples referenced by attributes.
    File:    10.3-OpenAPI-Examples-Component-Inline.ps1
    Notes:   - Component examples (components/examples) are referenced via *ExampleRef attributes.
             - Inline examples are stored via Add-KrOpenApiInline and referenced via *ExampleRef too,
               but are copied inline (no $ref) when applied.
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

New-KrServer -Name 'OpenAPI Examples (Components + Inline)'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Examples API' `
    -Version '1.0.0' `
    -Description 'Demonstrates OpenAPI example components and Kestrun inline examples applied via attributes.'

Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# =========================================================
#                  HELPERS (DATES)
# =========================================================

function Convert-ToIsoDate {
    param([datetime]$Date)
    $Date.ToString('yyyy-MM-dd')
}

function Get-NextDayOfWeek {
    param([System.DayOfWeek]$Day)
    $now = Get-Date
    $delta = (([int]$Day - [int]$now.DayOfWeek) + 7) % 7
    if ($delta -eq 0) { $delta = 7 } # force next, not today
    $now.AddDays($delta)
}

# =========================================================
#                      SCHEMAS (small)
# =========================================================

[OpenApiSchemaComponent(Description = 'Ticket purchase request.', Required = ('ticketType', 'ticketDate', 'email'))]
class BuyTicketRequest {
    [OpenApiProperty(Description = 'Ticket type', Example = 'general')]
    [string]$ticketType

    [OpenApiProperty(Format = 'date', Description = 'Ticket date (YYYY-MM-DD)', Example = '2023-09-07')]
    [string]$ticketDate

    [OpenApiProperty(Format = 'email', Description = 'Purchaser email', Example = 'todd@example.com')]
    [string]$email
}

[OpenApiSchemaComponent(Description = 'Ticket purchase response.', Required = ('message', 'ticketId', 'ticketType', 'ticketDate', 'confirmationCode'))]
class BuyTicketResponse {
    [OpenApiProperty(Example = 'Museum general entry ticket purchased')]
    [string]$message

    [OpenApiProperty(Format = 'uuid')]
    [string]$ticketId

    [OpenApiProperty(Example = 'general')]
    [string]$ticketType

    [OpenApiProperty(Format = 'date')]
    [string]$ticketDate

    [OpenApiProperty(Example = 'ticket-general-e5e5c6-dce78')]
    [string]$confirmationCode
}

[OpenApiSchemaComponent(Description = 'Museum daily hours', Required = ('date', 'timeOpen', 'timeClose'))]
class MuseumDailyHours {
    [OpenApiProperty(Format = 'date', Example = '2023-09-11')]
    [string]$date

    [OpenApiProperty(Example = '09:00')]
    [string]$timeOpen

    [OpenApiProperty(Example = '18:00')]
    [string]$timeClose
}

[OpenApiSchemaComponent(Description = 'List of museum hours', Array = $true)]
class GetMuseumHoursResponse : MuseumDailyHours {}

# =========================================================
#                 EXAMPLES (components vs inline)
# =========================================================

# --- Component examples (stored under components/examples) ---
New-KrOpenApiExample -Summary 'General entry ticket' -Value ([ordered]@{
        ticketType = 'general'
        ticketDate = '2023-09-07'
        email = 'todd@example.com'
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsRequestExample'

New-KrOpenApiExample -Summary 'General entry ticket purchased' -Value ([ordered]@{
        message = 'Museum general entry ticket purchased'
        ticketId = '382c0820-0530-4f4b-99af-13811ad0f17a'
        ticketType = 'general'
        ticketDate = '2023-09-07'
        confirmationCode = 'ticket-general-e5e5c6-dce78'
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsResponseExample'

New-KrOpenApiExample -Summary 'Get hours response' -Value @(
    [ordered]@{ date = '2023-09-11'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-12'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-13'; timeOpen = '09:00'; timeClose = '18:00' }
) | Add-KrOpenApiComponent -Name 'GetMuseumHoursResponseExample'

# --- Inline examples (stored in Kestrun inline store; copied inline when applied) ---
New-KrOpenApiExample -Summary 'Common today ticket date' -Value (Convert-ToIsoDate (Get-Date)) |
    Add-KrOpenApiInline -Name 'TodayParameter'

New-KrOpenApiExample -Summary 'Common next Saturday ticket date' -Value (Convert-ToIsoDate (Get-NextDayOfWeek Saturday)) |
    Add-KrOpenApiInline -Name 'NextSaturdayParameter'

New-KrOpenApiExample -Summary 'Common next Sunday ticket date' -Value (Convert-ToIsoDate (Get-NextDayOfWeek Sunday)) |
    Add-KrOpenApiInline -Name 'NextSundayParameter'

# =========================================================
#                 ROUTES / OPERATIONS (attribute wiring)
# =========================================================

function buyTicket {
    <#
    .SYNOPSIS
        Buy a ticket.
    .DESCRIPTION
        Shows request+response examples via ExampleRef attributes (components/examples).
    #>
    [OpenApiPath(HttpVerb = 'post', Pattern = '/tickets')]

    [OpenApiResponse(StatusCode = '201', Description = 'Ticket purchased', ContentType = 'application/json', Schema = [BuyTicketResponse])]
    [OpenApiResponseExampleRef(StatusCode = '201', Key = 'general_entry', ReferenceId = 'BuyGeneralTicketsResponseExample')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json' )]
        [OpenApiRequestBodyExampleRef(Key = 'general_entry', ReferenceId = 'BuyGeneralTicketsRequestExample')]
        [BuyTicketRequest]$Body
    )

    $resp = [BuyTicketResponse]::new()
    $resp.message = 'Museum general entry ticket purchased'
    $resp.ticketId = [guid]::NewGuid().ToString()
    $resp.ticketType = $Body.ticketType
    $resp.ticketDate = $Body.ticketDate
    $resp.confirmationCode = 'ticket-general-e5e5c6-dce78'

    Write-KrJsonResponse $resp -StatusCode 201
}

function getMuseumHours {
    <#
    .SYNOPSIS
        Get museum hours.
    .DESCRIPTION
        Shows response example via OpenApiResponseExampleRef (components/examples).
    #>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/museum-hours')]
    [OpenApiResponse(StatusCode = '200', SchemaRef = 'GetMuseumHoursResponse', Description = 'Success', ContentType = 'application/json')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'default_example', ReferenceId = 'GetMuseumHoursResponseExample')]
    param()

    $resp = @(
        @{ date = '2023-09-11'; timeOpen = '09:00'; timeClose = '18:00' }
        @{ date = '2023-09-12'; timeOpen = '09:00'; timeClose = '18:00' }
    )

    Write-KrJsonResponse $resp -StatusCode 200
}

function searchTickets {
    <#
    .SYNOPSIS
        Search tickets by date.
    .DESCRIPTION
        Shows scalar query parameter examples using inline example store (copied inline).
    .PARAMETER ticketDate
        Ticket date (YYYY-MM-DD).
    #>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/tickets/search')]
    [OpenApiResponse(StatusCode = '200', Description = 'OK', ContentType = 'application/json')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Query )]
        [OpenApiParameterExampleRef(Key = 'today', ReferenceId = 'TodayParameter')]
        [OpenApiParameterExampleRef(Key = 'nextSaturday', ReferenceId = 'NextSaturdayParameter')]
        [OpenApiParameterExampleRef(Key = 'nextSunday', ReferenceId = 'NextSundayParameter')]
        [datetime]$ticketDate
    )

    Write-KrJsonResponse @{ ticketDate = $ticketDate; ok = $true } -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute
Build-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -CloseLogsOnExit
