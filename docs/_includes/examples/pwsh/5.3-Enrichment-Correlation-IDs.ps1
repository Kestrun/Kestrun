<#
    Sample demonstrating enrichment and correlation IDs.
    - Adds a global property 'App' to the logger
    - For each request, generates a CorrelationId and attaches it to log events
    FileName: 5.3-Enrichment-Correlation-IDs.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

$appLogger = New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrEnrichProperty -Name 'App' -Value 'LoggingSamples' |
    Add-KrEnrichFromLogContext |
    Add-KrSinkConsole -OutputTemplate "[{App} {Timestamp:HH:mm:ss} {Level:u3} {CorrelationId}] {Message:lj}{NewLine}{Exception}" |
    Add-KrSinkFile -Path '.\logs\enrichment.log' -RollingInterval Hour -OutputTemplate "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({App}) {CorrelationId} {Message:lj}{NewLine}{Exception}" |
    Register-KrLogger -Name 'app' -PassThru

New-KrServer -Name "Enrichment & Correlation IDs"
Add-KrEndpoint -Port $Port -IPAddress $IPAddress


Enable-KrConfiguration

# PowerShell route that adds a correlation id to each log event as a property
Add-KrMapRoute -Verbs Get -Path "/ps-correlation" -ScriptBlock {
    $corr = [guid]::NewGuid().ToString('N')
    $scope = Push-KrLogContextProperty -Name CorrelationId -Value $corr
    try {
        Write-KrLog -Logger $appLogger -Level Information -Message "Handling /ps-correlation"
        Write-KrLog -Logger $appLogger -Level Debug -Message "Processing details"
        Write-KrTextResponse -InputObject "ps-correlation: $corr" -StatusCode 200
    } finally {
        $scope.Dispose()
    }
}

# C# route demonstrating per-request context using ForContext
Add-KrMapRoute -Verbs Get -Path "/csharp-correlation" -Code @"
    var correlationId = Guid.NewGuid().ToString("N");
    var log = appLogger.ForContext("CorrelationId", correlationId);
    log.Information("Handling /csharp-correlation");
    log.Debug("Processing details");
    Context.Response.WriteTextResponse($"csharp-correlation: {correlationId}", 200);
"@ -Language CSharp



# Start the server
Start-KrServer


# Clean up and close the logger when the server stops
Close-KrLogger -Logger $appLogger
