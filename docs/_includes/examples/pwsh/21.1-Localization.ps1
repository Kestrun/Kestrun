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

Add-KrLocalizationMiddleware -ResourcesBasePath './Assets/i18n' -SupportedCultures @('en-US', 'it-IT', 'fr-FR', 'es-ES', 'de-DE')

Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Expand-KrObject -InputObject $Context.Culture -Label 'Current Culture'
    $payload = [ordered]@{
        culture = $Context.Culture
        hello   = Get-KrString -Key 'Hello' -Default 'Hello'
        save    = Get-KrString -Key 'Labels.Save' -Default 'Save'
        cancel  = Get-KrString -Key 'Labels.Cancel' -Default 'Cancel'
    }

    Write-KrJsonResponse -InputObject $payload -StatusCode 200
}

Enable-KrConfiguration

Start-KrServer
