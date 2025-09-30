<#
    Sample: Basic Authentication (C# and VB.NET)
    Purpose: Shows how to implement Basic authentication credential validation using inline C# and VB.NET code.
    File:    8.2-Basic-MultiLang.ps1
    Notes:   Demonstrates multi-language support for auth handlers.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging pipeline
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server
New-KrServer -Name 'Auth Basic Multi'

# 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# 4. PowerShell runtime
Add-KrPowerShellRuntime

# 5. C# validation logic
Add-KrBasicAuthentication -Name 'CSharpBasic' -Realm 'CS' -AllowInsecureHttp -Code @'
    return username == "admin" && password == "password";
'@ -CodeLanguage CSharp

# 6. VB.NET validation logic
Add-KrBasicAuthentication -Name 'VBNetBasic' -Realm 'VB' -AllowInsecureHttp -Code @'
    Return username = "admin" AndAlso password = "password"
'@ -CodeLanguage VBNet

# 7. Finalize configuration
Enable-KrConfiguration

# 8. Map language-specific routes
Add-KrMapRoute -Verbs Get -Pattern '/secure/cs/hello' -AuthorizationSchema 'CSharpBasic' -ScriptBlock {
    Write-KrTextResponse -InputObject "CS Hello, $( $Context.User.Identity.Name )!" -ContentType 'text/plain'
}

Add-KrMapRoute -Verbs Get -Pattern '/secure/vb/hello' -AuthorizationSchema 'VBNetBasic' -ScriptBlock {
    Write-KrTextResponse -InputObject "VB Hello, $( $Context.User.Identity.Name )!" -ContentType 'text/plain'
}

# 9. Start server
Start-KrServer -CloseLogsOnExit

