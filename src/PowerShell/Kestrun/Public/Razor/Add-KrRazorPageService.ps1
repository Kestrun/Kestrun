<#
    .SYNOPSIS
        Adds Razor Pages service to the server.
    .DESCRIPTION
        This cmdlet allows you to register Razor Pages with the Kestrun server.
        It can be used to serve dynamic web pages using Razor syntax.
    .PARAMETER Server
        The Kestrun server instance to which the Razor Pages service will be added.
    .PARAMETER Options
        The RazorPagesOptions to configure the Razor Pages service.
    .PARAMETER RootDirectory
        The root directory for the Razor Pages.
    .PARAMETER Conventions
        An array of page conventions to apply to the Razor Pages.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance.
    .EXAMPLE
        $server | Add-KrRazorPageService -RootDirectory '/Pages' -Conventions $conventions
        This example adds Razor Pages service to the server, specifying the root directory and conventions for the pages.
    .EXAMPLE
        $server | Add-KrRazorPageService -Options $options
        This example adds Razor Pages service to the server using the specified RazorPagesOptions.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.razorpages.razorpagesoptions?view=aspnetcore-8.0
    .NOTES
        This cmdlet is used to register Razor Pages with the Kestrun server, allowing you to serve dynamic web pages using Razor syntax.
#>
function Add-KrRazorPageService {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RootDirectory,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.Mvc.ApplicationModels.IPageConvention[]]$Conventions = @(),

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions]::new()
            if (-not [string]::IsNullOrWhiteSpace($RootDirectory)) {
                $Options.RootDirectory = $RootDirectory
            }
            if ($Conventions.Count -gt 0) {
                foreach ($c in $Conventions) {
                    $Options.Conventions.Add($c)
                }
            }
        }

        [Kestrun.Hosting.KestrunHostRazorExtensions]::AddRazorPages($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

