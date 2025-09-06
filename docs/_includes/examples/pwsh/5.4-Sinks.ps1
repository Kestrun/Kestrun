<#
    Sample demonstrating sinks: console, text file, and JSON file.
    - 'text' logger: writes to console + logs/sinks-text.log
    - 'json' logger: writes to logs/sinks-json.log with JSON formatter
    FileName: 5.4-Sinks.ps1
#>

$text = New-KrLogger |
    Set-KrMinimumLevel -Value Information |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\sinks-text.log' -RollingInterval Hour |
    Register-KrLogger -Name 'text' -PassThru

$json = New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\sinks-json.log' -Formatter (Get-KrJsonFormatter) -RollingInterval Hour |
    Register-KrLogger -Name 'json' -PassThru

New-KrServer -Name "Sinks Demo"
Add-KrListener -Port 5003 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Path "/text" -ScriptBlock {
    Write-KrLog -Logger $text -Level Information -Message "Text sink example"
    Write-KrTextResponse -InputObject "text" -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Path "/json" -ScriptBlock {
    Write-KrLog -Logger $json -Level Debug -Message "Json sink example" -Properties @{ Example = $true }
    Write-KrTextResponse -InputObject "json" -StatusCode 200
}

Start-KrServer
