<#
    .SYNOPSIS
        Writes a redirect response to the HTTP client.
    .DESCRIPTION
        Sets the Location header to the provided URL and optionally includes a
        message body describing the redirect.
    .PARAMETER Url
        The URL to redirect the client to. This should be a fully qualified URL.
    .PARAMETER Message
        An optional message to include in the response body. This can be used to provide additional context about the redirect.
    .EXAMPLE
        Write-KrRedirectResponse -Url "https://example.com/new-page" -Message "You are being redirected to the new page."
        Redirects the client to "https://example.com/new-page" and includes a message in the response body.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrRedirectResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [Parameter()]
        [string]$Message
    )
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteRedirectResponse($Url, $Message)
    }
}
