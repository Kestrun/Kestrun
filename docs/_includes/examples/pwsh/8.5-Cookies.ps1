<#
    Sample: Cookies Authentication
    Purpose: Demonstrates form login + cookie session for subsequent authorized requests.
    File:    8.5-Cookies.ps1
    Notes:   Uses HTTP (not HTTPS) for simplicity; secure cookie flags recommended with TLS.
#>

# 1. Logging
New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth Cookies'

# 3. Listener
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback) -SelfSignedCert

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Define cookie builder

# 6. Register cookie auth scheme
New-KrCookieBuilder -Name 'KestrunAuth' -HttpOnly -SecurePolicy Always -SameSite Strict |
    Add-KrCookiesAuthentication -Name 'Cookies' -LoginPath '/cookies/login' -LogoutPath '/cookies/logout' -AccessDeniedPath '/cookies/denied' `
        -SlidingExpiration -ExpireTimeSpan (New-TimeSpan -Minutes 30)

# 7. Finalize configuration
Enable-KrConfiguration

# 8. Login form route
Add-KrMapRoute -Verbs Get -Pattern '/cookies/login' -ScriptBlock {
    Write-KrTextResponse -InputObject @'
       <!DOCTYPE html>
<html>
  <head>
    <meta charset='utf-8' />
    <title>Login</title>
  </head>
  <body>
    <h1>Login</h1>
    <form method='post' action='/cookies/login'>
      <label>
        Username:
        <input type='text' name='username' required />
      </label><br/>
      <label>
        Password:
        <input type='password' name='password' required />
      </label><br/>
      <button type='submit'>Log In</button>
    </form>
  </body>
</html>
'@ -ContentType 'text/html'
}

# 9. Login route (issues cookie)
Add-KrMapRoute -Verbs Post -Pattern '/cookies/login' -ScriptBlock {
    $form = $Context.Request.Form
    if ($form['username'] -eq 'admin' -and $form['password'] -eq 'secret') {
        $principal = Invoke-KrCookieSignIn -Scheme 'Cookies' -Name $form['username'] -AllowRefresh -PassThru
        Write-KrLog -Level Information -Message 'User {user} signed {principal} in with Cookies authentication.' -Properties $form['username'], $principal
        Write-KrJsonResponse @{ success = $true }
    } else {
        Write-KrJsonResponse @{ success = $false } -StatusCode 401
    }
}

# 10. Protected route group requiring cookie auth
Add-KrRouteGroup -Prefix '/cookies' -AuthorizationSchema 'Cookies' {
    Add-KrMapRoute -Verbs Get -Pattern '/logout' -ScriptBlock {
        Invoke-KrCookieSignOut -Scheme 'Cookies' -Redirect
    }
    # 9. Protected route requiring cookie
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies Authentication." -ContentType 'text/plain'
    }
}

# 11. Start server
Start-KrServer
