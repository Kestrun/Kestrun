<#
    Run a small API that generates a CSR (certificate signing request).
    POST /certs/csr with JSON body providing subject and key parameters.
    FileName: 6.2-Cert-CSR.ps1
#>

Initialize-KrRoot -Path $PSScriptRoot

# Configure default logging
New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

New-KrServer -Name "CSR API"
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration

# POST /certs/csr
# Body JSON example:
# {
#   "DnsNames": ["example.com", "www.example.com"],
#   "KeyType": "Ecdsa",            # or "Rsa"
#   "KeyLength": 384,               # 2048 for RSA; 256/384/521 for ECDSA
#   "Country": "US",
#   "Org": "Acme Ltd.",
#   "CommonName": "example.com"
# }
Add-KrMapRoute -Verbs Post -Pattern "/certs/csr" -ScriptBlock {
    $body = Get-KrRequestBody
    $dns = @()
    if ($body.DnsNames) {
        if ($body.DnsNames -is [System.Array]) { $dns = $body.DnsNames } else { $dns = @($body.DnsNames) }
    }

    $params = @{}
    if ($dns.Count -gt 0) { $params.DnsNames = $dns }
    if ($body.KeyType) { $params.KeyType = [string]$body.KeyType }
    if ($body.KeyLength) { $params.KeyLength = [int]$body.KeyLength }
    if ($body.Country) { $params.Country = [string]$body.Country }
    if ($body.Org) { $params.Org = [string]$body.Org }
    if ($body.CommonName) { $params.CommonName = [string]$body.CommonName }

    try {
        Write-KrLog -Level Debug -Message "Creating CSR with params: {params}" -Properties $params
        $csr = New-KrCertificateRequest @params
        Write-KrLog -Level Debug -Message "CSR created successfully: {csr}" -Properties $csr
        Write-KrJsonResponse -StatusCode 200 -InputObject @{ csrPem = $csr.CsrPem; privateKeyPem = $csr.PrivateKeyPem ; publicKeyPem = $csr.PublicKeyPem }
    } catch {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = $_.Exception.Message }
    }
}

Start-KrServer
