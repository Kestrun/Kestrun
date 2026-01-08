<#
    .SYNOPSIS
        Enables Kestrun server configuration and starts the server.
    .DESCRIPTION
        This function applies the configuration to the Kestrun server and starts it.
    .PARAMETER Server
        The Kestrun server instance to configure and start. This parameter is mandatory.
    .PARAMETER ExcludeVariables
        An array of variable names to exclude from the runspaces.
    .PARAMETER ExcludeFunctions
        An array of function name patterns to exclude from the runspaces.
    .PARAMETER Quiet
        If specified, suppresses output messages during the configuration and startup process.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after applying the configuration.
    .EXAMPLE
        Enable-KrConfiguration -Server $server
        Applies the configuration to the specified Kestrun server instance and starts it.
    .NOTES
        This function is designed to be used after the server has been configured with routes, listeners,
        and other middleware components.
#>
function Enable-KrConfiguration {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$ExcludeVariables,
        [Parameter()]
        [string[]]$ExcludeFunctions,
        [Parameter()]
        [switch]$Quiet,
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Collect assigned variables from the parent scope, resolving their values
        $variables = Get-KrAssignedVariable -FromParent -ResolveValues -IncludeSetVariable -ExcludeVariables $ExcludeVariables -OutputStructure StringObjectMap -WithoutAttributesOnly

        Write-KrLog -Level Debug -Logger $Server.Logger -Message 'Collected {VarCount} user-defined variables for server configuration.' -Values $variables.Count

        # AUTO: determine caller script path to filter session-defined functions
        $callerPath = [Kestrun.KestrunHostManager]::EntryScriptPath
        if ([string]::IsNullOrEmpty($callerPath)) {
            throw 'KestrunHostManager is not properly initialized. EntryScriptPath is not set.'
        }
        # AUTO: collect session-defined functions present now
        $fx = @( Get-Command -CommandType Function | Where-Object { -not $_.Module } )

        if ($callerPath) {
            $fx = $fx | Where-Object {
                $_.ScriptBlock.File -and
                ((Resolve-Path -LiteralPath $_.ScriptBlock.File -ErrorAction SilentlyContinue)?.ProviderPath) -eq $callerPath
            }
            $fx = @($fx)  # normalize again
        }

        # Exclude functions by name patterns if specified
        if ($ExcludeFunctions) {
            $fx = $fx | Where-Object {
                $n = $_.Name
                -not ($ExcludeFunctions | Where-Object { $n -like $_ }).Count
            }
            $fx = @($fx)  # normalize again
        }

        # Create a dictionary of function names to their definitions
        # NOTE: exclude OpenAPI metadata functions (OpenApiPath/OpenApiWebhook/OpenApiCallback)
        # from the general function map; they are handled separately.
        $fxMap = $null
        if ($fx -and $fx.Count -gt 0) {
            $fxUser = @($fx | Where-Object { -not (Test-KrFunctionHasAttribute -Command $_ -AttributeNameRegex 'OpenApi(Path|Webhook|Callback)') })
            $fxMap = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($f in $fxUser) { $fxMap[$f.Name] = $f.Definition }
        }

        # Create a dictionary of OpenAPI callback function names to their definitions
        $fxCallBack = $null
        if ($fx -and $fx.Count -gt 0) {
            $cb = @($fx | Where-Object { Test-KrFunctionHasAttribute -Command $_ -AttributeNameRegex 'OpenApiCallback' })
            if ($cb -and $cb.Count -gt 0) {
                $fxCallBack = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
                foreach ($f in $cb) { $fxCallBack[$f.Name] = $f.Definition }
            }
        }

        if ($Server.Logger -and $Server.Logger.IsEnabled([Serilog.Events.LogEventLevel]::Debug)) {
            $callbackNames = @()
            if ($fxCallBack) {
                $callbackNames = @($fxCallBack.Keys)
                [System.Array]::Sort($callbackNames, [System.StringComparer]::OrdinalIgnoreCase)
            }
            Write-KrLog -Level Debug -Logger $Server.Logger -Message 'Detected {CallbackCount} OpenAPI callback function(s): {CallbackNames}' -Values ($callbackNames.Count), ($callbackNames -join ', ')
        }

        Write-KrLog -Level Debug -Logger $Server.Logger -Message 'Enabling Kestrun server configuration with {VarCount} variables and {FuncCount} functions.' -Values $variables.Count, ($fxMap?.Count ?? 0)
        # Apply the configuration to the server
        # Set the user-defined variables in the server configuration
        $Server.EnableConfiguration($variables, $fxMap, $fxCallBack) | Out-Null

        Write-KrLog -Level Information -Logger $Server.Logger -Message 'Kestrun server configuration enabled successfully.'

        if (-not $Quiet.IsPresent) {
            Write-Host 'Kestrun server configuration enabled successfully.'
            Write-Host "Server Name: $($Server.Options.ApplicationName)"
        }
        # Generate OpenAPI components for all documents
        foreach ( $doc in $Server.OpenApiDocumentDescriptor.Values ) {
            $doc.GenerateComponents()
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
