#requires -Module Kestrun
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

New-KrLogger | Add-KrSinkConsole | Set-KrLoggerLevel Debug | Register-KrLogger console -SetAsDefault | Out-Null
$srv = New-KrServer -Name 'Petstore 3.1 (Kestrun Edition)' -PassThru

# --- Top-level OpenAPI ---
Add-KrOpenApiInfo -Title 'Swagger Petstore (3.1 Deluxe)' -Version '1.0.0' -Description 'OAS 3.1 Petstore with pagination, links, headers.'
Add-KrOpenApiContact -Name 'API Support' -Url 'https://example.com/support' -Email 'support@example.com'
Add-KrOpenApiLicense -Name 'MIT' -Url 'https://opensource.org/licenses/MIT' -Identifier 'MIT'
Add-KrOpenApiExternalDoc -Description 'OpenAPI 3.1 Spec' -Url 'https://spec.openapis.org/oas/v3.1.0'
Add-KrOpenApiTag -Name 'pets' -Description 'Manage your furry (and scaly) friends'
$envVar = New-KrOpenApiServerVariable -Name env -Default 'prod' -Enum @('dev', 'staging', 'prod') -Description 'Environment'
Add-KrOpenApiServer -Url 'https://{env}.api.petstore.example.com/v1' -Description 'Main API' -Variables $envVar

# --- Schemas ---

# Problem per RFC 7807 (true JSON Schema in 3.1)
[OpenApiSchemaComponent(Required = 'title,status', AdditionalPropertiesAllowed = $true)]
class Problem {
    [OpenApiPropertyAttribute(Format = 'uri')]
    [string]$type = 'about:blank'
    [string]$title
    [ValidateRange(100, 599)]
    [int]$status = 0
    [string]$detail
    [OpenApiPropertyAttribute(Format = 'uri')]
    [string]$instance
}

[OpenApiSchemaComponent(Description = 'Cat', Required = 'id')]
[OpenApiSchemaComponent(Required = 'name')]
[OpenApiSchemaComponent(Required = 'kind')]
class Cat {
    [OpenApiPropertyAttribute(Format = 'int64')]
    [long]$id
    [OpenApiPropertyAttribute(MinLength = 1)]
    [string]$name
    [ValidateSet('cat')]
    [string]$kind = 'cat'
    [string]$tag
    [ValidateSet('soft', 'loud')]
    [string]$meowStyle
}

[OpenApiSchemaComponent(Description = 'Dog', Required = 'id')]
[OpenApiSchemaComponent(Required = 'name')]
[OpenApiSchemaComponent(Required = 'kind')]
class Dog {
    [OpenApiPropertyAttribute(Format = 'int64')]
    [long]$id
    [OpenApiPropertyAttribute(MinLength = 1)]
    [string]$name
    [ValidateSet('dog')]
    [string]$kind = 'dog'
    [string]$tag
    [ValidateSet('yappy', 'deep')]
    [string]$barkStyle
}

[OpenApiSchemaComponent(Description = 'A pet (cat or dog)', Required = 'id')]
[OpenApiSchemaComponent(Required = 'name')]
[OpenApiSchemaComponent(Required = 'kind')]
class Pet {
    [OpenApiPropertyAttribute(Format = 'int64')]
    [long]$id
    [OpenApiPropertyAttribute(MinLength = 1)]
    [string]$name
    [ValidateSet('cat', 'dog')]
    [string]$kind
    [string]$tag
    [OpenApiPropertyAttribute(Nullable = $true)]
    [string]$barkStyle
    [OpenApiPropertyAttribute(Nullable = $true)]
    [string]$meowStyle
}
<#
[OpenApiSchemaComponent(Description = 'Pet creation payload', Required = 'name')]
[OpenApiSchemaComponent(Required = 'kind')]
class NewPet {
    [OpenApiPropertyAttribute(MinLength = 1)]
    [string]$name
    [ValidateSet('cat', 'dog')]
    [string]$kind
    [OpenApiPropertyAttribute(Format = 'uri')]
    [string]$statusCallbackUrl
    [OpenApiPropertyAttribute(Nullable = $true)]
    [string]$tag
    [OpenApiPropertyAttribute(Nullable = $true)]
    [ValidateSet('soft', 'loud')]
    [string]$meowStyle
    [OpenApiPropertyAttribute(Nullable = $true)]
    [ValidateSet('yappy', 'deep')]
    [string]$barkStyle
}#>

