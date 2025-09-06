<#
    Sample demonstrating enrichment and correlation IDs.
    - Adds a global property 'App' to the logger
    - For each request, generates a CorrelationId and attaches it to log events
    FileName: 5.3-Enrichment-Correlation-IDs.ps1
#>

$appLogger = New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrProperty -Name 'App' -Value 'LoggingSamples' |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\enrichment.log' -RollingInterval Hour |
    Register-KrLogger -Name 'app' -PassThru

New-KrServer -Name "Enrichment & Correlation IDs"
Add-KrListener -Port 5002 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration

# PowerShell route that adds a correlation id to each log event as a property
Add-KrMapRoute -Verbs Get -Path "/ps-correlation" -ScriptBlock {
    $corr = [guid]::NewGuid().ToString('N')
    Write-KrLog -Logger $appLogger -Level Information -Message "Handling /ps-correlation" -Properties @{ CorrelationId = $corr }
    Write-KrLog -Logger $appLogger -Level Debug -Message "Processing details" -Properties @{ CorrelationId = $corr }
    Write-KrTextResponse -InputObject "ps-correlation: $corr" -StatusCode 200
}

# C# route demonstrating per-request context using ForContext
Add-KrMapRoute -Verbs Get -Path "/csharp-correlation" -Code @"
    var correlationId = Guid.NewGuid().ToString("N");
    var log = app.ForContext("CorrelationId", correlationId);
    log.Information("Handling /csharp-correlation");
    log.Debug("Processing details");
    Context.Response.WriteTextResponse($"csharp-correlation: {correlationId}", 200);
"@ -Language CSharp -UsingVariables @{ app = $appLogger }

Start-KrServer
