# 9.5 BSON & CBOR

New-KrServer -Name 'Responses 9.5' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5095' | Out-Null
Add-KrRuntimePowerShell | Out-Null

Add-KrMapRoute -Path '/bson' -Method GET -ScriptBlock {
    [pscustomobject]@{ kind = 'bson'; ts = (Get-Date).ToUniversalTime(); values = 1..3 } | Write-KrBsonResponse -ContentType 'application/bson'
}

Add-KrMapRoute -Path '/cbor' -Method GET -ScriptBlock {
    [pscustomobject]@{ kind = 'cbor'; ts = (Get-Date).ToUniversalTime(); values = 4..6 } | Write-KrCborResponse -ContentType 'application/cbor'
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.5 server running on http://127.0.0.1:5095'
