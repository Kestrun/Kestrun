
function Write-KrHtmlResponse {
    <#
    .SYNOPSIS
        Writes an HTML response to the HTTP response body.
    .DESCRIPTION
        Serializes the provided HTML template with variables and writes it to the HTTP response.
    .PARAMETER FilePath
        The path to the HTML file to read and write to the response. This can be a relative or absolute path.
    .PARAMETER Template
        The HTML template string to write to the response. If provided, this will override the FilePath parameter.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER Variables
        A hashtable of variables to use for template placeholders. These will be merged into the HTML template.
    .EXAMPLE
        Write-KrHtmlResponse -FilePath "C:\path\to\template.html" -StatusCode 200 -Variables @{ Title = "My Page"; Content = "Hello, World!" }
        Reads the HTML file at "C:\path\to\template.html", merges in the variables, and writes the resulting HTML to the response with a 200 status code.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
    #>
    [KestrunRuntimeApi([KestrunApiContext]::Route)]
    [CmdletBinding(defaultParameterSetName = "FilePath")]
    [KestrunRuntimeApi([KestrunApiContext]::Route)]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = "FilePath")]
        [string]$FilePath,
        [Parameter(Mandatory = $true, ParameterSetName = "Template")]
        [string]$Template,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [hashtable]$Variables
    )
    try {
        # Check if the Context.Response is available
        if ($null -ne $Context.Response) {

            $readOnlyDictionary = [Kestrun.Utilities.ReadOnlyDictionaryAdapter]::new($Variables)

            switch ($PSCmdlet.ParameterSetName) {
                "FilePath" {
                    # Resolve the file path relative to the Kestrun root if necessary
                    $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
                    Write-KrVerboseLog -Message "Resolved file path: $resolvedPath"
                    # Call the C# method on the $Context.Response object
                    $Context.Response.WriteHtmlResponseFromFile($resolvedPath, $readOnlyDictionary, $StatusCode)
                    Write-Information "HTML response written for $FilePath"
                }
                "Template" {
                    # Call the C# method on the $Context.Response object
                    $Context.Response.WriteHtmlResponse($Template, $readOnlyDictionary, $StatusCode)
                    Write-Information "HTML response written from template"
                }
            }
        }
    }
    catch {
        # Handle any errors that occur during the file response writing
        Write-KrErrorLog -Message "Error writing file response." -ErrorRecord $_
    }
}