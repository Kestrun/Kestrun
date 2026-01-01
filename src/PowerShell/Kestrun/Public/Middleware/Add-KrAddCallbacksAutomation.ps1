<#
    .SYNOPSIS
        Adds callback automation middleware to the Kestrun host.
    .DESCRIPTION
        This cmdlet adds middleware to the Kestrun host that enables automatic handling of callbacks using
        specified options or individual parameters for configuration.
    .PARAMETER Server
        The Kestrun host instance to which the middleware will be added. If not specified, the current host instance will be used.
    .PARAMETER Options
        An instance of HttpsRedirectionOptions to configure the HTTPS redirection middleware.
    .PARAMETER DefaultTimeout
        The default timeout in seconds for callback operations. Used when Options is not provided.
    .PARAMETER MaxAttempts
        The maximum number of attempts for callback operations. Used when Options is not provided.
    .PARAMETER BaseDelay
        The base delay in seconds between callback attempts. Used when Options is not provided.
    .PARAMETER MaxDelay
        The maximum delay in seconds between callback attempts. Used when Options is not provided.
    .PARAMETER PassThru
        If specified, the cmdlet returns the modified Kestrun host instance.
    .EXAMPLE
        PS> Add-KrAddCallbacksAutomation -DefaultTimeout 30 -MaxAttempts 5 -BaseDelay 2 -MaxDelay 60
        Adds callback automation middleware to the current Kestrun host with specified parameters and returns the modified host instance.
    .EXAMPLE
        PS> $server = Get-KrServer
        PS> Add-KrAddCallbacksAutomation -Server $server -Options $customOptions
        Adds callback automation middleware to the specified Kestrun host using the provided options.
 .NOTES
        This cmdlet is part of the Kestrun PowerShell module.
 #>
function Add-KrAddCallbacksAutomation {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Callback.CallbackDispatchOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, 600)]
        [int]$DefaultTimeout,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, 99)]
        [int]$MaxAttempts,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, 30)]
        [int]$BaseDelay,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, 300)]
        [int]$MaxDelay,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            # Create options from individual parameters
            $Options = [Kestrun.Callback.CallbackDispatchOptions]::new()
            # Set default values
            if ($PSBoundParameters.ContainsKey('DefaultTimeout')) {
                $Options.DefaultTimeout = [TimeSpan]::FromSeconds($DefaultTimeout)
            }
            if ($PSBoundParameters.ContainsKey('MaxAttempts')) {
                $Options.MaxAttempts = $MaxAttempts
            }
            if ($PSBoundParameters.ContainsKey('BaseDelay')) {
                $Options.BaseDelay = [TimeSpan]::FromSeconds($BaseDelay)
            }
            if ($PSBoundParameters.ContainsKey('MaxDelay')) {
                $Options.MaxDelay = [TimeSpan]::FromSeconds($MaxDelay)
            }
        }

        # Add the callback automation middleware to the server
        $Server.AddCallbacksAutomation($Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

