<#
    Sample: OpenAPI Component Schemas
    Purpose: Demonstrate meaningful schema composition using reusable components.
    File:    10.2-OpenAPI-Component-Schema.ps1
    Notes:
            - Uses OpenApi* scalar wrapper types (see: OpenApiScalars.cs)
      - Demonstrates:
          * reusable primitive schema components (Date, Money, EmployeeId)
          * array wrapper components (Dates, EmployeeList, LineItemList)
          * nested object graphs (PurchaseRequest/PurchaseResponse)
          * inheritance → allOf composition (PersonBase → Person → Employee → EmployeeResponse)
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
    -Description 'Demonstrates meaningful combinations of reusable component schemas.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

# --- Reusable primitive schema components (wrappers over OpenApi* primitives) ---

[OpenApiSchemaComponent(Description = 'A calendar date (YYYY-MM-DD).', Example = '2026-01-13')]
class Date : OpenApiDate {}

[OpenApiSchemaComponent(
    Description = 'A monetary amount. Prefer decimals for currency values.',
    Format = 'decimal',
    Minimum = '0',
    Example = 19.99
)]
class Money : OpenApiNumber {}

[OpenApiSchemaComponent(
    Description = 'Server-generated employee identifier.',
    Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c'
)]
class EmployeeId : OpenApiUuid {}

# Array wrapper for a primitive component: array of Date via items: $ref Date
[OpenApiSchemaComponent(Description = 'List of visit dates.', Array = $true)]
class Dates : Date {}


# --- Object schema components ---

enum TicketType {
    general
    event
}

[OpenApiSchemaComponent(Description = 'Postal address.', RequiredProperties = ('street', 'city', 'postalCode'))]
[OpenApiExtension('x-badges', '[{"name":"Beta","position":"before","color":"purple"},{"name":"PII","position":"after","color":"orange"}]')]
[OpenApiExtension('x-kestrun-demo', '{"owner":"docs","stability":"beta","domain":"profiles","containsPii":true,"notes":["Schema-level vendor extensions are emitted under components.schemas.<Name>","This schema also uses a regex ValidatePattern for postalCode"]}')]
class Address {
    [OpenApiProperty(Description = 'Street line.', Example = '1 Museum Way')]
    [string]$street

    [OpenApiProperty(Description = 'City.', Example = 'Seattle')]
    [string]$city

    [OpenApiProperty(Description = 'Postal code.', Example = '98101')]
    [ValidatePattern('^[0-9A-Za-z\- ]{3,12}$')]
    [string]$postalCode
}

[OpenApiSchemaComponent(Description = 'Base person identity.', RequiredProperties = ('firstName', 'lastName'))]
class PersonBase {
    [OpenApiProperty(Description = 'First name.', Example = 'Avery')]
    [string]$firstName

    [OpenApiProperty(Description = 'Last name.', Example = 'Rivera')]
    [string]$lastName
}

# Inheritance produces allOf(PersonBase, Person)
[OpenApiSchemaComponent(Description = 'Person contact information.', RequiredProperties = ('email'))]
class Person : PersonBase {
    [OpenApiProperty(Description = 'Primary email address.', Example = 'avery.rivera@example.com')]
    [OpenApiEmail]$email

    [OpenApiProperty(Description = 'Optional phone number.', Example = '+1-206-555-0123')]
    [ValidatePattern('^[+0-9() \-]{7,25}$')]
    [string]$phone
}

# Inheritance produces allOf(Person, Employee)
[OpenApiSchemaComponent(Description = 'Employee record.', RequiredProperties = ('hireDate'))]
class Employee : Person {
    [OpenApiProperty(Description = 'Hire date (YYYY-MM-DD).', Example = '2024-08-01')]
    [Date]$hireDate

    [OpenApiProperty(Description = 'Employee roles.', Example = ('guide', 'cashier'))]
    [string[]]$roles

    [OpenApiProperty(Description = 'Mailing address.')]
    [Address]$address
}

