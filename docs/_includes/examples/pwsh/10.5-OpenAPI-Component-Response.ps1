<#
    Sample: OpenAPI Response Components
    Purpose: Demonstrate reusable response components with multiple response types per component.
    File:    10.5-OpenAPI-Component-Response.ps1
    Notes:   Uses the variable-based response component pattern.
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

New-KrServer -Name 'OpenAPI Response Component'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Response Component API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable response components.'

Add-KrOpenApiTag -Name 'Articles' -Description 'Article CRUD operations.'


# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(RequiredProperties = ('statusCode', 'message'))]
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

[OpenApiSchemaComponent(RequiredProperties = ('id', 'title', 'content'))]
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

[OpenApiSchemaComponent(RequiredProperties = ('id', 'message', 'timestamp'))]
class SuccessResponse {
    [OpenApiPropertyAttribute(Description = 'Operation ID', Format = 'int64')]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Success message', Example = 'Resource created successfully')]
    [string]$message

    [OpenApiPropertyAttribute(Description = 'Operation timestamp', Format = 'date-time')]
    [string]$timestamp
}

# =========================================================
#                COMPONENT EXAMPLES / HEADERS / LINKS
# =========================================================

$correlationExamples = @{
    'uuid' = New-KrOpenApiExample -Summary 'Correlation id' -Value '7b2a8e5d-0d7c-4f0a-9b3c-3f9d0b8ad7b1'
}
New-KrOpenApiHeader `
    -Description 'Correlation id for tracing the request across services.' `
    -Schema ([string]) `
    -Required `
    -Examples $correlationExamples |
    Add-KrOpenApiComponent -Name 'X-Correlation-Id'

New-KrOpenApiExample -Summary 'Article created response' -Value ([ordered]@{
        id = 1
        message = 'Article created successfully'
        timestamp = '2026-01-08T00:00:00Z'
    }) | Add-KrOpenApiComponent -Name 'ArticleCreatedResponseExample'

New-KrOpenApiExample -Summary 'Bad request error' -Value ([ordered]@{
        statusCode = 400
        message = 'Validation failed'
        code = 'VALIDATION_ERROR'
        details = 'title and content are required'
    }) | Add-KrOpenApiComponent -Name 'BadRequestErrorExample'

New-KrOpenApiExample -Summary 'Not found error' -Value ([ordered]@{
        statusCode = 404
        message = 'Article not found'
        code = 'NOT_FOUND'
        details = 'No article exists with id 404'
    }) | Add-KrOpenApiComponent -Name 'NotFoundErrorExample'

New-KrOpenApiLink -OperationId 'getArticle' -Description 'Get the returned/created article.' `
    -Parameters @{ articleId = '$response.body#/id' } |
    Add-KrOpenApiComponent -Name 'GetArticleLink'

New-KrOpenApiLink -OperationId 'deleteArticle' -Description 'Delete the returned/created article.' `
    -Parameters @{ articleId = '$response.body#/id' } |
    Add-KrOpenApiComponent -Name 'DeleteArticleLink'

# =========================================================
#          COMPONENT RESPONSES (Reusable)
# =========================================================

