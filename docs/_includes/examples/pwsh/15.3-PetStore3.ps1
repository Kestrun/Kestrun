param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault
$srv = New-KrServer -Name 'Swagger Petstore - OpenAPI 3.0' -PassThru

# =========================================================
#                 TOP-LEVEL OPENAPI (3.0.4)
# =========================================================

Add-KrOpenApiInfo -Title 'Swagger Petstore - OpenAPI 3.0' `
    -Version '1.0.12' `
    -Description @'
This is a sample Pet Store Server based on the OpenAPI 3.0 specification.  You can find out more about
Swagger at https://swagger.io. In the third iteration of the pet store, we've switched to the design first approach!
You can now help us improve the API whether it's by making changes to the definition itself or to the code.
That way, with time, we can improve the API in general, and expose some of the new features in OAS3.

Some useful links:
- The Pet Store repository (https://github.com/swagger-api/swagger-petstore)
- The source API definition for the Pet Store (https://github.com/swagger-api/swagger-petstore/blob/master/src/main/resources/openapi.yaml)
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
[OpenApiSchemaComponent(Required = 'code,message')]
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
[OpenApiSchemaComponent(Required = 'name,photoUrls')]
class Pet {
    [long]$id
    [string]$name
    [Category]$category
    [OpenApiPropertyAttribute()]
    [string[]]$photoUrls
    [OpenApiPropertyAttribute()]
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
    [OpenApiPropertyAttribute(Format = 'date-time')]
    [string]$shipDate
    [ValidateSet('placed', 'approved', 'delivered')]
    [string]$status
    [bool]$complete
}

[OpenApiSchemaComponent(AdditionalPropertiesAllowed = $true,
    AdditionalPropertiesType = 'integer',
    AdditionalPropertiesFormat = 'int32',
    Description = 'Inventory counts by status')]
class Inventory {
}

#region COMPONENT REQUEST BODIES
# =========================================================
#                 COMPONENT REQUEST BODIES
# =========================================================

# RequestBody: UserArray
[OpenApiRequestBodyComponent(Description = 'List of user object', Required = $true, ContentType = 'application/json')]
[OpenApiPropertyAttribute(Array = $true)]
class UserArray:User {}

# RequestBody: Pet
[OpenApiRequestBodyComponent(Description = 'Pet object that needs to be added to the store', Required = $true,
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

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
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

# --------------------------------------
# /pet  (PUT, POST)
# --------------------------------------


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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation' , Schema = [Pet] , ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponseAttribute(StatusCode = '404' , Description = 'Pet not found' )]
    [OpenApiResponseAttribute(StatusCode = '422' , Description = 'Validation exception' )]
    [OpenApiResponseAttribute(StatusCode = 'default' , Description = 'Unexpected error' , ContentTypes = ('application/json'))]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiRequestBodyAttribute(Required = $true, Inline = $false,
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation' , Schema = [Pet] , ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid Input' )]
    [OpenApiResponseAttribute(StatusCode = '422' , Description = 'Validation exception' )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default' , Inline = $true)]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]

    param(
        [OpenApiRequestBodyAttribute(Required = $true,
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'successful operation', Schema = [Pet[]] , ContentTypes = ('application/json', 'application/xml') )]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid status value')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        #[OpenApiParameterRefAttribute(ReferenceId = 'FindByStatusParams-status' )]
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Explode = $true)]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'successful operation', Schema = [Pet[]] ,
        ContentTypes = ('application/json', 'application/xml'))]

    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid tag value')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Explode = $true, Required = $false)]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Pet' )]
    [OpenApiResponseAttribute( StatusCode = '404' , Description = 'Pet not found')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path , Required = $true)]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'successfully updated')]
    [OpenApiResponseAttribute( StatusCode = '400' , Description = 'Invalid input')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path  , Required = $true)]
        [long]$petId,
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query  )]
        [string]$name,
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query  )]
        [string]  $status
    )

    Write-Host "updatePetWithForm called with name='$name' and status='$status',  petId='$petId'"
    Write-KrJsonResponse @(@{}) -StatusCode 200
}

<#
.SYNOPSIS
    Deletes a pet
.DESCRIPTION
    Deletes a pet resource.
.PARAMETER Api_key

.PARAMETER petId
    ID of pet to delete
#>
function deletePet {
    [OpenApiPath(HttpVerb = 'Delete' , Pattern = '/pet/{petId}', Tags = 'pet')]
    [OpenApiResponseAttribute( StatusCode = '200' , Description = "Successful operation" )]
    [OpenApiResponseAttribute( StatusCode = '400' , Description = "Invalid pet value" )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Header , Required = $false )]
        [string]$Api_key,
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Path, Required = $true )]
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
function uploadImage {
    [OpenApiPath(HttpVerb = 'Post' , Pattern = '/pet/{petId}/uploadImage', Tags = 'pet')]
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'ApiResponse', Inline = $true , ContentTypes = ('application/json'))]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true )]
        [long]$petId,

        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Required = $false )]
        [string]$AdditionalMetadata,

        [OpenApiRequestBodyAttribute( ContentType = 'application/octet-stream' )]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Inventory', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'api_key')]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Order' )]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid input' )]
    [OpenApiResponseAttribute(StatusCode = '422' , Description = 'Validation Exception' )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiRequestBodyAttribute(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Order', ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponseAttribute(StatusCode = '404' , Description = 'Order not found' )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Path, Required = $true )]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation')]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponseAttribute(StatusCode = '404' , Description = 'Order not found' )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Path, Required = $true )]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiRequestBodyAttribute(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]

    param(
        [OpenApiRequestBodyAttribute(ContentType = 'application/json', Inline = $false)]
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
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/user/login', Tags = 'user')]
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation' , Schema = [string], ContentTypes = ('application/json', 'application/xml'))]
    ##   [OpenApiHeaderAttribute( StatusCode = '200' , Key = 'X-Expires-After', Description = 'date in UTC when token expires')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Query, Required = $false )]
        [string]$username,

        [OpenApiParameterAttribute( In = [OaParameterLocation]::Query, Required = $false )]
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
function logout {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/user/logout', Tags = 'user')]
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation' )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
    )
    Write-Host "logout called"
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid username supplied')]
    [OpenApiResponseAttribute(StatusCode = '404' , Description = 'User not found')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Path, Required = $true )]
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'Successful operation')]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid username supplied')]
    [OpenApiResponseAttribute(StatusCode = '404' , Description = 'User not found')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Path, Required = $true )]
        [string]$username,
        [OpenApiRequestBodyAttribute(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [User]$User
    )
    Write-Host "updateUser called with username='$username'"
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
    [OpenApiResponseAttribute(StatusCode = '200' , Description = 'User deleted')]
    [OpenApiResponseAttribute(StatusCode = '400' , Description = 'Invalid username supplied')]
    [OpenApiResponseAttribute(StatusCode = '404' , Description = 'User not found')]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    param(
        [OpenApiParameterAttribute( In = [OaParameterLocation]::Path, Required = $true )]
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
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Start-KrServer -Server $srv -CloseLogsOnExit
#endregion
#endregion