[OpenApiSchemaComponent(Description = 'A page of pets', Required = 'items')]
[OpenApiSchemaComponent(Required = 'page')]
class PetPage {
    [OpenApiPropertyAttribute()]
    [Pet[]]$items
    [OpenApiPropertyAttribute(Required = 'size')]
    [hashtable]$page = @{ size = 25; nextCursor = $null }
}

# --- Examples ---
[OpenApiExampleComponent(Summary = 'A new kitten')] class NewPetExample {
    [string]$name = 'Luna'
    [string]$kind = 'cat'
    [string]$meowStyle = 'soft'
}
[OpenApiExampleComponent(Summary = 'Page of two pets')]
class PetPageSample {
    #$value =
    #@{
    $items = @(
        @{id = 1; name = 'Luna'; kind = 'cat' },
        @{id = 2; name = 'Bruno'; kind = 'dog' }
    )
    $page = @{size = 25; nextCursor = 'abc123' }
    # }
}

# --- Headers (reusable, referenced by Key) ---
[OpenApiHeaderComponent()]
class CommonHeaders {
    [OpenApiHeader(Key = 'X-Request-ID', Description = 'Correlation id (UUID)', Required = $true)]
    [string]$XRequestId
    [OpenApiHeader(Key = 'Location', Description = 'URL of the created resource', Required = $true)]
    [string]$Location
    [OpenApiHeader(Key = 'Link', Description = 'Pagination links per RFC 5988', Required = $true)]
    [string]$Link
}

# --- Parameters ---
[OpenApiParameterComponent()]
class Params_Pets {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Page size (1-100)')]
    [OpenApiPropertyAttribute(Minimum = 1, Maximum = 100)]
    [int]$limit = 25
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Opaque pagination cursor')]
    [string]$cursor
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Filter by tag')]
    [string]$tag
}
[OpenApiParameterComponent()]
class Param_PetId {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Description = 'The pet identifier')]
    [OpenApiPropertyAttribute(Format = 'int64', Minimum = 1)]
    [long]$petId
}

# --- Links ---
[OpenApiLinkComponent()]
class PetLinks {
    [OpenApiLinkAttribute(Description = 'Fetch a pet by id after creation', OperationId = 'getPetById')]
    [OpenApiLinkAttribute(MapKey = 'petId', MapValue = '$response.header.Location')]
    $GetPetById
}

# --- Responses (headers live **inside** responses) ---
[OpenApiResponseComponent(JoinClassName = '-', Description = 'Common problem response')]
class ProblemResponse {
    [OpenApiResponseAttribute(Description = 'RFC 7807 Problem Details')]
    [OpenApiContentTypeAttribute(ContentType = 'application/problem+json', ReferenceId = 'Problem')]
    [Problem]$Default
}

[OpenApiResponseComponent(JoinClassName = '-', Description = 'Pet responses')]
class PetResponses {
    # 200 (list)
    [OpenApiResponseAttribute(Description = 'A page of pets')]
    [OpenApiHeaderRefAttribute(Key = 'X-Request-ID', ReferenceId = 'X-Request-ID')]
    [OpenApiHeaderRefAttribute(Key = 'Link', ReferenceId = 'Link')]
    [OpenApiExampleRefAttribute(Key = 'sample', ReferenceId = 'PetPageSample')]
    [PetPage]$OK

    # 200 (single)
    [OpenApiResponseAttribute(Description = 'The pet')]
    [OpenApiHeaderRefAttribute(Key = 'X-Request-ID', ReferenceId = 'X-Request-ID')]
    [Pet]$PetOK

