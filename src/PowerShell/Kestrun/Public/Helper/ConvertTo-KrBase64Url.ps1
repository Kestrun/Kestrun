<#
.SYNOPSIS
    Converts a byte array to a Base64Url-encoded string.
.DESCRIPTION
    This function takes a byte array as input and returns its Base64Url-encoded representation.
    Base64Url encoding is similar to standard Base64 encoding but uses URL-safe characters.
.PARAMETER Bytes
    The byte array to be converted to Base64Url format.
.EXAMPLE
    $data = [System.Text.Encoding]::UTF8.GetBytes("Hello, World!")
    $base64Url = ConvertTo-KrBase64Url -Bytes $data
    Write-Host $base64Url  # Outputs: "SGVsbG8sIFdvcmxkIQ"
.OUTPUTS
    [string] - The Base64Url-encoded string.
#>
function ConvertTo-KrBase64Url {
    [KestrunRuntimeApi('Everywhere')]
    [outputType([string])]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [byte[]]$Bytes
    )
    # Return Base64Url-encoded string
    return [System.Buffers.Text.Base64Url]::EncodeToString($Bytes)
}

