<##
    Sample: Cookies Authentication
    Purpose: Demonstrates form login + cookie session for subsequent authorized requests.
    File:    8.5-Cookies.ps1
    Notes:   Uses HTTP (not HTTPS) for simplicity; secure cookie flags recommended with TLS.
##>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth Cookies'

# 3. Listener
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Define cookie builder
$cookie = [Microsoft.AspNetCore.Http.CookieBuilder]::new()
$cookie.Name = 'KestrunAuth'
$cookie.HttpOnly = $true
$cookie.SecurePolicy = [Microsoft.AspNetCore.Http.CookieSecurePolicy]::None

# 6. Register cookie auth scheme
Add-KrCookiesAuthentication -Name 'Cookies' -LoginPath '/cookies/login' -LogoutPath '/cookies/logout' -AccessDeniedPath '/cookies/denied' -Cookie $cookie -SlidingExpiration -ExpireTimeSpan (New-TimeSpan -Minutes 30)

# 7. Finalize configuration
Enable-KrConfiguration

# 8. Login route (issues cookie)
Add-KrMapRoute -Verbs Post -Pattern '/cookies/login' -ScriptBlock {
    $form = $Context.Request.Form
    if($form['username'] -eq 'admin' -and $form['password'] -eq 'secret') {
        Write-KrJsonResponse @{ success = $true }
    } else {
        Write-KrJsonResponse @{ success = $false } -StatusCode 401
    }
}

# 9. Protected route requiring cookie
Add-KrMapRoute -Verbs Get -Pattern '/secure/cookies/hello' -AuthorizationSchema 'Cookies' -ScriptBlock { Write-KrTextResponse 'Cookie Hello admin' }

# 10. Start server
Start-KrServer

