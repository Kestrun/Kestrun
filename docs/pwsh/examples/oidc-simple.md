---
layout: default
parent: PowerShell Cmdlets
title: OIDC Simple Example
nav_order: 70
---

# OpenID Connect - Simple Example

The `Add-KrOpenIdConnectAuthentication` cmdlet simplifies OIDC setup by automatically creating three authentication schemes:

- **`<Name>`** → Remote OIDC handler (performs authorization code flow)
- **`<Name>.Cookies`** → Local cookie persistence for authenticated sessions
- **`<Name>.Policy`** → Policy/forwarding scheme (authenticates via cookies, challenges via OIDC)

## Basic Usage

```powershell
# Minimal setup with defaults
New-KrServer |
    Add-KrEndpoint -Port 5000 -SelfSignedCert |
    Add-KrOpenIdConnectAuthentication `
        -Name 'Oidc' `
        -Authority 'https://your-provider.com' `
        -ClientId 'your-client-id' `
        -ClientSecret 'your-client-secret' |
    Enable-KrConfiguration

# Protected route using the policy scheme
Add-KrRouteGroup -Prefix '/secure' -AuthorizationSchema 'Oidc.Policy' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $name = $Context.User.Identity.Name ?? '(anonymous)'
        Write-KrTextResponse "Hello, $name!"
    }
}

Start-KrServer
```

## Advanced Configuration

```powershell
# Customize scopes, PKCE, and callbacks
Add-KrOpenIdConnectAuthentication `
    -Name 'AzureAD' `
    -Authority 'https://login.microsoftonline.com/{tenant}/v2.0' `
    -ClientId $env:AAD_CLIENT_ID `
    -ClientSecret $env:AAD_CLIENT_SECRET `
    -Scope @('email', 'profile', 'offline_access') `
    -CallbackPath '/signin-oidc' `
    -UsePkce $true `
    -SaveTokens $true `
    -GetUserInfo $true `
    -VerboseEvents
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Name` | `string` | `'Oidc'` | Base scheme name |
| `Authority` | `string` | *(required)* | OpenID Provider URL |
| `ClientId` | `string` | *(required)* | Client application ID |
| `ClientSecret` | `string` | `$null` | Client secret (leave empty for public clients) |
| `Scope` | `string[]` | `@('openid', 'profile')` | Additional scopes beyond defaults |
| `CallbackPath` | `string` | `'/signin-oidc'` | Redirect URI path |
| `ResponseMode` | `string` | `'form_post'` | Response mode (`form_post`, `query`, `fragment`) |
| `UsePkce` | `bool` | `$true` | Enable PKCE for authorization code flow |
| `SaveTokens` | `bool` | `$true` | Persist tokens in auth cookie |
| `GetUserInfo` | `bool` | `$true` | Call UserInfo endpoint to enrich claims |
| `VerboseEvents` | `switch` | `$false` | Enable verbose event logging |

## Public vs Confidential Clients

### Public Client (no secret)

```powershell
# PKCE is required for public clients
Add-KrOpenIdConnectAuthentication `
    -Authority 'https://demo.duendesoftware.com' `
    -ClientId 'interactive.public' `
    -UsePkce $true
```

### Confidential Client (with secret)

```powershell
# PKCE optional for confidential clients (provider-dependent)
Add-KrOpenIdConnectAuthentication `
    -Authority 'https://demo.duendesoftware.com' `
    -ClientId 'interactive.confidential' `
    -ClientSecret 'secret' `
    -UsePkce $false
```

## C# Direct Usage

For advanced scenarios, use the framework's `OpenIdConnectOptions` directly:

```csharp
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Kestrun.Hosting;

var options = new OpenIdConnectOptions
{
    Authority = "https://login.microsoftonline.com/common/v2.0",
    ClientId = "your-client-id",
    ClientSecret = "your-secret",
    CallbackPath = "/signin-oidc",
    ResponseType = OpenIdConnectResponseType.Code,
    UsePkce = true,
    SaveTokens = true,
    GetClaimsFromUserInfoEndpoint = true,
    RequireHttpsMetadata = true
};

options.Scope.Add("email");
options.Scope.Add("offline_access");

// Advanced: customize token validation
options.TokenValidationParameters.ValidateIssuer = false;

server.AddOpenIdConnectAuthentication("Oidc", options);
```

## See Also

- [OIDC Full Sample (Duende Demo)](../../_includes/examples/pwsh/8.12-OIDC.ps1)
- [OAuth2 Authentication](oauth2-authentication.md)
- [GitHub OAuth](github-oauth.md)
