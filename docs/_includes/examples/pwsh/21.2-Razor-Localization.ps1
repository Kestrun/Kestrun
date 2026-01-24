<#!
    Sample: Razor Pages + Localization
    File:   21.2-Razor-Localization.ps1
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

New-KrServer -Name 'Razor Localization Demo'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

Add-KrLocalizationMiddleware -ResourcesBasePath './Assets/i18n' -EnableQuery

Add-KrPowerShellRazorPagesRuntime -RootPath './Assets/Pages-Localization' -PathPrefix '/ui'

Enable-KrConfiguration

Start-KrServer
