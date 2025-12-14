<#
    Sample: Petstore with Components
    Purpose: To demonstrate the use of reusable request header components with multiple content types in a sample Petstore application.
    File:    10.14-PetStore.ps1
    Notes:   This sample demonstrates how to use reusable request header components with multiple content types.
     It includes a variety of operations and schemas, including user management, pet management, and order processing.
     The sample also includes security schemes for API key and OAuth2 authentication.
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
$srv = New-KrServer -Name 'Swagger Petstore - OpenAPI 3.1' -PassThru
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI (3.0.4)
# =========================================================

Add-KrOpenApiInfo -Title 'Swagger Petstore - OpenAPI 3.1' `
    -Version '1.0.12' `
    -Description @'
This is a sample Pet Store Server based on the OpenAPI 3.1 specification.  You can find out more about Swagger at [https://swagger.io](https://swagger.io).
In the third iteration of the pet store, we've switched to the design first approach!
You can now help us improve the API whether it's by making changes to the definition itself or to the code.
That way, with time, we can improve the API in general, and expose some of the new features in OAS3.

Some useful links:
- [The Pet Store repository](https://github.com/swagger-api/swagger-petstore)
- [The source API definition for the Pet Store](https://github.com/swagger-api/swagger-petstore/blob/master/src/main/resources/openapi.yaml)
'@ `
    -TermsOfService 'https://swagger.io/terms/'

Add-KrOpenApiContact -Email 'apiteam@swagger.io'
Add-KrOpenApiLicense -Name 'Apache 2.0' -Url 'https://www.apache.org/licenses/LICENSE-2.0.html'
Add-KrOpenApiExternalDoc -Description 'Find out more about Swagger' -Url 'https://swagger.io'

#Add-KrOpenApiServer -Url 'https://petstore3.swagger.io/api/v3'

# Tags (with externalDocs where applicable)
Add-KrOpenApiTag -Name 'pet' -Description 'Everything about your Pets' -ExternalDocs (New-KrOpenApiExternalDoc -Description 'Find out more' -Url 'https://swagger.io')
Add-KrOpenApiTag -Name 'store' -Description 'Access to Petstore orders' -ExternalDocs (New-KrOpenApiExternalDoc -Description 'Find out more about our store' -Url 'https://swagger.io')
Add-KrOpenApiTag -Name 'user' -Description 'Operations about user'

# Add server pointing to local instance
Add-KrOpenApiServer -Url '/' -Description 'Local Kestrun Server'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

# Category
[OpenApiSchemaComponent()]
class Category {
    [long]$id
    [string]$name
}

# Tag
[OpenApiSchemaComponent()]
class Tag {
    [long]$id
    [string]$name
}

# User
[OpenApiSchemaComponent()]
class User {
    [long]$id
    [string]$username
    [string]$firstName
    [string]$lastName
    [string]$email
    [string]$password
    [string]$phone
    [int]$userStatus
}

# ApiResponse
[OpenApiSchemaComponent()]
class ApiResponse {
    [int]$code
    [string]$type
    [string]$message
}

# Error
[OpenApiSchemaComponent(Required = ('code', 'message'))]
class Error {
    [string]$code
    [string]$message
}

<#
.SYNOPSIS
    Pet schema
.DESCRIPTION
    A pet for sale in the pet store
.PARAMETER id
    Unique identifier for the pet
.PARAMETER name
    Name of the pet
.PARAMETER category
    Category the pet belongs to
.PARAMETER photoUrls
    Photo URLs of the pet
.PARAMETER tags
    Tags associated with the pet
.PARAMETER status
    Pet status in the store
#>
[OpenApiSchemaComponent(Required = ('name', 'photoUrls'))]
class Pet {
    [long]$id
    [string]$name
    [Category]$category
    [OpenApiProperty()]
    [string[]]$photoUrls
    [OpenApiProperty()]
    [Tag[]]$tags
    [ValidateSet('available', 'pending', 'sold')]
    [string]$status
}

# Order
[OpenApiSchemaComponent()]
class Order {
    [long]$id
    [long]$petId
    [int]$quantity
    [OpenApiProperty(Format = 'date-time')]
    [string]$shipDate
    [ValidateSet('placed', 'approved', 'delivered')]
    [string]$status
    [bool]$complete
}

[OpenApiSchemaComponent( Description = 'Inventory counts by status')]
class Inventory {
    [OpenApiAdditionalProperties()]
    [int]$AdditionalProperties
}

#region COMPONENT REQUEST BODIES
# =========================================================
#                 COMPONENT REQUEST BODIES
# =========================================================

# RequestBody: UserArray
[OpenApiRequestBodyComponent(Description = 'List of user object', IsRequired = $true, ContentType = 'application/json' , Array = $true)]
class UserArray:User {}

# RequestBody: PetBody
[OpenApiRequestBodyComponent(Description = 'Pet object that needs to be added to the store', IsRequired = $true,
    ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
class PetBody:Pet {}
#endregion

#region COMPONENT PARAMETERS
# =========================
# COMPONENT: PARAMETERS
# =========================

#endregion
#region COMPONENT RESPONSES
# =========================
# COMPONENT: RESPONSES
# =========================

[OpenApiResponseComponent( )]
class ResponseDefault {

    [OpenApiResponse(Description = 'Unexpected error', ContentType = 'application/json')]
    [Error]$Default
}


#endregion
#region COMPONENT SECURITY SCHEMES

# 6. Script-based validation


# =========================================================
#                 SECURITY SCHEMES
# =========================================================
Add-KrApiKeyAuthentication -AuthenticationScheme 'api_key' -AllowInsecureHttp -ApiKeyName 'api_key' -ScriptBlock {
    param($ProvidedKey) $ProvidedKey -eq 'my-secret-api-key' }


$claimPolicy = New-KrClaimPolicy |
    Add-KrClaimPolicy -PolicyName 'read:pets' -Scope -Description 'read your pets' |
    Add-KrClaimPolicy -PolicyName 'write:pets' -Scope -Description 'modify pets in your account' |
    Build-KrClaimPolicy

Add-KrOAuth2Authentication -AuthenticationScheme 'petstore_auth' `
    -ClientId 'zL91HbWioaNrqAldrVyPGjJHIxODaYj4' `
    -ClientSecret 'your-client-secret' `
    -AuthorizationEndpoint 'https://petstore3.swagger.io/oauth/authorize' `
    -TokenEndpoint 'https://your-auth-server/oauth/token' `
    -CallbackPath '/signin-petstore' `
    -SaveTokens `
    -ClaimPolicy $claimPolicy

#Add-KrCorsPolicyMiddleware -AllowAll -Name '_OpenApiCorsPolicy2'

#endregion
#region ROUTES / OPERATIONS
# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================
Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc
Add-KrApiDocumentationRoute -DocumentType Scalar
Add-KrApiDocumentationRoute -DocumentType Rapidoc
Add-KrApiDocumentationRoute -DocumentType Elements

<#
.SYNOPSIS
    Update an existing pet
.DESCRIPTION
    Update an existing pet by Id.
.PARAMETER pet
    Update an existent pet in the store
#>
function updatePet {
    [OpenApiPath(HttpVerb = 'Put' , Pattern = '/pet', Tags = 'pet')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation' , Schema = [Pet] , ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponse(StatusCode = '404' , Description = 'Pet not found' )]
    [OpenApiResponse(StatusCode = '422' , Description = 'Validation exception' )]
    [OpenApiResponseRef(StatusCode = 'default' , ReferenceId = 'Default' , Inline = $true)]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiRequestBody(Required = $true, Inline = $false,
            ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [Pet]$pet
    )
    # Stub handler; the doc is our star tonight.
    Write-Host 'updatePet called'
    Expand-KrObject -InputObject $pet -Label 'Received Pet:'
    Write-KrJsonResponse @{ ok = $true }
}

<#
.SYNOPSIS
    Add a new pet to the store
.DESCRIPTION
    Add a new pet to the store.
.PARAMETER pet
    Create a new pet in the store
#>
function addPet {
    [OpenApiPath(HttpVerb = 'Post' , Pattern = '/pet', Tags = 'pet')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation' , Schema = [Pet] , ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid input' )]
    [OpenApiResponse(StatusCode = '422' , Description = 'Validation exception' )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default' , Inline = $true)]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]

    param(
        [OpenApiRequestBody(Required = $true,
            ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [Pet] $pet
    )
    # Stub handler; the doc is our star tonight.

    Expand-KrObject -InputObject $pet -Label 'Received Pet:'
    Write-KrJsonResponse $pet -StatusCode 200
}

<#
.SYNOPSIS
    Finds Pets by status
.DESCRIPTION
    Multiple status values can be provided with comma separated strings.
.PARAMETER status
    Status values that need to be considered for filter
#>
function findPetsByStatus {
    [OpenApiPath(HttpVerb = 'Get' , Pattern = '/pet/findByStatus', Tags = 'pet')]
    [OpenApiResponse(StatusCode = '200' , Description = 'successful operation', Schema = [Pet[]] , ContentType = ('application/json', 'application/xml') )]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid status value')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        #[OpenApiParameterRef(ReferenceId = 'FindByStatusParams-status' )]
        [OpenApiParameter(In = [OaParameterLocation]::Query, Explode = $true)]
        [ValidateSet('available', 'pending', 'sold')]
        [string]$status = 'available'
    )
    Write-Host "FindByStatus called with status='$status'"
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Finds Pets by tags
.DESCRIPTION
    Multiple tags can be provided with comma separated strings.
.PARAMETER tags
    Tags to filter by
#>
function findPetsByTags {
    [OpenApiPath(HttpVerb = 'Get' , Pattern = '/pet/findByTags', Tags = 'pet')]
    [OpenApiResponse(StatusCode = '200' , Description = 'successful operation', Schema = [Pet[]] ,
        ContentType = ('application/json', 'application/xml'))]

    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid tag value')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Query, Explode = $true, Required = $false)]
        [string[]]$tags
    )
    Write-Host "FindByTags called with tags='$tags'"
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Find pet by ID
.DESCRIPTION
    Returns a single pet.
.PARAMETER petId
    ID of pet to return
#>
function getPetById {
    [OpenApiPath(HttpVerb = 'Get' , Pattern = '/pet/{petId}', Tags = 'pet')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Pet' )]
    [OpenApiResponse( StatusCode = '400' , Description = 'Invalid ID supplied')]
    [OpenApiResponse( StatusCode = '404' , Description = 'Pet not found')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path , Required = $true)]
        [long]$petId
    )
    Write-Host "getPetById called with petId='$petId'"
    $pet = [Pet]::new()
    $pet | ConvertTo-Json | Write-Host
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Updates a pet in the store with form data
.DESCRIPTION
    Updates a pet resource based on the form data.
.PARAMETER petId
    ID of pet that needs to be updated
.PARAMETER name
    Name of pet that needs to be updated
.PARAMETER status
    Status of pet that needs to be updated
#>
function updatePetWithForm {
    [OpenApiPath(HttpVerb = 'Post' , Pattern = '/pet/{petId}', Tags = 'pet')]
    [OpenApiResponse( StatusCode = '200' , Description = 'successfully updated')]
    [OpenApiResponse( StatusCode = '400' , Description = 'Invalid input')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path  , Required = $true)]
        [long]$petId,
        [OpenApiParameter(In = [OaParameterLocation]::Query  )]
        [string]$name,
        [OpenApiParameter(In = [OaParameterLocation]::Query  )]
        [string]  $status
    )

    Write-Host "updatePetWithForm called with name='$name' and status='$status',  petId='$petId'"
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Deletes a pet
.DESCRIPTION
    delete a pet.
.PARAMETER Api_key

.PARAMETER petId
    ID of pet to delete
#>
function deletePet {
    [OpenApiPath(HttpVerb = 'Delete' , Pattern = '/pet/{petId}', Tags = 'pet')]
    [OpenApiResponse( StatusCode = '200' , Description = 'Successful operation' )]
    [OpenApiResponse( StatusCode = '400' , Description = 'Invalid pet value' )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameter( In = [OaParameterLocation]::Header , Required = $false )]
        [string]$api_key,
        [OpenApiParameter( In = [OaParameterLocation]::Path, Required = $true )]
        [long]$petId
    )

    Write-Host "deletePet called with petId='$petId' and api_key='$Api_key'"
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Uploads an image
.DESCRIPTION
    Uploads an image.
.PARAMETER petId
    ID of pet to update
.PARAMETER AdditionalMetadata
    Additional Metadata
.PARAMETER Image
    Upload an image file
#>
function uploadFile {
    [OpenApiPath(HttpVerb = 'Post' , Pattern = '/pet/{petId}/uploadImage', Tags = 'pet')]
    [OpenApiResponse( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'ApiResponse', ContentType = ('application/json'))]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true )]
        [long]$petId,

        [OpenApiParameter(In = [OaParameterLocation]::Query, Required = $false )]
        [string]$additionalMetadata,

        [OpenApiRequestBody( ContentType = 'application/octet-stream' )]
        [byte[]]$Image
    )

    Write-Host "uploadImage called with additionalMetadata='$AdditionalMetadata' and petId='$PetId'"
    Write-Host "Image size: $($Image.Length) bytes"
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Returns pet inventories by status
.DESCRIPTION
    Returns a map of status codes to quantities.
#>
function getInventory {
    [OpenApiPath(HttpVerb = 'Get' , Pattern = '/store/inventory', Tags = 'store')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Inventory', Inline = $true )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiAuthorization( Scheme = 'api_key')]
    param()
    Write-Host 'getInventory called'
    Write-KrJsonResponse @{} -StatusCode 200
}

<#
.SYNOPSIS
    Place an order for a pet
.DESCRIPTION
    Place a new order in the store.
.PARAMETER order
    order placed for purchasing the pet
#>
function placeOrder {
    [OpenApiPath(HttpVerb = 'Post' , Pattern = '/store/order', Tags = 'store')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Order' )]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid input' )]
    [OpenApiResponse(StatusCode = '422' , Description = 'Validation Exception' )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [Order]$Order
    )
    Write-Host "placeOrder called with order='$Order'"
    Write-KrJsonResponse @{} -StatusCode 200
}

<#
.SYNOPSIS
    Find purchase order by ID
.DESCRIPTION
    For valid response try integer IDs with value <= 5 or > 10. Other values will generate exceptions.
.PARAMETER orderId
    ID of order that needs to be fetched
#>
function getOrderById {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/store/order/{orderId}', Tags = 'store')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Order', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponse(StatusCode = '404' , Description = 'Order not found' )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiParameter( In = [OaParameterLocation]::Path, Required = $true )]
        [long]$orderId
    )
    Write-Host "getOrderById called with orderId='$orderId'"
    $order = [Order]::new()
    $order.id = $orderId
    Write-KrJsonResponse $order -StatusCode 200
}

<#
.SYNOPSIS
    Delete purchase order by ID
.DESCRIPTION
    For valid response try integer IDs with value < 1000. Anything above 1000 or non-integers will generate API errors.
.PARAMETER orderId
    ID of the order that needs to be deleted
#>
function deleteOrder {
    [OpenApiPath(HttpVerb = 'delete' , Pattern = '/store/order/{orderId}', Tags = 'store')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation')]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponse(StatusCode = '404' , Description = 'Order not found' )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiParameter( In = [OaParameterLocation]::Path, Required = $true )]
        [long]$orderId
    )
    Write-Host "deleteOrder called with orderId='$orderId'"
    Write-KrStatusResponse -StatusCode 200
}

<#
.SYNOPSIS
    Create user
.DESCRIPTION
    This can only be done by the logged in user.
.PARAMETER user
    Created user object
#>
function createUser {
    [OpenApiPath(HttpVerb = 'post' , Pattern = '/user', Tags = 'user' )]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [User]$User
    )
    Write-Host "createUser called with user='$User'"
    Write-KrJsonResponse $User -StatusCode 200
}

<#
.SYNOPSIS
    Creates list of users with given input array
.DESCRIPTION
    Creates list of users with given input array.
.PARAMETER users
    List of user objects
#>
function createUsersWithListInput {
    [OpenApiPath(HttpVerb = 'post' , Pattern = '/user/createWithList', Tags = 'user' )]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiRequestBody(ContentType = 'application/json', Inline = $false)]
        [User[]]$Users
    )
    Write-Host "createUsersWithListInput called with users='$Users'"
    Write-KrJsonResponse $Users[0] -StatusCode 200
}

<#
.SYNOPSIS
    Logs user into the system
.DESCRIPTION
    log user into the system.
.PARAMETER username
    The user name for login
.PARAMETER password
    The password for login in clear text
#>
function loginUser {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingUsernameAndPasswordParams', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', '')]
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/user/login', Tags = 'user')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation' , Schema = [string], ContentType = ('application/json', 'application/xml'))]
    ##   [OpenApiHeader( StatusCode = '200' , Key = 'X-Expires-After', Description = 'date in UTC when token expires')]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid username/password supplied' )]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameter( In = [OaParameterLocation]::Query, Required = $false )]
        [string]$username,

        [OpenApiParameter( In = [OaParameterLocation]::Query, Required = $false )]
        [string]$password
    )
    Write-Host "loginUser called with username='$username' and password='$password'"
    Write-KrStatusResponse -StatusCode 200
}

<#
.SYNOPSIS
    Logs out current logged in user session
.DESCRIPTION
    Log user out of the system.
#>
function logoutUser {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/user/logout', Tags = 'user')]
    [OpenApiResponse(StatusCode = '200' , Description = 'successful operation' )]
    [OpenApiResponse( StatusCode = 'default' , Description = 'successful operation' )]
    param(
    )
    Write-Host 'logout called'
    Write-KrStatusResponse -StatusCode 200
}
<#
.SYNOPSIS
    Get user by user name
.DESCRIPTION
    Get user detail by user name.
.PARAMETER username
    The name that needs to be fetched. Use user1 for testing
#>
function getUserByName {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/user/{username}', Tags = 'user')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid username supplied')]
    [OpenApiResponse(StatusCode = '404' , Description = 'User not found')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameter( In = [OaParameterLocation]::Path, Required = $true )]
        [string]$username
    )
    Write-Host "getUserByName called with username='$username'"
    $user = [User]::new()
    Write-KrResponse $user -StatusCode 200
}

<#
.SYNOPSIS
    Update user resource
.DESCRIPTION
    This can only be done by the logged in user.
.PARAMETER username
    name that need to be updated
.PARAMETER user
    Updated user object
#>
function updateUser {
    [OpenApiPath(HttpVerb = 'put' , Pattern = '/user/{username}', Tags = 'user')]
    [OpenApiResponse(StatusCode = '200' , Description = 'Successful operation')]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid username supplied')]
    [OpenApiResponse(StatusCode = '404' , Description = 'User not found')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameter( In = [OaParameterLocation]::Path, Required = $true )]
        [string]$username,
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [User]$User
    )
    Write-Host "updateUser called with User='$User', username='$username'"
    Write-KrStatusResponse -StatusCode 200
}

<#
.SYNOPSIS
    Delete user
.DESCRIPTION
    This can only be done by the logged in user.
.PARAMETER username
    The name that needs to be deleted
#>
function deleteUser {
    [OpenApiPath(HttpVerb = 'delete' , Pattern = '/user/{username}', Tags = 'user')]
    [OpenApiResponse(StatusCode = '200' , Description = 'User deleted')]
    [OpenApiResponse(StatusCode = '400' , Description = 'Invalid username supplied')]
    [OpenApiResponse(StatusCode = '404' , Description = 'User not found')]
    [OpenApiResponseRef( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameter( In = [OaParameterLocation]::Path, Required = $true )]
        [string]$username
    )
    Write-Host "deleteUser called with username='$username'"
    Write-KrStatusResponse -StatusCode 200
}


# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================
Add-KrOpenApiRoute  # Default Pattern '/openapi/{version}/openapi.{format}'
#endregion

#region BUILD AND TEST OPENAPI DOCUMENT
# Register component requestBodies to match Pet/UserArray in official doc
# (If your builder collects from class attributes, this is already handled.)

Build-KrOpenApiDocument
Test-KrOpenApiDocument
#endregion

#region RUN SERVER
# Optional: run server (your call, you deliciously decisive creature)

Start-KrServer -Server $srv -CloseLogsOnExit
#endregion
#endregion
