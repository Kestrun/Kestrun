param(
    [int]$Port = $env:PORT ?? 5000
)

New-KrServer -Name 'MCP Hello'

Add-KrEndpoint -Port $Port

Enable-KrConfiguration

Add-KrMapRoute -Pattern '/hello' -Verbs Get -ScriptBlock {
    Write-KrJsonResponse @{
        message = 'hello from Kestrun'
    }
}

Start-KrServer
