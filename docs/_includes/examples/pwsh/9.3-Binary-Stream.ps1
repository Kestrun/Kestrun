# 9.3 Binary & Stream Responses

New-KrServer -Name 'Responses 9.3' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5093' | Out-Null
Add-KrRuntimePowerShell | Out-Null

# Binary: load file fully
Add-KrMapRoute -Path '/logo' -Method GET -ScriptBlock {
    $path = Resolve-KrPath -Path 'Assets/wwwroot/files/sample.bin' -KestrunRoot -Test
    if (Test-Path $path) {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        Write-KrBinaryResponse -InputObject $bytes -ContentType 'application/octet-stream'
    } else {
        Write-KrErrorResponse -Message 'sample.bin not found' -StatusCode 404
    }
}

# Stream: forward text file
Add-KrMapRoute -Path '/stream' -Method GET -ScriptBlock {
    $path = Resolve-KrPath -Path 'Assets/wwwroot/files/sample.txt' -KestrunRoot -Test
    if (Test-Path $path) {
        $fs = [System.IO.File]::OpenRead($path)
        Write-KrStreamResponse -InputObject $fs -ContentType 'text/plain'
    } else {
        Write-KrErrorResponse -Message 'sample.txt not found' -StatusCode 404
    }
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.3 server running on http://127.0.0.1:5093'
