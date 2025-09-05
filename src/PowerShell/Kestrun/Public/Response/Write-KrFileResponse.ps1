<#
    .SYNOPSIS
        Sends a file as the HTTP response.

    .DESCRIPTION
        Writes a file from disk to the response body. The file path is resolved
        relative to the Kestrun root if required. Additional options allow
        specifying the download name, forcing inline display and custom content
        type.
    .PARAMETER FilePath
        The path to the file to send in the response. This can be an absolute path
        or a relative path from the Kestrun root.
    .PARAMETER ContentType
        The content type of the file being sent. If not specified, it will be determined
        based on the file extension.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER FileDownloadName
        The name to use for the file when downloaded. If not specified, the original
        file name will be used.
    .PARAMETER ContentDisposition
        Specifies how the content should be presented in the response. Options include
        inline and attachment.
    .EXAMPLE
        Write-KrFileResponse -FilePath "C:\path\to\file.txt" -ContentType "text/plain" -StatusCode 200 -FileDownloadName "download.txt" -ContentDisposition Attachment
        Sends the file at "C:\path\to\file.txt" as a downloadable attachment
        with the name "download.txt" and a content type of "text/plain". The response
        status code is set to 200 (OK).
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrFileResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter()]
        [string]$ContentType,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$FileDownloadName,
        [Parameter()]
        [Kestrun.Models.ContentDispositionType]$ContentDisposition = [Kestrun.Models.ContentDispositionType]::NoContentDisposition
    )

    try {
        # Check if the Context.Response is available
        if ($null -ne $Context.Response) {
            # Resolve the file path relative to the Kestrun root if necessary
            $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
            Write-KrLog -Level Verbose -Message "Resolved file path: $resolvedPath"
            # Set the content disposition type if specified
            if ($ContentDisposition -ne [Kestrun.Models.ContentDispositionType]::NoContentDisposition) {
                $Context.Response.ContentDisposition.Type = $ContentDisposition.ToString()
            }
            # Set the file download name if specified
            if (!([string]::IsNullOrEmpty($FileDownloadName))) {
                $Context.Response.ContentDisposition.FileName = $FileDownloadName
            }

            # Call the C# method on the $Context.Response object
            $Context.Response.WriteFileResponse($resolvedPath, $ContentType, $StatusCode)
            Write-Information "File response written for $FilePath with download name $FileDownloadName"
        }
    } catch {
        # Handle any errors that occur during the file response writing
        Write-KrLog -Level Error -Message 'Error writing file response.' -ErrorRecord $_
    }
}

