<#!
    Sample: Localization (PowerShell string tables)
    File:   21.1-Localization.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Set-KrLoggerLevel -Value Information |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Localization Demo'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

Add-KrLocalizationMiddleware -ResourcesBasePath './Assets/i18n'

Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Expand-KrObject -InputObject $Context.Culture -Label 'Current Culture'
    $culture = [System.Globalization.CultureInfo]::CurrentCulture
    $now = Get-Date
    $payload = [ordered]@{
        culture = $Context.Culture
        hello = Get-KrLocalizedString -Key 'Hello' -Default 'Hello'
        save = Get-KrLocalizedString -Key 'Labels.Save' -Default 'Save'
        cancel = Get-KrLocalizedString -Key 'Labels.Cancel' -Default 'Cancel'
        dateSample = $now.ToString('D', $culture)
        currencySample = 1234.56.ToString('C', $culture)
        calendar = [System.Globalization.CultureInfo]::GetCultureInfo($Context.Culture).Calendar
        calendarName = [System.Globalization.CultureInfo]::GetCultureInfo($Context.Culture).Calendar.GetType().Name
    }

    Write-KrJsonResponse -InputObject $payload -StatusCode 200
}

Enable-KrConfiguration

Start-KrServer
