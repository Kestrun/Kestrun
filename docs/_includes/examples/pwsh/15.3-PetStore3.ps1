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

# Pet
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
[OpenApiRequestBodyComponent()]
class test {
    [Nullable[long]]$id
    [OpenApiPropertyAttribute(Description = 'Quantity of items', Minimum = 1, Maximum = 200)]
    [int]$quantity
    [OpenApiPropertyAttribute(Description = 'Maximum items allowed', Minimum = 1, Maximum = 200)]
    [int]$maximum = 200
    [Pet]$pet
    [User]$user
}
# RequestBody: upload_OctetStream
[OpenApiRequestBodyComponent(required = $true, ContentType = 'application/octet-stream')]
#[OpenApiPropertyAttribute(Format = 'binary')]
class Upload_OctetStream {}

# RequestBody: UserArray
[OpenApiRequestBodyComponent(Description = 'List of user object', Required = $true)]
[OpenApiRequestBodyComponent(ContentType = 'application/json')]
[OpenApiPropertyAttribute(Array = $true)]
class UserArrayBody:User {}

# RequestBody: User
[OpenApiRequestBodyComponent(Description = 'Created user object', Required = $true)]
[OpenApiRequestBodyComponent(ContentType = 'application/json')]
[OpenApiRequestBodyComponent(ContentType = 'application/xml')]
[OpenApiRequestBodyComponent(ContentType = 'application/x-www-form-urlencoded')]
class UserBody:User {}

# RequestBody: Pet
[OpenApiRequestBodyComponent(Description = 'Pet object that needs to be added to the store', Required = $true)]
[OpenApiRequestBodyComponent(ContentType = 'application/json')]
[OpenApiRequestBodyComponent(ContentType = 'application/xml')]
[OpenApiRequestBodyComponent(ContentType = 'application/x-www-form-urlencoded')]
class PetBody:Pet {}

# RequestBody: Order
[OpenApiRequestBodyComponent(required = $true)]
[OpenApiRequestBodyComponent(ContentType = 'application/json')]
[OpenApiRequestBodyComponent(ContentType = 'application/xml')]
[OpenApiRequestBodyComponent(ContentType = 'application/x-www-form-urlencoded')]
class OrderBody:Order {
}


#endregion

#region COMPONENT PARAMETERS
# =========================
# COMPONENT: PARAMETERS
# =========================

# /pet/findByStatus?status=available|pending|sold
[OpenApiParameterComponent(JoinClassName = '-')]
class FindByStatusParams {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Status values that need to be considered for filter', Explode = $true)]
    [ValidateSet('available', 'pending', 'sold')]
    [string]$status = 'available'
}

# /pet/findByTags?tags=tag1&tags=tag2 ...
[OpenApiParameterComponent(JoinClassName = '-')]
class FindByTagsParams {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Tags to filter by', Explode = $true)]
    [string[]]$tags
}

# /pet/{petId}
[OpenApiParameterComponent()]
class Param_PetId {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Description = 'ID of pet to return')]
    [OpenApiPropertyAttribute(Format = 'int64')]
    [long]$petId
}

# POST /pet/{petId} (form query params)
[OpenApiParameterComponent(JoinClassName = '-')]
class UpdatePetWithFormParams {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Name of pet that needs to be updated')]
    [string]$name

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Status of pet that needs to be updated')]
    [string]$status
}

# DELETE /pet/{petId} (optional api_key header)
[OpenApiParameterComponent()]
class DeletePetHeader {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Header, Description = '')]
    [string]$api_key
}

# /pet/{petId}/uploadImage (plus optional additionalMetadata)
[OpenApiParameterComponent(JoinClassName = '-')]
class UploadImageParams {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Description = 'ID of pet to update', Name = 'petId')]
    [long]$petId

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'Additional Metadata', Name = 'additionalMetadata')]
    [string]$additionalMetadata
}

# /store/order/{orderId}
[OpenApiParameterComponent()]
class Param_OrderId {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Description = 'ID of order that needs to be fetched')]
    [OpenApiPropertyAttribute(Format = 'int64')]
    [long]$orderId
}

# /user/login?username&password
[OpenApiParameterComponent(JoinClassName = '-')]
class LoginParams {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'The user name for login')]
    [string]$username

    [OpenApiParameterAttribute(In = [OaParameterLocation]::Query, Description = 'The password for login in clear text')]
    [string]$password
}