    # 404
    [OpenApiResponseAttribute(Description = 'Not found')]
    [OpenApiContentTypeAttribute(ContentType = 'application/problem+json', ReferenceId = 'Problem')]
    [Problem]$NotFound
}

[OpenApiResponseComponent(JoinClassName = '-', Description = 'Create pet 201')]
class CreatePetResponse {
    [OpenApiResponseAttribute(Description = 'Created')]
    [OpenApiHeaderRefAttribute(Key = 'Location', ReferenceId = 'Location')]
    [OpenApiHeaderRefAttribute(Key = 'X-Request-ID', ReferenceId = 'X-Request-ID')]
    [OpenApiLinkRefAttribute(Key = 'GetPetById', ReferenceId = 'GetPetById')]
    [object]$Created
}

# --- Request Body ---
[OpenApiRequestBodyComponent(Description = 'Pet creation payload', Required = $true, Inline = $true)]
[OpenApiRequestBodyComponent(ContentType = 'application/json')]
[OpenApiExampleRefAttribute(Key = 'kitten', ReferenceId = 'NewPetExample')]
class CreatePetBody {
    [OpenApiPropertyAttribute(MinLength = 1, Required = $true)]
    [string]$name
    [OpenApiPropertyAttribute(Required = $true)]
    [ValidateSet('cat', 'dog')]
    [string]$kind
    [OpenApiPropertyAttribute(Format = 'uri')]
    [string]$statusCallbackUrl
    [OpenApiPropertyAttribute(Nullable = $true)]
    [string]$tag
    [OpenApiPropertyAttribute(Nullable = $true)]
    [ValidateSet('soft', 'loud')]
    [string]$meowStyle
    [OpenApiPropertyAttribute(Nullable = $true)]
    [ValidateSet('yappy', 'deep')]
    [string]$barkStyle
    #[OpenApiPropertyAttribute()]
    #   [NewPet]$value = [NewPet]@{ name = 'Luna'; kind = 'cat'; meowStyle = 'soft' }
}

# --- Request Body: JSON Merge Patch for Pet ---
[OpenApiRequestBodyComponent(Description = 'JSON Merge Patch for updating a pet', Required = $true, Inline = $true)]
[OpenApiRequestBodyComponent(ContentType = 'application/merge-patch+json')]
class PatchPetBody {
    # Minimal example payload; clients may include any subset of these fields.
    [OpenApiPropertyAttribute(Description = 'Partial fields to update')]
    [hashtable]$value = @{
        name = 'New name'
        tag = $null
    }
}


# --- Finalize config & UI ---
Enable-KrConfiguration
Add-KrSwaggerUiRoute

# --- Routes ---
# /pets
New-KrMapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/pets' |
    Add-KrMapRouteScriptBlock -ScriptBlock {
        if ($KrRequest.Method -eq 'GET') {
            $items = @(
                @{ id = 1; name = 'Luna'; kind = 'cat'; meowStyle = 'soft' },
                @{ id = 2; name = 'Bruno'; kind = 'dog'; barkStyle = 'deep' }
            )
            $page = @{ size = [int]($KrRequest.Query.limit ?? 25); nextCursor = 'abc123' }
            Write-KrJsonResponse @{ items = $items; page = $page }
        } else {
            $id = 101
            Write-KrHeader -Name 'Location' -Value ('https://{0}/pets/{1}' -f $KrRequest.Host, $id)
            Write-KrStatusResponse -StatusCode 201
        }
    } |
    Add-KrMapRouteOpenApiTag -Tag 'pets' |
    # GET
    Add-KrMapRouteOpenApiInfo -Verbs 'GET' -Summary 'List pets' -Description 'Returns a paginated list of pets.' -OperationId 'listPets' |
    Add-KrMapRouteOpenApiParameter -Verbs 'GET' -ReferenceId 'limit' |
    Add-KrMapRouteOpenApiParameter -Verbs 'GET' -ReferenceId 'cursor' |
    Add-KrMapRouteOpenApiParameter -Verbs 'GET' -ReferenceId 'tag' -Embed |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode '200' -Description 'A page of pets' -ReferenceId 'PetResponses-OK' -Embed |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode 'default' -Description 'Problem' -ReferenceId 'ProblemResponse-Default' |
    # POST
    Add-KrMapRouteOpenApiInfo -Verbs 'POST' -Summary 'Create a pet' -OperationId 'createPet' |
    Add-KrMapRouteOpenApiRequestBody -Verbs 'POST' -Description 'Pet to add' -ReferenceId 'CreatePetBody' -Embed |
    Add-KrMapRouteOpenApiResponse -Verbs 'POST' -StatusCode '201' -Description 'Created' -ReferenceId 'CreatePetResponse-Created' |
    Add-KrMapRouteOpenApiResponse -Verbs 'POST' -StatusCode 'default' -Description 'RFC 7807 Problem Details' -ReferenceId 'ProblemResponse-Default' |
    Build-KrMapRoute

