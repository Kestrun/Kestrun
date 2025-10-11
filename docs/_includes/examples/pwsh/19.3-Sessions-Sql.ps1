<#
    Sample Kestrun Server Configuration – Sessions Demo
    This script shows how to enable Session middleware and use it in routes.
    FileName: 19.1-Sessions.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Configure default logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name 'Sessions Demo Server'


# Listener
Add-KrEndpoint -Port ($Port) -IPAddress $IPAddress -SelfSignedCert

# --- Session setup -----------------------------------------------------------
# Cookie is optional, but nice to be explicit.
$cookie = New-KrCookieBuilder `
    -Name 'Kr.Session' `
    -HttpOnly `
    -SameSite Lax `
    -SecurePolicy Always  # set to None if you want to test over plain http

# SQL Server configuration (if using SQL Server)
Add-KrDistributedSqlServerCache -ConnectionString 'Server=localhost;Database=KestrunSessions;User Id=sa;Password=Your_password123;' `
    -SchemaName 'dbo' -TableName 'Sessions'

# Add session services + middleware. By default, Add-KrSession will ensure a distributed memory cache exists.
Add-KrSession -Cookie $cookie -IdleTimeout 20 -IOTimeout 10


# Enable configuration
Enable-KrConfiguration

# Tip:
# If you're wiring Redis/SQL yourself, use: Add-KrSession -NoDistributedMemoryCache
# and add your cache provider first.

# --- Routes -----------------------------------------------------------------

# 1) Simple counter: increments an integer stored in session.
Add-KrMapRoute -Verbs Get -Path '/session/counter' -ScriptBlock {
    # Get current value (or 0 if not set)
    $current = Get-KrSessionInt32 -Key 'counter'
    # Increment and store back
    Set-KrSessionInt32 -Key 'counter' -Value ($current + 1)

    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        message = 'Counter incremented'
        counter = $current + 1
    }
}

# 2) Login: set a 'user' value in session (?user=max)
Add-KrMapRoute -Verbs Get -Path '/session/login' -ScriptBlock {
    # Get user from query
    $user = Get-KrRequestQuery -Name 'user'
    # Validate
    if ([string]::IsNullOrWhiteSpace($user)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{
            error = "Missing 'user' query parameter"
            example = '/session/login?user=max'
        }
        return
    }
    # Store in session
    Set-KrSessionString -Key 'user' -Value $user
    # respond to login event
    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        message = 'Logged in'
        user = $user
    }
}

# 3) WhoAmI: read 'user' from session
Add-KrMapRoute -Verbs Get -Path '/session/whoami' -ScriptBlock {
    # Get user from session
    $user = Get-KrSessionString -Key 'user'
    # If not set, return 401
    if ([string]::IsNullOrEmpty($user)) {
        Write-KrJsonResponse -StatusCode 401 -InputObject @{
            error = 'No active session or user not set'
        }
        return
    }
    # respond with user info
    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        user = $user
    }
}

# 4) Logout: clear the session
Add-KrMapRoute -Verbs Get -Path '/session/logout' -ScriptBlock {
    # Clear the session
    Clear-KrSession
    # respond to logout event
    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        message = 'Session cleared'
    }
}

# 5) Generic set: /session/set?key=foo&value=bar
Add-KrMapRoute -Verbs Get -Path '/session/set' -ScriptBlock {
    # Get key and value from query
    $key = Get-KrRequestQuery -Name 'key'
    $value = Get-KrRequestQuery -Name 'value'

    # Validate
    if ([string]::IsNullOrWhiteSpace($key) -or [string]::IsNullOrWhiteSpace($value)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{
            error = "Missing 'key' and/or 'value' query parameter(s)"
            example = '/session/set?key=color&value=purple'
        }
        return
    }
    # Store in session
    Set-KrSessionString -Key $key -Value $value
    # respond to set event
    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        message = 'Stored in session'
        key = $key
        value = $value
    }
}

# 6) Generic get: /session/get?key=foo
Add-KrMapRoute -Verbs Get -Path '/session/get' -ScriptBlock {
    # Get key from query
    $key = Get-KrRequestQuery -Name 'key'
    # Validate
    if ([string]::IsNullOrWhiteSpace($key)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{
            error = "Missing 'key' query parameter"
            example = '/session/get?key=color'
        }
        return
    }
    # Get from session
    $val = Get-KrSessionString -Key $key
    # If not found, return 404
    if ($null -eq $val) {
        Write-KrJsonResponse -StatusCode 404 -InputObject @{
            error = "Key '$key' not found in session"
        }
        return
    }
    # respond with value
    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        key = $key
        value = $val
    }
}

# Convenience: hello (non-session)
Add-KrMapRoute -Verbs Get -Path '/hello' -ScriptBlock {
    Write-KrTextResponse -StatusCode 200 -InputObject 'Hello, Session World!'
}

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
