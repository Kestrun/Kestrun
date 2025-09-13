# 9.9 Low-Level Response

New-KrServer -Name 'Responses 9.9' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5099' | Out-Null
Add-KrRuntimePowerShell | Out-Null

Add-KrMapRoute -Path '/raw' -Method GET -ScriptBlock {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes('raw-stream')
    $ms = New-Object System.IO.MemoryStream(,$bytes)
    Write-KrResponse -InputObject $ms -StatusCode 200
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.9 server running on http://127.0.0.1:5099'
