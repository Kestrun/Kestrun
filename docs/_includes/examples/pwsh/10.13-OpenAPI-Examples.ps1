<#
    Sample: OpenAPI Examples (Components + Inline) - Attribute-driven
    Purpose: Demonstrate example components + inline examples referenced by attributes.
    File:    10.13-OpenAPI-Examples.ps1
    Notes:   - Component examples (components/examples) are referenced via *ExampleRef attributes.
             - Inline examples are stored via Add-KrOpenApiInline and referenced via *ExampleRef too,
               but are copied inline (no $ref) when applied.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# =========================================================
#                  HELPERS (DATES)
# =========================================================

<#
.SYNOPSIS
    Converts a DateTime to an ISO 8601 date string (YYYY-MM-DD).
.PARAMETER Date
    The DateTime to convert.
.OUTPUTS
    [string] The ISO 8601 date string.
#>
function Convert-ToIsoDate {
    param([datetime]$Date)
    $Date.ToString('yyyy-MM-dd')
}


<#.SYNOPSIS
    Gets a specific day in ISO date format.
.OUTPUTS
    [datetime] The specified date.
#>
function Get-ADayInIsoDate {
    [DateTime]::ParseExact('20260829', 'yyyyMMdd', [System.Globalization.CultureInfo]::InvariantCulture)
}
<#
.SYNOPSIS
    Gets the next occurrence of the specified day of the week.
.PARAMETER Day
    The day of the week to find the next occurrence for.
.OUTPUTS
    [datetime] The date of the next occurrence of the specified day of the week.
#>
function Get-NextDayOfWeek {
    param([System.DayOfWeek]$Day)
    $now = Get-ADayInIsoDate
    $delta = (([int]$Day - [int]$now.DayOfWeek) + 7) % 7
    if ($delta -eq 0) { $delta = 7 } # force next, not today
    $now.AddDays($delta)
}

# =========================================================
#                  SETUP SERVER + OPENAPI
# =========================================================

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

# =========================================================
#                      SCHEMAS (small)
# =========================================================

[OpenApiSchemaComponent(Description = 'Ticket purchase request.', RequiredProperties = ('ticketType', 'ticketDate', 'email'))]
class BuyTicketRequest {
    [OpenApiProperty(Description = 'Ticket type', Example = 'general')]
    [string]$ticketType

    [OpenApiProperty(Format = 'date', Description = 'Ticket date (YYYY-MM-DD)', Example = '2023-09-07')]
    [string]$ticketDate

    [OpenApiProperty(Format = 'email', Description = 'Purchaser email', Example = 'todd@example.com')]
    [string]$email
}

[OpenApiSchemaComponent(Description = 'Ticket purchase response.', RequiredProperties = ('message', 'ticketId', 'ticketType', 'ticketDate', 'confirmationCode'))]
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

[OpenApiSchemaComponent(Description = 'Museum daily hours', RequiredProperties = ('date', 'timeOpen', 'timeClose'))]
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
    }) -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            scenario = 'buy-ticket'
            payloadKind = 'request'
            preferredContentTypes = @('application/json', 'application/xml', 'application/yaml', 'application/x-www-form-urlencoded')
        }
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsRequestExample'

$buyTicketRequestDataValue = [ordered]@{
    ticketType = 'general'
    ticketDate = '2023-09-07'
    email = 'todd@example.com'
}
New-KrOpenApiExample -Summary 'General entry ticket (dataValue)' `
    -Description 'Same payload as the request example, using the OpenAPI 3.2 dataValue field (3.1 emits x-oai-dataValue).' `
    -DataValue $buyTicketRequestDataValue `
    -SerializedValue '{"ticketType":"general","ticketDate":"2023-09-07","email":"todd@example.com"}' `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            scenario = 'buy-ticket'
            payloadKind = 'request'
            openApiField = 'dataValue'
        }
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsRequestDataValueExample'

