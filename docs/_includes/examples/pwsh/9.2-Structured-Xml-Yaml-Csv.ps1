# 9.2 Structured Formats (XML / YAML / CSV)

New-KrServer -Name 'Responses 9.2' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5092' | Out-Null
Add-KrRuntimePowerShell | Out-Null

# XML
Add-KrMapRoute -Path '/xml' -Method GET -ScriptBlock {
    [pscustomobject]@{ Id = 1; Name = 'Alpha' } | Write-KrXmlResponse -ContentType 'application/xml'
}

# YAML
Add-KrMapRoute -Path '/yaml' -Method GET -ScriptBlock {
    [pscustomobject]@{ env = 'dev'; enabled = $true } | Write-KrYamlResponse -ContentType 'application/x-yaml'
}

# CSV
Add-KrMapRoute -Path '/csv' -Method GET -ScriptBlock {
    $data = 1..5 | ForEach-Object { [pscustomobject]@{ Id = $_; Square = ($_ * $_) } }
    $data | Write-KrCsvResponse -ContentType 'text/csv'
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.2 server running on http://127.0.0.1:5092'