# /pets/{petId}
New-KrMapRouteBuilder -Verbs @('GET', 'PATCH', 'DELETE') -Pattern '/pets/{petId}' |
    Add-KrMapRouteScriptBlock -ScriptBlock {
        $petId = Get-KrRequestRouteParam -Name 'petId' -AsInt
        switch ($KrRequest.Method) {
            'GET' {
                if ($petId -eq 404) { Write-KrStatusResponse 404; return }
                Write-KrJsonResponse @{ id = $petId; name = 'Spots'; kind = 'dog'; barkStyle = 'deep' }
            }
            'PATCH' {
                $patch = Get-KrRequestBody -AsJson
                Write-KrJsonResponse @{ id = $petId; name = ($patch.name ?? 'Spots'); kind = 'dog'; barkStyle = 'deep' }
            }
            'DELETE' { Write-KrStatusResponse 204 }
        }
    } |
    Add-KrMapRouteOpenApiTag -Tag 'pets' |
    Add-KrMapRouteOpenApiParameter -ReferenceId 'petId' |
    Add-KrMapRouteOpenApiInfo -Verbs 'GET' -Summary 'Get a pet by id' -OperationId 'getPetById' |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode '200' -Description 'The pet' -ReferenceId 'PetResponses-PetOK' -Embed |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode '404' -Description 'Not found' -ReferenceId 'PetResponses-NotFound' -Embed |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode 'default' -Description 'Problem' -ReferenceId 'ProblemResponse-Default' |
    # PATCH
    Add-KrMapRouteOpenApiInfo -Verbs 'PATCH' -Summary 'Update part of a pet (merge-patch)' -OperationId 'patchPet' |
    Add-KrMapRouteOpenApiRequestBody -Verbs 'PATCH' -Description 'JSON Merge Patch' -ReferenceId 'PatchPetBody' -Embed |
    Add-KrMapRouteOpenApiResponse -Verbs 'PATCH' -StatusCode '200' -Description 'Updated pet' -ReferenceId 'PetResponses-PetOK' |
    Add-KrMapRouteOpenApiResponse -Verbs 'PATCH' -StatusCode 'default' -Description 'Problem' -ReferenceId 'ProblemResponse-Default' |
    # DELETE
    Add-KrMapRouteOpenApiInfo -Verbs 'DELETE' -Summary 'Delete a pet' -OperationId 'deletePet' |
    Add-KrMapRouteOpenApiResponse -Verbs 'DELETE' -StatusCode '204' -Description 'Deleted' |
    Add-KrMapRouteOpenApiResponse -Verbs 'DELETE' -StatusCode 'default' -Description 'Problem' -ReferenceId 'ProblemResponse-Default' |
    Build-KrMapRoute

# --- OpenAPI doc routes / server start ---
Add-KrOpenApiRoute -Pattern '/openapi/{version}/openapi.{format}'
Build-KrOpenApiDocument
Test-KrOpenApiDocument

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Start-KrServer -Server $srv -CloseLogsOnExit
