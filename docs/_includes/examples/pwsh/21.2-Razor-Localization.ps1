<#!
    Sample: Razor Pages + Localization
    File:   21.2-Razor-Localization.ps1
#>

param(
    [int]$Port = $env:PORT ?? 5000
)

Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Set-KrLoggerLevel -Value Information |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Razor Localization Demo'

Add-KrEndpoint -Port $Port

Add-KrLocalizationMiddleware -ResourcesBasePath './Assets/i18n' -EnableQuery

Add-KrPowerShellRazorPagesRuntime -RootPath './Assets/Pages-Localization' -PathPrefix '/ui'

Enable-KrConfiguration

Start-KrServer
