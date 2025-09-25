<#
  FileName: 10.1-Antiforgery.ps1 (with antiforgery)
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault
# 2. Server
New-KrServer -Name "Simple Server"
# 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# Required to run PowerShell routes/middleware
Add-KrPowerShellRuntime

# --- Antiforgery: modern SPA preset ---
# Cookie: .Kestrun.AntiXSRF (HttpOnly, Secure, SameSite=Lax)
# Header: X-CSRF-TOKEN
Add-KrAntiforgeryMiddleware -CookieName ".Kestrun.AntiXSRF" -HeaderName "X-CSRF-TOKEN"

Enable-KrConfiguration

# Add token endpoint
Add-KrAntiforgeryTokenRoute -Path "/csrf-token" | Out-Null

# ----- Sample routes (GETs don’t need tokens; they’re safe by convention) -----
Add-KrMapRoute -Verbs Get -Path "/hello" -ScriptBlock {
    Write-KrTextResponse -InputObject "Hello, World!" -StatusCode 200
}
Add-KrMapRoute -Verbs Get -Path "/hello-json" -ScriptBlock {
    Write-KrJsonResponse -InputObject @{ message = "Hello, World!" } -StatusCode 200
}
Add-KrMapRoute -Verbs Get -Path "/hello-xml" -ScriptBlock {
    Write-KrXmlResponse -InputObject @{ message = "Hello, World!" } -StatusCode 200
}
Add-KrMapRoute -Verbs Get -Path "/hello-yaml" -ScriptBlock {
    Write-KrYamlResponse -InputObject @{ message = "Hello, World!" } -StatusCode 200
}

# ----- Your /json route (explicitly opt-out from antiforgery) -----
$options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$options.Pattern = "/json"
$options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get')
$options.Code = {
    $message = Get-KrRequestHeader -Name 'message'
    Write-KrJsonResponse -InputObject @{ message = $message } -StatusCode 200
}
$options.Language = 'PowerShell'
$options.DisableAntiforgery = $true  # this remains open (no CSRF validation)
Add-KrMapRoute -Options $options

# ----- Example of a PROTECTED POST (requires X-CSRF-TOKEN header) -----
Add-KrMapRoute -Verbs Post -Path "/profile" -ScriptBlock {
    # If the X-CSRF-TOKEN header is missing/invalid, the antiforgery middleware will reject the request.
    $body = Get-KrRequestBody
    Write-KrJsonResponse -InputObject @{ saved = $true; name = $body.name } -StatusCode 200
}
# Note: No -DisableAntiforgery here → this POST is protected.

Start-KrServer
