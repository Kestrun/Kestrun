
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
<#
.SYNOPSIS
    Kestrun PowerShell Example: Request Localization
.DESCRIPTION
    This script demonstrates how to use request localization in Kestrun to serve
    content in multiple languages based on user preferences.
#>

try {
    # Get the path of the current script
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"
    
    # Import the Kestrun module
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error 'Ensure the Kestrun module is installed or the path is correct.'
    exit 1
}

# Configure logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\RequestLocalization.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

# Create server
$server = New-KrServer -Name 'Kestrun RequestLocalization Demo'

# Configure server options
Set-KrServerOptions -AllowSynchronousIO -DenyServerHeader

# Add endpoints
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Configure request localization
# This enables the application to serve content in multiple languages
Add-KrRequestLocalizationMiddleware `
    -DefaultCulture 'en-US' `
    -SupportedCultures @('en-US', 'fr-FR', 'es-ES', 'de-DE', 'ja-JP') `
    -FallBackToParentCultures

# Enable configuration
Enable-KrConfiguration

# Helper function to load localized strings from .psd1 files
function Get-LocalizedStrings {
    param([string]$Culture)
    
    $localePath = Join-Path $ScriptPath "locales\$Culture.psd1"
    
    # Check if culture-specific file exists
    if (Test-Path $localePath) {
        return Import-PowerShellDataFile -Path $localePath
    }
    
    # Try parent culture (e.g., 'en' from 'en-GB')
    if ($Culture.Contains('-')) {
        $parentCulture = $Culture.Split('-')[0]
        $parentPath = Join-Path $ScriptPath "locales\$parentCulture.psd1"
        if (Test-Path $parentPath) {
            return Import-PowerShellDataFile -Path $parentPath
        }
    }
    
    # Fall back to default (en-US)
    $defaultPath = Join-Path $ScriptPath "locales\en-US.psd1"
    if (Test-Path $defaultPath) {
        return Import-PowerShellDataFile -Path $defaultPath
    }
    
    # If all else fails, return empty hashtable
    return @{}
}

# Main route - displays localized content
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    # Get the current culture from request context (set by middleware)
    # Preferred method: Use CurrentUICulture
    $culture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
    
    # Alternative: Read from HttpContext.Items (fallback)
    # $culture = $Context.Items['KrCulture']
    
    # Load localized strings from .psd1 file
    $messages = Get-LocalizedStrings -Culture $culture
    
    $html = @"
<!DOCTYPE html>
<html lang="$culture">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Kestrun Request Localization Demo</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 800px;
            margin: 50px auto;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }
        .container {
            background: rgba(255, 255, 255, 0.1);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 30px;
            box-shadow: 0 8px 32px 0 rgba(31, 38, 135, 0.37);
        }
        h1 {
            margin-top: 0;
            font-size: 2.5em;
        }
        .culture-info {
            background: rgba(255, 255, 255, 0.2);
            padding: 15px;
            border-radius: 10px;
            margin: 20px 0;
        }
        ul {
            list-style-type: none;
            padding-left: 0;
        }
        li {
            padding: 8px 0;
        }
        code {
            background: rgba(0, 0, 0, 0.3);
            padding: 3px 8px;
            border-radius: 5px;
            font-family: 'Courier New', monospace;
        }
        .languages {
            margin-top: 30px;
        }
        .language-btn {
            display: inline-block;
            margin: 5px;
            padding: 10px 20px;
            background: rgba(255, 255, 255, 0.2);
            border: 2px solid rgba(255, 255, 255, 0.3);
            border-radius: 5px;
            color: white;
            text-decoration: none;
            transition: all 0.3s;
        }
        .language-btn:hover {
            background: rgba(255, 255, 255, 0.3);
            transform: translateY(-2px);
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>$($messages.Welcome)</h1>
        <div class="culture-info">
            <strong>$($messages.CurrentCulture):</strong> $culture
        </div>
        <h2>$($messages.Instructions)</h2>
        <ul>
            <li>1. $($messages.Method1)</li>
            <li>2. $($messages.Method2)</li>
            <li>3. $($messages.Method3)</li>
        </ul>
        <div class="languages">
            <h3>$($messages.QuickLinks)</h3>
            <a href="/?culture=en-US" class="language-btn">English 🇺🇸</a>
            <a href="/?culture=fr-FR" class="language-btn">Français 🇫🇷</a>
            <a href="/?culture=es-ES" class="language-btn">Español 🇪🇸</a>
            <a href="/?culture=de-DE" class="language-btn">Deutsch 🇩🇪</a>
            <a href="/?culture=ja-JP" class="language-btn">日本語 🇯🇵</a>
        </div>
    </div>
</body>
</html>
"@
    
    Write-KrHtmlResponse -Html $html -StatusCode 200
}

# API route - returns current culture information as JSON
Add-KrMapRoute -Verbs Get -Pattern '/api/culture' -ScriptBlock {
    # Get culture from request context (set by middleware)
    $currentCulture = [System.Globalization.CultureInfo]::CurrentCulture.Name
    $currentUICulture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
    
    # Also available as: $Context.Items['KrCulture']
    
    $cultureInfo = @{
        CurrentCulture = $currentCulture
        CurrentUICulture = $currentUICulture
        DisplayName = [System.Globalization.CultureInfo]::CurrentCulture.DisplayName
        NativeName = [System.Globalization.CultureInfo]::CurrentCulture.NativeName
        EnglishName = [System.Globalization.CultureInfo]::CurrentCulture.EnglishName
        DateFormat = [System.Globalization.CultureInfo]::CurrentCulture.DateTimeFormat.ShortDatePattern
        TimeFormat = [System.Globalization.CultureInfo]::CurrentCulture.DateTimeFormat.LongTimePattern
        NumberFormat = @{
            DecimalSeparator = [System.Globalization.CultureInfo]::CurrentCulture.NumberFormat.NumberDecimalSeparator
            GroupSeparator = [System.Globalization.CultureInfo]::CurrentCulture.NumberFormat.NumberGroupSeparator
            CurrencySymbol = [System.Globalization.CultureInfo]::CurrentCulture.NumberFormat.CurrencySymbol
        }
        Timestamp = Get-Date -Format 'o'
        # Show where culture was read from
        HttpContextItemsValue = $Context.Items['KrCulture']
    }
    
    Write-KrJsonResponse -InputObject $cultureInfo -StatusCode 200
}

# Start the server
Write-Host ''
Write-Host '================================================' -ForegroundColor Cyan
Write-Host '  Kestrun Request Localization Demo' -ForegroundColor Yellow
Write-Host '================================================' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Server is starting on http://localhost:5000' -ForegroundColor Green
Write-Host ''
Write-Host 'Try these URLs:' -ForegroundColor Cyan
Write-Host '  http://localhost:5000/?culture=en-US  (English)' -ForegroundColor White
Write-Host '  http://localhost:5000/?culture=fr-FR  (French)' -ForegroundColor White
Write-Host '  http://localhost:5000/?culture=es-ES  (Spanish)' -ForegroundColor White
Write-Host '  http://localhost:5000/?culture=de-DE  (German)' -ForegroundColor White
Write-Host '  http://localhost:5000/?culture=ja-JP  (Japanese)' -ForegroundColor White
Write-Host ''
Write-Host 'API Endpoint:' -ForegroundColor Cyan
Write-Host '  http://localhost:5000/api/culture' -ForegroundColor White
Write-Host ''
Write-Host 'Press Ctrl+C to stop the server' -ForegroundColor Yellow
Write-Host '================================================' -ForegroundColor Cyan
Write-Host ''

Start-KrServer