# Response schema: server adds employeeId + createdAt (allOf(Employee, EmployeeResponse))
[OpenApiSchemaComponent(Description = 'Employee response with server-generated fields.', RequiredProperties = ('employeeId', 'createdAt'))]
class EmployeeResponse : Employee {
    [OpenApiProperty(Description = 'Employee identifier.', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
    [EmployeeId]$employeeId

    [OpenApiProperty(Description = 'Creation timestamp (RFC 3339).', Example = '2026-01-13T20:15:30.123Z')]
    [OpenApiDateTime]$createdAt
}

[OpenApiSchemaComponent(Description = 'List of employees.', Array = $true)]
class EmployeeList : EmployeeResponse {}


[OpenApiSchemaComponent(Description = 'A purchasable ticket line item.', RequiredProperties = ('ticketType', 'quantity', 'unitPrice'))]
class LineItem {
    [OpenApiProperty(Description = 'Ticket category.', Example = 'general')]
    [TicketType]$ticketType

    [OpenApiProperty(Description = 'Quantity of tickets.', Minimum = 1, Maximum = 20, Example = 2)]
    [ValidateRange(1, 20)]
    [int]$quantity

    [OpenApiProperty(Description = 'Unit price for the ticket.', Example = 25.00)]
    [Money]$unitPrice
}

[OpenApiSchemaComponent(Description = 'List of ticket line items.', Array = $true)]
class LineItemList : LineItem {}

[OpenApiSchemaComponent(Description = 'Ticket purchase request.', RequiredProperties = ('customer', 'items', 'visitDates'))]
class PurchaseRequest {
    [OpenApiProperty(Description = 'Customer details.')]
    [Person]$customer

    [OpenApiProperty(Description = 'Tickets being purchased.')]
    [LineItem[]]$items

    [OpenApiProperty(Description = 'Dates the tickets are valid for.')]
    [Dates]$visitDates

    [OpenApiProperty(Description = 'Optional note attached to the purchase.', Example = 'Please email the receipt.')]
    [string]$note
}

[OpenApiSchemaComponent(Description = 'Ticket purchase response.', RequiredProperties = ('ticketId', 'total', 'createdAt'))]
class PurchaseResponse {
    [OpenApiProperty(Description = 'Server-generated ticket id.', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
    [OpenApiUuid]$ticketId

    [OpenApiProperty(Description = 'Total amount charged.', Example = 50.00)]
    [Money]$total

    [OpenApiProperty(Description = 'Echo of purchased items.')]
    [LineItemList]$items

    [OpenApiProperty(Description = 'Purchase timestamp (RFC 3339).', Example = '2026-01-13T20:15:30.123Z')]
    [OpenApiDateTime]$createdAt
}

[OpenApiSchemaComponent(Description = 'Standard error response.', RequiredProperties = ('code', 'message'))]
class ErrorResponse {
    [OpenApiProperty(Description = 'HTTP-like error code.', Example = 400)]
    [int]$code

    [OpenApiProperty(Description = 'Human-readable error message.', Example = 'Invalid input')]
    [string]$message
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc


<#
.SYNOPSIS
    List employees.
.DESCRIPTION
    Returns an array of employee records.
.NOTES
    Demonstrates reusable component schemas and array wrappers.
    GET endpoint: Return a list of employees (array component)
#>
function listEmployees {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/employees')]
    [OpenApiResponse(StatusCode = '200', Description = 'OK', Schema = [EmployeeList], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    param()

    $employees = @(
        @{
            employeeId = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c'
            createdAt = (Get-Date).ToUniversalTime().ToString('o')
            firstName = 'Avery'
            lastName = 'Rivera'
            email = 'avery.rivera@example.com'
            phone = '+1-206-555-0123'
            hireDate = '2024-08-01'
            roles = @('guide')
            address = @{ street = '1 Museum Way'; city = 'Seattle'; postalCode = '98101' }
        },
        @{
            employeeId = '3d8f5c2c-6e3c-4a7a-8f79-1f2a4b1c9a10'
            createdAt = (Get-Date).AddDays(-7).ToUniversalTime().ToString('o')
            firstName = 'Jordan'
            lastName = 'Chen'
            email = 'jordan.chen@example.com'
            hireDate = '2023-02-15'
            roles = @('cashier', 'security')
            address = @{ street = '99 Gallery Ave'; city = 'Seattle'; postalCode = '98104' }
        }
    )

    Write-KrResponse $employees -StatusCode 200
}

<#
.SYNOPSIS
    Purchase tickets.
.DESCRIPTION
    Accepts a purchase request and returns a purchase confirmation.
.PARAMETER body
    Ticket purchase request payload.
.NOTES
    Demonstrates nested object graphs and array wrappers.
    POST endpoint: Accept a purchase request and return a purchase response
#>
function purchaseTickets {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/tickets/purchase')]
    [OpenApiResponse(StatusCode = '201', Description = 'Created', Schema = [PurchaseResponse], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input', Schema = [ErrorResponse], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    param(
        [OpenApiRequestBody(
            Description = 'Ticket purchase request payload.',
            Required = $true,
            ContentType = ('application/json', 'application/xml', 'application/yaml', 'application/x-www-form-urlencoded')
        )]
        [PurchaseRequest]$body
    )

    if ($null -eq $body -or $null -eq $body.customer -or -not $body.customer.email) {
        Write-KrResponse @{ code = 400; message = 'customer.email is required' } -StatusCode 400
        return
    }

    if ($null -eq $body.items -or $body.items.Count -lt 1) {
        Write-KrResponse @{ code = 400; message = 'At least one line item is required' } -StatusCode 400
        return
    }

    $total = 0.0
    foreach ($item in $body.items) {
        $qty = [int]$item.quantity
        $price = [double]$item.unitPrice
        $total += ($qty * $price)
    }

    $response = @{
        ticketId = [guid]::NewGuid().ToString()
        total = $total
        items = $body.items
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $response -StatusCode 201
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
# Test and log OpenAPI document validation result
if (Test-KrOpenApiDocument) {
    Write-KrLog -Level Information -Message 'OpenAPI document built and validated successfully.'
} else {
    Write-KrLog -Level Error -Message 'OpenAPI document validation failed.'
}

# =========================================================
#                      RUN SERVER
# =========================================================


Start-KrServer -CloseLogsOnExit
