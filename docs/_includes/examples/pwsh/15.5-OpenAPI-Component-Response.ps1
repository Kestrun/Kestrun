<#
    Sample: OpenAPI Response Components
    Purpose: Demonstrate reusable response components with multiple response types per component.
    File:    15.5-OpenAPI-Component-Response.ps1
    Notes:   Shows inline responses, generic object responses, and schema references within attributes.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI Response Component' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Response Component API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable response components.'


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

# Response component for common success responses (200, 201)
[OpenApiResponseComponent(JoinClassName = '-', Description = 'Success responses')]
class SuccessResponses {
    [OpenApiResponseAttribute(Description = 'Operation completed successfully', ContentType = ('application/json', 'application/xml'))]
    [SuccessResponse]$OK

    [OpenApiResponseAttribute(Description = 'Resource created successfully' , ContentType = ('application/json', 'application/xml'))]
    [SuccessResponse]$Created
}


# Response component for common error responses (400, 404)
[OpenApiResponseComponent(JoinClassName = '-', Description = 'Error responses')]
class ErrorResponses {
    [OpenApiResponseAttribute(Description = 'Bad request - validation failed', ContentType = ('application/json', 'application/xml'))]
    [ErrorResponse]$BadRequest

    [OpenApiResponseAttribute(Description = 'Resource not found', ContentType = ('application/json', 'application/xml'))]
    [ErrorResponse]$NotFound
}

# Response component for article responses
[OpenApiResponseComponent( Description = 'Article responses')]
class ArticleResponses {
    [OpenApiResponseAttribute(Description = 'Article retrieved successfully', ContentType = ('application/json', 'application/xml'), inline = $true)]
    [Article]$ArticleResponsesOK

    [OpenApiResponseAttribute(Description = 'Article not found', ContentType = ('application/json', 'application/xml'))]
    [ErrorResponse] $ArticleResponsesNotFound

    [OpenApiResponseAttribute()]
    $objectResponse
}

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
    [OpenApiResponseRefAttribute(StatusCode = '404', ReferenceId = 'ArticleResponsesNotFound')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'Article ID to retrieve')]
        [int]$articleId
    )

    # Validate ID
    if ($articleId -le 0) {
        $myError = @{
            statusCode = 400
            message = 'Invalid article ID'
            code = 'INVALID_ID'
            details = 'Article ID must be a positive integer'
        }
        Write-KrJsonResponse $myError -StatusCode 400
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
    [OpenApiResponseRefAttribute(StatusCode = '201', ReferenceId = 'SuccessResponses-Created')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'ErrorResponses-BadRequest')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [Article]$body
    )

    # Validate
    if (-not $body.title -or -not $body.content) {
        $myError = @{
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
        id = [System.Guid]::NewGuid().ToString()
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
    [OpenApiResponseRefAttribute(StatusCode = '200', ReferenceId = 'SuccessResponses-OK')]
    [OpenApiResponseRefAttribute(StatusCode = '404', ReferenceId = 'ErrorResponses-NotFound')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'Article ID to delete')]
        [int]$articleId
    )

    $success = [SuccessResponse]@{
        id = [System.Guid]::NewGuid().ToString()
        message = "Article $articleId deleted successfully"
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $success -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -Server $srv -CloseLogsOnExit
