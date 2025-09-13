# 9.7 Errors

New-KrServer -Name 'Responses 9.7' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5097' | Out-Null
Add-KrRuntimePowerShell | Out-Null

Add-KrMapRoute -Path '/fail' -Method GET -ScriptBlock {
    Write-KrErrorResponse -Message 'Missing parameter' -StatusCode 400 -Details 'id required'
}

Add-KrMapRoute -Path '/boom' -Method GET -ScriptBlock {
    try {
        throw [System.InvalidOperationException]::new('Exploded')
    } catch {
        Write-KrErrorResponse -Exception $_.Exception -StatusCode 500 -IncludeStack
    }
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.7 server running on http://127.0.0.1:5097'
