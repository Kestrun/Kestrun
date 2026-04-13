<#
.SYNOPSIS
    Combines multiple X509KeyUsageFlags into a single value.
.DESCRIPTION
    The Join-KeyUsageFlags function takes an array of X509KeyUsageFlags and combines them using a bitwise OR operation to produce a single X509KeyUsageFlags value
    that represents all the specified key usage flags.
    This is useful for scenarios where multiple key usage flags need to be included in a certificate request or self-signed certificate generation,
    and the underlying APIs expect a single combined value rather than an array of individual flags.
.PARAMETER KeyUsage
    An array of X509KeyUsageFlags to combine. Each element in the array should be a valid X509KeyUsageFlags value such as DigitalSignature, KeyEncipherment, DataEncipherment, etc.
.OUTPUTS
    [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags] - A single combined X509KeyUsageFlags value that represents all the specified key usage flags.
    If no valid flags are provided, it returns $null.
.EXAMPLE
    $combinedFlags = Join-KeyUsageFlags -KeyUsage DigitalSignature, KeyEncipherment
    Write-Output $combinedFlags

    This example combines the DigitalSignature and KeyEncipherment flags into a single X509KeyUsageFlags value and outputs it.
.EXAMPLE
    $combinedFlags = Join-KeyUsageFlags -KeyUsage DataEncipherment, KeyAgreement
    Write-Output $combinedFlags

    This example combines the DataEncipherment and KeyAgreement flags into a single X509KeyUsageFlags value and outputs it.
#>
function Join-KeyUsageFlags {
    param(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags[]]$KeyUsage
    )

    $combinedKeyUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::None

    foreach ($keyUsageFlag in $KeyUsage) {
        $combinedKeyUsage = $combinedKeyUsage -bor $keyUsageFlag
    }

    if ($combinedKeyUsage -eq [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::None) {
        return $null
    }

    return $combinedKeyUsage
}
