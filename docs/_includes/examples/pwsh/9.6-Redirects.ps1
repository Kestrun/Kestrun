# 9.6 Redirects

New-KrServer -Name 'Responses 9.6' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5096' | Out-Null
Add-KrRuntimePowerShell | Out-Null

Add-KrMapRoute -Path '/old' -Method GET -ScriptBlock {
    Write-KrRedirectResponse -Url 'https://example.com/new' -Message 'Resource moved to /new'
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.6 server running on http://127.0.0.1:5096'