# /user/{username}
[OpenApiParameterComponent(JoinClassName = '-')]
class Param_Username {
    [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Description = 'The name that needs to be fetched. Use user1 for testing')]
    [string]$username
}
#endregion
#region COMPONENT RESPONSES
# =========================
# COMPONENT: RESPONSES
# =========================

# ---------- /pet  (PUT updatePet / POST addPet)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_Pet_Write {
    # 200
    [OpenApiResponseAttribute(Description = 'Successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [Pet]$OK

    # 400
    [OpenApiResponseAttribute(Description = 'Invalid ID supplied')]
    [object]$BadRequest

    # 404
    [OpenApiResponseAttribute(Description = 'Pet not found')]
    [object]$NotFound

    # 422
    [OpenApiResponseAttribute(Description = 'Validation exception')]
    [object]$UnprocessableEntity

    # default
    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /pet/findByStatus  (GET)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_FindByStatus {
    # 200 -> array of Pet (json+xml)
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [OpenApiPropertyAttribute()]
    [Pet[]]$OK

    # 400
    [OpenApiResponseAttribute(Description = 'Invalid status value')]
    [object]$BadRequest

    # default
    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /pet/findByTags  (GET)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_FindByTags {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [OpenApiPropertyAttribute()]
    [Pet[]]$OK

    [OpenApiResponseAttribute(Description = 'Invalid tag value')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /pet/{petId}  (GET getPetById, POST updatePetWithForm, DELETE deletePet)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_PetById_Get {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [Pet]$OK

    [OpenApiResponseAttribute(Description = 'Invalid ID supplied')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Pet not found')]
    [object]$NotFound

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

[OpenApiResponseComponent( )]
class ResponseDefault {

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_PetById_PostForm {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [Pet]$OK

    [OpenApiResponseAttribute(Description = 'Invalid input')]
    [object]$BadRequest
}

[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_PetById_Delete {
    [OpenApiResponseAttribute(Description = 'Pet deleted')]
    [object]$OK

    [OpenApiResponseAttribute(Description = 'Invalid pet value')]
    [object]$BadRequest
}

# ---------- /pet/{petId}/uploadImage  (POST)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_UploadImage {
    # TODO: Fix Inline attribute
    [OpenApiResponseAttribute(Description = 'successful operation' )]
    [OpenApiContentTypeAttribute(ContentType = 'application/json', Inline = $true)]
    [ApiResponse]$OK

    [OpenApiResponseAttribute(Description = 'No file uploaded')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Pet not found')]
    [object]$NotFound

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /store/inventory  (GET)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_Inventory {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [hashtable]$OK  # additionalProperties int32; you can keep as hashtable or a dedicated class if you prefer

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /store/order  (POST placeOrder)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_Order_Create {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Order]$OK

    [OpenApiResponseAttribute(Description = 'Invalid input')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Validation exception')]
    [object]$UnprocessableEntity

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /store/order/{orderId}  (GET, DELETE)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_OrderById_Get {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [Order]$OK

    [OpenApiResponseAttribute(Description = 'Invalid ID supplied')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Order not found')]
    [object]$NotFound

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_OrderById_Delete {
    [OpenApiResponseAttribute(Description = 'order deleted')]
    [object]$OK

    [OpenApiResponseAttribute(Description = 'Invalid ID supplied')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Order not found')]
    [object]$NotFound

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /user  (POST createUser)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_User_Create {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [User]$OK

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /user/createWithList  (POST)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_User_CreateWithList {
    [OpenApiResponseAttribute(Description = 'Successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [User]$OK

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /user/login  (GET) with headers
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_User_Login {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiHeaderAttribute(Key = 'X-Rate-Limit', Description = 'calls per hour allowed by the user')]
    [OpenApiHeaderAttribute(Key = 'X-Expires-After', Description = 'date in UTC when token expires')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [string]$OK

    [OpenApiResponseAttribute(Description = 'Invalid username/password supplied')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /user/logout  (GET)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_User_Logout {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [object]$OK

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

# ---------- /user/{username}  (GET, PUT, DELETE)
[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_UserByName_Get {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [OpenApiContentTypeAttribute(ContentType = 'application/xml')]
    [User]$OK

    [OpenApiResponseAttribute(Description = 'Invalid username supplied')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'User not found')]
    [object]$NotFound

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_UserByName_Put {
    [OpenApiResponseAttribute(Description = 'successful operation')]
    [object]$OK

    [OpenApiResponseAttribute(Description = 'bad request')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'user not found')]
    [object]$NotFound

    [OpenApiResponseAttribute(Description = 'Unexpected error')]
    [OpenApiContentTypeAttribute(ContentType = 'application/json')]
    [Error]$Default
}

[OpenApiResponseComponent(JoinClassName = '-')]
class Resp_UserByName_Delete {
    [OpenApiResponseAttribute(Description = 'User deleted')]
    [object]$OK

    [OpenApiResponseAttribute(Description = 'Invalid username supplied')]
    [object]$BadRequest

    [OpenApiResponseAttribute(Description = 'User not found')]
    [object]$NotFound

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
.PARAMETER petId
    Update an existing pet by Id.
.PARAMETER pet
    Update an existent pet in the store
#>
function updatePet {
    [OpenApiPath(HttpVerb = 'Put' , Pattern = '/pet/{petId}', Tags = 'pet')]
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_Pet_Write-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_Pet_Write-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '404' , ReferenceId = 'Resp_Pet_Write-NotFound', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '422' , ReferenceId = 'Resp_Pet_Write-UnprocessableEntity', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path  )]
        [long]$petId,
        [OpenApiRequestBodyAttribute(Required = $true, Inline = $false,
            ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [Pet]$pet
    )
    # Stub handler; the doc is our star tonight.
    Write-Host "updatePet called for petId='$petId'"
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
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_Pet_Write-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_Pet_Write-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '422' , ReferenceId = 'Resp_Pet_Write-UnprocessableEntity', Inline = $true )]
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
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_FindByStatus-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_FindByStatus-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'FindByStatusParams-status' )]
        $status
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
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_FindByTags-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_FindByTags-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'FindByTagsParams-tags' , Inline = $true)]
        $tags
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
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_PetById_Get-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_PetById_Get-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '404' , ReferenceId = 'Resp_PetById_Get-NotFound', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path  )]
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
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_PetById_PostForm-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_PetById_PostForm-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterRefAttribute(ReferenceId = 'petId')]
        $petId,
        [OpenApiParameterRefAttribute(ReferenceId = 'UpdatePetWithFormParams-name' )]
        $name,
        [OpenApiParameterRefAttribute(ReferenceId = 'UpdatePetWithFormParams-status' )]
        $status
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
    [OpenApiPath(HttpVerb = 'Delete' , Pattern = '/pet/{petId}', Tags = 'pet',
        Summary = 'Delete a pet.',
        Description = 'Delete a pet.',
        OperationId = 'deletePet')]
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_PetById_Delete-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = '400' , ReferenceId = 'Resp_PetById_Delete-BadRequest', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Header )]
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
    [OpenApiResponseRefAttribute( StatusCode = '200' , ReferenceId = 'Resp_UploadImage-OK', Inline = $true )]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    # [OpenApiAuthorizationAttribute( Scheme = 'petstore_auth' , Policies = 'write:pets, read:pets' )]
    param(
        [OpenApiParameterAttribute(In = [OaParameterLocation]::Path, Required = $true )]
        [long]$petId,

        [OpenApiParameterAttribute(In = [OaParameterLocation]::Query,  Required = $false )]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Inventory', Inline = $true )]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Order' )]
    [OpenApiResponseAttribute( StatusCode = '400' , Description = 'Invalid input' )]
    [OpenApiResponseAttribute( StatusCode = '422' , Description = 'Validation Exception' )]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'Order', ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseAttribute( StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponseAttribute( StatusCode = '404' , Description = 'Order not found' )]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation')]
    [OpenApiResponseAttribute( StatusCode = '400' , Description = 'Invalid ID supplied' )]
    [OpenApiResponseAttribute( StatusCode = '404' , Description = 'Order not found' )]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentTypes = ('application/json', 'application/xml'))]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation', SchemaRef = 'User', ContentTypes = ('application/json', 'application/xml'))]
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
    [OpenApiResponseAttribute( StatusCode = '200' , Description = 'Successful operation' , Schema = [string], ContentTypes = ('application/json', 'application/xml'))]
    [OpenApiResponseRefAttribute( StatusCode = 'default' , ReferenceId = 'Default', Inline = $true )]
    [OpenApiHeaderAttribute(Key = 'X-Rate-Limit', Description = 'calls per hour allowed by the user')]
    [OpenApiHeaderAttribute(Key = 'X-Expires-After', Description = 'date in UTC when token expires')]
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


# --------------------------------------
# /user/login (GET)
# --------------------------------------
New-KrMapRouteBuilder -Verbs 'GET' -Pattern '/user/login' |
    Add-KrMapRouteScriptBlock -ScriptBlock { Write-KrJsonResponse '' } |
    Add-KrMapRouteOpenApiTag -Tags 'user' |
    Add-KrMapRouteOpenApiInfo -Summary 'Logs user into the system.' -Description 'Log into the system.' -OperationId 'loginUser' |
    Add-KrMapRouteOpenApiParameter -ReferenceId 'LoginParams-username' -Inline |
    Add-KrMapRouteOpenApiParameter -ReferenceId 'LoginParams-password' -Inline |
    Add-KrMapRouteOpenApiResponse -StatusCode '200' -ReferenceId 'Resp_User_Login-OK' -Inline |
    Add-KrMapRouteOpenApiResponse -StatusCode '400' -ReferenceId 'Resp_User_Login-BadRequest' -Inline |
    Add-KrMapRouteOpenApiResponse -StatusCode 'default' -ReferenceId 'Resp_User_Login-Default' |
    Build-KrMapRoute

# --------------------------------------
# /user/logout (GET)
# --------------------------------------
New-KrMapRouteBuilder -Verbs 'GET' -Pattern '/user/logout' |
    Add-KrMapRouteScriptBlock -ScriptBlock { Write-KrStatusResponse 200 } |
    Add-KrMapRouteOpenApiTag -Tags 'user' |
    Add-KrMapRouteOpenApiInfo -Summary 'Logs out current logged in user session.' -Description 'Log user out of the system.' -OperationId 'logoutUser' |
    Add-KrMapRouteOpenApiResponse -StatusCode '200' -ReferenceId 'Resp_User_Logout-OK' -Inline |
    Add-KrMapRouteOpenApiResponse -StatusCode 'default' -ReferenceId 'Resp_User_Logout-Default' |

    Build-KrMapRoute

# --------------------------------------
# /user/{username} (GET, PUT, DELETE)
# --------------------------------------
New-KrMapRouteBuilder -Verbs @('GET', 'PUT', 'DELETE') -Pattern '/user/{username}' |
    Add-KrMapRouteScriptBlock -ScriptBlock { Write-KrJsonResponse @{ } } |
    Add-KrMapRouteOpenApiTag -Tags 'user' |
    # parameter component
    Add-KrMapRouteOpenApiParameter -ReferenceId 'Param_Username-username' -Inline -Key 'username' |

    # GET /user/{username}
    Add-KrMapRouteOpenApiInfo -Verbs 'GET' -Summary 'Get user by user name.' -Description 'Get user detail based on username.' -OperationId 'getUserByName' |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode '200' -ReferenceId 'Resp_UserByName_Get-OK' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode '400' -ReferenceId 'Resp_UserByName_Get-BadRequest' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode '404' -ReferenceId 'Resp_UserByName_Get-NotFound' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'GET' -StatusCode 'default' -ReferenceId 'Resp_UserByName_Get-Default' |

    # PUT /user/{username}
    Add-KrMapRouteOpenApiInfo -Verbs 'PUT' -Summary 'Update user resource.' -Description 'This can only be done by the logged in user.' -OperationId 'updateUser' |

    Add-KrMapRouteOpenApiResponse -Verbs 'PUT' -StatusCode '200' -ReferenceId 'Resp_UserByName_Put-OK' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'PUT' -StatusCode '400' -ReferenceId 'Resp_UserByName_Put-BadRequest' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'PUT' -StatusCode '404' -ReferenceId 'Resp_UserByName_Put-NotFound' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'PUT' -StatusCode 'default' -ReferenceId 'Resp_UserByName_Put-Default' |

    # DELETE /user/{username}
    Add-KrMapRouteOpenApiInfo -Verbs 'DELETE' -Summary 'Delete user resource.' -Description 'This can only be done by the logged in user.' -OperationId 'deleteUser' |
    Add-KrMapRouteOpenApiResponse -Verbs 'DELETE' -StatusCode '200' -ReferenceId 'Resp_UserByName_Delete-OK' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'DELETE' -StatusCode '400' -ReferenceId 'Resp_UserByName_Delete-BadRequest' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'DELETE' -StatusCode '404' -ReferenceId 'Resp_UserByName_Delete-NotFound' -Inline |
    Add-KrMapRouteOpenApiResponse -Verbs 'DELETE' -StatusCode 'default' -ReferenceId 'Resp_UserByName_Delete-Default' |


    Build-KrMapRoute

#>

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
