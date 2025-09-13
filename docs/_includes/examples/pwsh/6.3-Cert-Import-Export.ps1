<#
    Run a small API for importing and exporting certificates.
    - POST /certs/import { filePath, format, password? }
    - POST /certs/export { filePath, format, outPath, includePrivateKey?, password? }
    FileName: 6.3-Cert-Import-Export.ps1
#>


function Convert-ToSecureStringOrNull {
    [CmdletBinding()]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
    param([string]$Password)
    if ([string]::IsNullOrWhiteSpace($Password)) { return $null }
    return (ConvertTo-SecureString $Password -AsPlainText -Force)
}


Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger'

New-KrServer -Name "Cert Ops API"
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration

# POST /certs/import
# Body JSON example: { "filePath": "./devcert.pfx", "password": "p@ss" }
Add-KrMapRoute -Verbs Post -Pattern "/certs/import" -ScriptBlock {
    $body = Get-KrRequestBody
    try {
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Importing certificate from {filePath}" -Properties $body.filePath
        $password = Convert-ToSecureStringOrNull -Password ([string]$body.password)
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Password provided: {hasPassword}" -Properties ($null -ne $password)
        $cert = Import-KrCertificate -FilePath ([string]$body.filePath) -Password $password
        if ( $null -eq $cert) {
            throw "Certificate import failed."
        } else {
            Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Certificate imported successfully"
        }
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Certificate imported successfully: {subject}" -Properties $cert.Subject
        $purposes = Get-KrCertificatePurpose -Certificate $cert
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Certificate purposes: {purposes}" -Properties $purposes
        $valid = Test-KrCertificate -Certificate $cert -DenySelfSigned:$false -AllowWeakAlgorithms:$false
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Certificate validity: {valid}" -Properties $valid
        Write-KrJsonResponse -StatusCode 200 -InputObject @{ subject = $cert.Subject; notAfter = $cert.NotAfter; valid = $valid; purposes = $purposes }
    } catch {
        Write-KrLog -Level Error -LoggerName 'myLogger' -Message "Certificate import failed" -ErrorRecord $_
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = $_.Exception.Message }
    }
}

# POST /certs/export
# Body JSON example: { "filePath": "./devcert.pfx", "format": "Pfx", "outPath": "./devcert", "includePrivateKey": true, "password": "p@ss" }
Add-KrMapRoute -Verbs Post -Pattern "/certs/export" -ScriptBlock {
    $body = Get-KrRequestBody
    try {
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Exporting certificate from {filePath} with format {format} to {outPath}" -Properties $body.filePath, $body.format, $body.outPath
        if (-not $body.outPath) {
            throw "Output path (outPath) is required for export."
        }
        $password = Convert-ToSecureStringOrNull -Password ([string]$body.password)
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Password provided: {hasPassword}" -Properties ($null -ne $password)
        $cert = New-KrSelfSignedCertificate -DnsNames "temp.example.com" -Exportable -ValidDays 1
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Temporary self-signed certificate created: {subject}" -Properties $cert.Subject
        $args = @{ Certificate = $cert; FilePath = ([string]$body.outPath); Format = ([string]$body.outFormat) }
        if ($body.includePrivateKey) { $args.IncludePrivateKey = [bool]$body.includePrivateKey }
        if ($password) { $args.Password = $password }
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Exporting certificate to {outPath} with format {format}" -Properties $body.outPath, $body.outFormat

        Export-KrCertificate @args | Out-Null
        Write-KrLog -Level Debug -LoggerName 'myLogger' -Message "Certificate exported successfully to {outPath} with format {format}" -Properties $body.outPath, $body.outFormat
        Write-KrJsonResponse -StatusCode 200 -InputObject @{ exported = $true; path = $body.outPath; format = $body.outFormat }
    } catch {
        Write-KrLog -Level Error -LoggerName 'myLogger' -Message "Certificate export failed" -ErrorRecord $_
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = $_.Exception.Message }
    }
}

Start-KrServer
