param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 15.11-RequestLocalization' -Tag 'Tutorial', 'Middleware', 'RequestLocalization' {
    BeforeAll { 
        # Create locale files
        $localesPath = Join-Path $TestDrive 'locales'
        New-Item -Path $localesPath -ItemType Directory -Force | Out-Null
        
        # Create en-US.psd1
        $enUS = @'
@{
    Message = 'Hello'
    Greeting = 'Welcome'
}
'@
        Set-Content -Path (Join-Path $localesPath 'en-US.psd1') -Value $enUS
        
        # Create fr-FR.psd1
        $frFR = @'
@{
    Message = 'Bonjour'
    Greeting = 'Bienvenue'
}
'@
        Set-Content -Path (Join-Path $localesPath 'fr-FR.psd1') -Value $frFR
        
        # Create es-ES.psd1
        $esES = @'
@{
    Message = 'Hola'
    Greeting = 'Bienvenido'
}
'@
        Set-Content -Path (Join-Path $localesPath 'es-ES.psd1') -Value $esES
        
        # Create a test script using PowerShell-first approach
        $testScript = @"
Import-Module "`$PSScriptRoot/../../../src/PowerShell/Kestrun/Kestrun.psm1" -Force

New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

`$server = New-KrServer -Name 'RequestLocalization Test'
Add-KrEndpoint -Port 0 -IPAddress ([IPAddress]::Loopback)

Add-KrRequestLocalizationMiddleware ``
    -DefaultCulture 'en-US' ``
    -SupportedCultures @('en-US', 'fr-FR', 'es-ES') ``
    -FallBackToParentCultures

Enable-KrConfiguration

# Helper function to load localized strings from .psd1 files
function Get-LocalizedStrings {
    param([string]`$Culture, [string]`$LocalesPath)
    
    `$localePath = Join-Path `$LocalesPath "`$Culture.psd1"
    
    if (Test-Path `$localePath) {
        return Import-PowerShellDataFile -Path `$localePath
    }
    
    # Try parent culture
    if (`$Culture.Contains('-')) {
        `$parentCulture = `$Culture.Split('-')[0]
        `$parentPath = Join-Path `$LocalesPath "`$parentCulture.psd1"
        if (Test-Path `$parentPath) {
            return Import-PowerShellDataFile -Path `$parentPath
        }
    }
    
    # Fall back to default
    `$defaultPath = Join-Path `$LocalesPath 'en-US.psd1'
    if (Test-Path `$defaultPath) {
        return Import-PowerShellDataFile -Path `$defaultPath
    }
    
    return @{}
}

Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    # Read culture from request context (set by middleware) - preferred method
    `$culture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
    
    # Alternative fallback: `$culture = `$Context.Items['KrCulture']
    
    # Load localized strings from .psd1 file
    `$strings = Get-LocalizedStrings -Culture `$culture -LocalesPath '$localesPath'
    
    Write-KrJsonResponse @{ 
        culture = `$culture
        message = `$strings.Message
        greeting = `$strings.Greeting
    } -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/api/culture' -ScriptBlock {
    Write-KrJsonResponse @{
        CurrentCulture = [System.Globalization.CultureInfo]::CurrentCulture.Name
        CurrentUICulture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
        HttpContextItemsValue = `$Context.Items['KrCulture']
    } -StatusCode 200
}

Start-KrServer
"@
        $script:testPath = Join-Path $TestDrive 'RequestLocalizationTest.ps1'
        Set-Content -Path $script:testPath -Value $testScript
        
        $script:instance = Start-ExampleScript -Path $script:testPath
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Server responds 200 with default culture (en-US) and loads strings from .psd1' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/" -Method Get
        $response.culture | Should -Be 'en-US'
        $response.message | Should -Be 'Hello'
        $response.greeting | Should -Be 'Welcome'
    }

    It 'Culture can be changed via query string (?culture=fr-FR) and loads French .psd1' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/?culture=fr-FR" -Method Get
        $response.culture | Should -Be 'fr-FR'
        $response.message | Should -Be 'Bonjour'
        $response.greeting | Should -Be 'Bienvenue'
    }

    It 'Culture can be changed to Spanish (?culture=es-ES) and loads Spanish .psd1' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/?culture=es-ES" -Method Get
        $response.culture | Should -Be 'es-ES'
        $response.message | Should -Be 'Hola'
        $response.greeting | Should -Be 'Bienvenido'
    }

    It 'Culture API endpoint returns current culture information and HttpContext.Items value' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/api/culture?culture=fr-FR" -Method Get
        $response.CurrentCulture | Should -Be 'fr-FR'
        $response.CurrentUICulture | Should -Be 'fr-FR'
        $response.HttpContextItemsValue | Should -Be 'fr-FR'
    }

    It 'Unsupported culture falls back to default (en-US)' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/?culture=de-DE" -Method Get
        $response.culture | Should -Be 'en-US'
        $response.message | Should -Be 'Hello'
    }

    It 'Culture can be set via Accept-Language header' {
        $headers = @{ 'Accept-Language' = 'fr-FR' }
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/" -Method Get -Headers $headers
        $response.culture | Should -Be 'fr-FR'
        $response.message | Should -Be 'Bonjour'
    }

    It 'PowerShell handlers read culture from CurrentUICulture (not computed in endpoint)' {
        # This test verifies that culture is determined by middleware, not in the endpoint
        $response1 = Invoke-RestMethod -Uri "$($script:instance.Url)/api/culture?culture=es-ES" -Method Get
        $response2 = Invoke-RestMethod -Uri "$($script:instance.Url)/?culture=es-ES" -Method Get
        
        # Both endpoints should see the same culture set by middleware
        $response1.CurrentUICulture | Should -Be 'es-ES'
        $response2.culture | Should -Be 'es-ES'
    }
    
    It 'HttpContext.Items["KrCulture"] is available as fallback' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/api/culture?culture=en-US" -Method Get
        $response.HttpContextItemsValue | Should -Be 'en-US'
        $response.HttpContextItemsValue | Should -Be $response.CurrentUICulture
    }
}