[OpenApiResponseComponent(Description = 'Operation completed successfully', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseLinkRef(Key = 'get', ReferenceId = 'GetArticleLink')]
[OpenApiResponseLinkRef(Key = 'delete', ReferenceId = 'DeleteArticleLink')]
[OpenApiExtension('x-kestrun-demo', '{"stability":"beta","audience":"public","headers":["X-Correlation-Id"],"links":["get","delete"],"kind":"success"}')]
[SuccessResponse]$OK = NoDefault

[OpenApiResponseComponent(Description = 'Resource created successfully', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseLinkRef(Key = 'get', ReferenceId = 'GetArticleLink')]
[OpenApiResponseLinkRef(Key = 'delete', ReferenceId = 'DeleteArticleLink')]
[OpenApiResponseExampleRef(Key = 'default', ReferenceId = 'ArticleCreatedResponseExample', ContentType = ('application/json', 'application/xml'))]
[SuccessResponse]$Created = NoDefault

[OpenApiResponseComponent(Description = 'Bad request - validation failed', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseExampleRef(Key = 'default', ReferenceId = 'BadRequestErrorExample', ContentType = ('application/json', 'application/xml'))]
[ErrorResponse]$BadRequest = NoDefault

[OpenApiResponseComponent(Description = 'Resource not found', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseExampleRef(Key = 'default', ReferenceId = 'NotFoundErrorExample', ContentType = ('application/json', 'application/xml'))]
[ErrorResponse]$NotFound = NoDefault

[OpenApiResponseComponent(Description = 'Article retrieved successfully', ContentType = ('application/json', 'application/xml'), Inline = $true)]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseLinkRef(Key = 'delete', ReferenceId = 'DeleteArticleLink')]
[Article]$ArticleResponsesOK = NoDefault

[OpenApiResponseComponent(Description = 'Article not found', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseExampleRef(Key = 'default', ReferenceId = 'NotFoundErrorExample', ContentType = ('application/json', 'application/xml'))]
[ErrorResponse]$ArticleResponsesNotFound = NoDefault

[OpenApiResponseComponent(Description = 'Generic object response', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
$objectResponse = NoDefault

[OpenApiResponseComponent(Description = 'A generic integer response', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[int]$intResponse = NoDefault




# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc


<#
.SYNOPSIS
    Get article by ID.
.DESCRIPTION
    Retrieves a single article. Returns Article on success or ErrorResponse on failure.
.PARAMETER articleId
    The article ID to retrieve
.NOTES
    GET article endpoint
#>
function getArticle {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/articles/{articleId}')]
    [OpenApiResponseRefAttribute(StatusCode = '200', ReferenceId = 'ArticleResponsesOK')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'BadRequest')]
    [OpenApiResponseRefAttribute(StatusCode = '404', ReferenceId = 'ArticleResponsesNotFound')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'Article ID to retrieve')]
        [int]$articleId
    )

    $Context.Response.Headers['X-Correlation-Id'] = ([Guid]::NewGuid().ToString())

    # Validate ID
    if ($articleId -le 0) {
        $myError = [ErrorResponse]@{
            statusCode = 400
            message = 'Invalid article ID'
            code = 'INVALID_ID'
            details = 'Article ID must be a positive integer'
        }
        Write-KrJsonResponse $myError -StatusCode 400
        return
    }

    if ($articleId -eq 404) {
        $myError = [ErrorResponse]@{
            statusCode = 404
            message = 'Article not found'
            code = 'NOT_FOUND'
            details = "No article exists with id $articleId"
        }
        Write-KrJsonResponse $myError -StatusCode 404
        return
    }

    # Mock article data
    $article = [Article]@{
        id = $articleId
        title = 'Getting Started with OpenAPI'
        content = 'OpenAPI is a specification for building APIs...'
        publishedAt = '2025-01-15'
        author = 'John Doe'
    }
    Write-KrResponse $article -StatusCode 200
}



<#
.SYNOPSIS
    Create a new article.
.DESCRIPTION
    Creates a new article and returns success response or error.
.PARAMETER body
    Article data (title and content required)
.NOTES
    POST article endpoint
#>
function createArticle {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/articles')]
    [OpenApiResponseRefAttribute(StatusCode = '201', ReferenceId = 'Created')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'BadRequest')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [Article]$body
    )

    $Context.Response.Headers['X-Correlation-Id'] = ([Guid]::NewGuid().ToString())

    # Validate
    if (-not $body.title -or -not $body.content) {
        $myError = [ErrorResponse]@{
            statusCode = 400
            message = 'Validation failed'
            code = 'VALIDATION_ERROR'
            details = 'title and content are required'
        }
        Write-KrJsonResponse $myError -StatusCode 400
        return
    }

    # Success response
    $success = [SuccessResponse]@{
        id = 1
        message = 'Article created successfully'
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $success -StatusCode 201
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
    [OpenApiResponseRefAttribute(StatusCode = '200', ReferenceId = 'OK')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'BadRequest')]
    [OpenApiResponseRefAttribute(StatusCode = '404', ReferenceId = 'NotFound')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'Article ID to delete')]
        [int]$articleId
    )

    $Context.Response.Headers['X-Correlation-Id'] = ([Guid]::NewGuid().ToString())

    if ($articleId -le 0) {
        $myError = [ErrorResponse]@{
            statusCode = 400
            message = 'Invalid article ID'
            code = 'INVALID_ID'
            details = 'Article ID must be a positive integer'
        }
        Write-KrJsonResponse $myError -StatusCode 400
        return
    }

    if ($articleId -eq 404) {
        $myError = [ErrorResponse]@{
            statusCode = 404
            message = 'Article not found'
            code = 'NOT_FOUND'
            details = "No article exists with id $articleId"
        }
        Write-KrJsonResponse $myError -StatusCode 404
        return
    }

    $success = [SuccessResponse]@{
        id = $articleId
        message = "Article $articleId deleted successfully"
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $success -StatusCode 200
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