New-KrOpenApiExample -Summary 'General entry ticket purchased' -Value ([ordered]@{
        message = 'Museum general entry ticket purchased'
        ticketId = '382c0820-0530-4f4b-99af-13811ad0f17a'
        ticketType = 'general'
        ticketDate = '2023-09-07'
        confirmationCode = 'ticket-general-e5e5c6-dce78'
    }) -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            scenario = 'buy-ticket'
            payloadKind = 'response'
            statusCode = 201
        }
    }) | Add-KrOpenApiComponent -Name 'BuyGeneralTicketsResponseExample'

New-KrOpenApiExample -Summary 'Get hours response' -Value @(
    [ordered]@{ date = '2023-09-11'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-12'; timeOpen = '09:00'; timeClose = '18:00' }
    [ordered]@{ date = '2023-09-13'; timeOpen = '09:00'; timeClose = '18:00' }
) -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            scenario = 'museum-hours'
            payloadKind = 'response'
            statusCode = 200
        }
    }) | Add-KrOpenApiComponent -Name 'GetMuseumHoursResponseExample'

New-KrOpenApiExample -Summary 'Get hours response (external)' `
    -Description 'Demonstrates externalValue (a URL to a literal example payload).' `
    -ExternalValue 'https://example.com/openapi/examples/museum-hours.json' `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            scenario = 'museum-hours'
            payloadKind = 'response'
            openApiField = 'externalValue'
        }
    }) | Add-KrOpenApiComponent -Name 'GetMuseumHoursResponseExternalExample'

# --- Inline examples (stored in Kestrun inline store; copied inline when applied) ---
New-KrOpenApiExample -Summary 'Common today ticket date' -Value (Convert-ToIsoDate (Get-ADayInIsoDate)) |
    Add-KrOpenApiInline -Name 'TodayParameter'

New-KrOpenApiExample -Summary 'Common next Saturday ticket date' -Value (Convert-ToIsoDate (Get-NextDayOfWeek Saturday)) |
    Add-KrOpenApiInline -Name 'NextSaturdayParameter'

New-KrOpenApiExample -Summary 'Common next Sunday ticket date' -Value (Convert-ToIsoDate (Get-NextDayOfWeek Sunday)) |
    Add-KrOpenApiInline -Name 'NextSundayParameter'
# =========================================================
#                 OPENAPI DOC ROUTE / BUILD
# =========================================================
Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Elements -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Rapidoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Scalar -OpenApiEndpoint '/openapi/v3.1/openapi.json'




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

    [OpenApiResponse(StatusCode = '201', Description = 'Ticket purchased',
        ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded', 'application/yaml'),
        Schema = [BuyTicketResponse])]
    [OpenApiResponseExampleRef(StatusCode = '201', Key = 'general_entry', ReferenceId = 'BuyGeneralTicketsResponseExample')]
    param(
        [OpenApiRequestBody(Required = $true,
            ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded', 'application/yaml') )]
        [OpenApiRequestBodyExampleRef(Key = 'general_entry', ReferenceId = 'BuyGeneralTicketsRequestExample' ,
            ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded', 'application/yaml'))]
        [OpenApiRequestBodyExampleRef(Key = 'general_entry_dataValue', ReferenceId = 'BuyGeneralTicketsRequestDataValueExample' ,
            ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded', 'application/yaml'))]
        [BuyTicketRequest]$Body
    )

    $resp = [BuyTicketResponse]::new()
    $resp.message = 'Museum general entry ticket purchased'
    $resp.ticketId = [guid]::NewGuid().ToString()
    $resp.ticketType = $Body.ticketType
    $resp.ticketDate = $Body.ticketDate
    $resp.confirmationCode = 'ticket-general-e5e5c6-dce78'

    Write-KrResponse -InputObject $resp -StatusCode 201
}

function getMuseumHours {
    <#
    .SYNOPSIS
        Get museum hours.
    .DESCRIPTION
        Shows response example via OpenApiResponseExampleRef (components/examples).
    #>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/museum-hours')]
    [OpenApiResponse(StatusCode = '200', Schema = [GetMuseumHoursResponse], Description = 'Success', ContentType = 'application/json')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'default_example', ReferenceId = 'GetMuseumHoursResponseExample')]
    [OpenApiResponseExampleRef(StatusCode = '200', Key = 'external_example', ReferenceId = 'GetMuseumHoursResponseExternalExample')]
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
