param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 15.11-RequestLocalization' -Tag 'Tutorial', 'Middleware', 'RequestLocalization' {
    BeforeAll { 
        # Create a simple test script
        $testScript = @'
Import-Module "$PSScriptRoot/../../../src/PowerShell/Kestrun/Kestrun.psm1" -Force

New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

$server = New-KrServer -Name 'RequestLocalization Test'
Add-KrEndpoint -Port 0 -IPAddress ([IPAddress]::Loopback)

Add-KrRequestLocalizationMiddleware `
    -DefaultCulture 'en-US' `
    -SupportedCultures @('en-US', 'fr-FR', 'es-ES')

Set-KrSharedState -Name 'Greetings' -Value @{
    'en-US' = @{ Message = 'Hello' }
    'fr-FR' = @{ Message = 'Bonjour' }
    'es-ES' = @{ Message = 'Hola' }
}

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    $culture = [System.Globalization.CultureInfo]::CurrentCulture.Name
    if (-not $Greetings.ContainsKey($culture)) { $culture = 'en-US' }
    $message = $Greetings[$culture].Message
    Write-KrJsonResponse @{ culture = $culture; message = $message } -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/api/culture' -ScriptBlock {
    Write-KrJsonResponse @{
        CurrentCulture = [System.Globalization.CultureInfo]::CurrentCulture.Name
        CurrentUICulture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
    } -StatusCode 200
}

Start-KrServer
'@
        $script:testPath = Join-Path $TestDrive 'RequestLocalizationTest.ps1'
        Set-Content -Path $script:testPath -Value $testScript
        
        $script:instance = Start-ExampleScript -Path $script:testPath
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Server responds 200 with default culture (en-US)' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/" -Method Get
        $response.culture | Should -Be 'en-US'
        $response.message | Should -Be 'Hello'
    }

    It 'Culture can be changed via query string (?culture=fr-FR)' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/?culture=fr-FR" -Method Get
        $response.culture | Should -Be 'fr-FR'
        $response.message | Should -Be 'Bonjour'
    }

    It 'Culture can be changed to Spanish (?culture=es-ES)' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/?culture=es-ES" -Method Get
        $response.culture | Should -Be 'es-ES'
        $response.message | Should -Be 'Hola'
    }

    It 'Culture API endpoint returns current culture information' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/api/culture?culture=fr-FR" -Method Get
        $response.CurrentCulture | Should -Be 'fr-FR'
        $response.CurrentUICulture | Should -Be 'fr-FR'
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
}
