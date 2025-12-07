param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI Response Component' -PassThru

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Response Component API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable response components.'

Add-KrOpenApiContact -Email 'support@example.com'
Add-KrOpenApiServer -Url "http://$($IPAddress):$Port" -Description 'Local Server'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(Required = ('statusCode', 'message'))]
class ErrorResponse {
    [OpenApiPropertyAttribute(Description = 'HTTP status code', Example = 400)]
    [int]$statusCode

    [OpenApiPropertyAttribute(Description = 'Error message', Example = 'Invalid request')]
    [string]$message

    [OpenApiPropertyAttribute(Description = 'Error code identifier', Example = 'INVALID_INPUT')]
    [string]$code

    [OpenApiPropertyAttribute(Description = 'Additional error details')]
    [string]$details
}

[OpenApiSchemaComponent(Required = ('id', 'title', 'content'))]
class Article {
    [OpenApiPropertyAttribute(Description = 'Article ID', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Article title', Example = 'Getting Started with OpenAPI')]
    [string]$title

    [OpenApiPropertyAttribute(Description = 'Article content', Example = 'This article covers...')]
    [string]$content

    [OpenApiPropertyAttribute(Description = 'Publication date', Format = 'date', Example = '2025-01-15')]
    [string]$publishedAt

    [OpenApiPropertyAttribute(Description = 'Author name', Example = 'John Doe')]
    [string]$author
}

[OpenApiSchemaComponent(Required = ('id', 'message', 'timestamp'))]
class SuccessResponse {
    [OpenApiPropertyAttribute(Description = 'Operation ID', Format = 'uuid')]
    [string]$id

    [OpenApiPropertyAttribute(Description = 'Success message', Example = 'Resource created successfully')]
    [string]$message

    [OpenApiPropertyAttribute(Description = 'Operation timestamp', Format = 'date-time')]
    [string]$timestamp
}

# =========================================================
#          COMPONENT RESPONSES (Reusable)
# =========================================================

# Success response component
[OpenApiResponseComponent(Description = 'Success response')]
class ResponseSuccess {
    [OpenApiResponse(Description = 'Operation completed successfully', ContentType = 'application/json')]
    [SuccessResponse]$Default
}

# Error response component (400/401/500)
[OpenApiResponseComponent(Description = 'Error response')]
class ResponseError {
    [OpenApiResponse(Description = 'Client error (400, 401, 403)', ContentType = 'application/json')]
    [ErrorResponse]$Default
}

# Article response component
[OpenApiResponseComponent(Description = 'Article response')]
class ResponseArticle {
    [OpenApiResponse(Description = 'Article data', ContentType = 'application/json')]
    [Article]$Default
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# GET article endpoint
<#
.SYNOPSIS
    Get article by ID.
.DESCRIPTION
    Retrieves a single article. Returns Article on success or ErrorResponse on failure.
.PARAMETER articleId
    The article ID to retrieve
#>
function getArticle {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/articles/{articleId}')]
    param([int]$articleId)

    # Validate ID
    if ($articleId -le 0) {
        $error = @{
            statusCode = 400
            message = 'Invalid article ID'
            code = 'INVALID_ID'
            details = 'Article ID must be a positive integer'
        }
        Write-KrJsonResponse $error -StatusCode 400
        return
    }

    # Mock article data
    $article = @{
        id = $articleId
        title = 'Getting Started with OpenAPI'
        content = 'OpenAPI is a specification for building APIs...'
        publishedAt = '2025-01-15'
        author = 'John Doe'
    }

    Write-KrJsonResponse $article -StatusCode 200
}

# POST article endpoint
<#
.SYNOPSIS
    Create a new article.
.DESCRIPTION
    Creates a new article and returns success response or error.
.PARAMETER body
    Article data (title and content required)
#>
function createArticle {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/articles')]
    [OpenApiResponse(StatusCode = '201', Description = 'Article created successfully', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Validation failed')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [PSCustomObject]$body
    )

    # Validate
    if (-not $body.title -or -not $body.content) {
        $error = @{
            statusCode = 400
            message = 'Validation failed'
            code = 'VALIDATION_ERROR'
            details = 'title and content are required'
        }
        Write-KrJsonResponse $error -StatusCode 400
        return
    }

    # Success response
    $success = @{
        id = [System.Guid]::NewGuid().ToString()
        message = 'Article created successfully'
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrJsonResponse $success -StatusCode 201
}

# DELETE article endpoint
<#
.SYNOPSIS
    Delete an article.
.DESCRIPTION
    Deletes an article and returns success or error response.
.PARAMETER articleId
    The article ID to delete
#>
function deleteArticle {
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/articles/{articleId}')]
    [OpenApiResponse(StatusCode = '200', Description = 'Article deleted successfully', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '404', Description = 'Article not found')]
    param([int]$articleId)

    $success = @{
        id = [System.Guid]::NewGuid().ToString()
        message = "Article $articleId deleted successfully"
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrJsonResponse $success -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Start-KrServer -Server $srv -CloseLogsOnExit
