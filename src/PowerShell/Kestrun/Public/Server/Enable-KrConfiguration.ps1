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
        $Variables = Get-KrAssignedVariable -FromParent -ResolveValues -IncludeSetVariable

        # Build user variable map as before
        $vars = [System.Collections.Generic.Dictionary[string, object]]::new()
        foreach ($v in $Variables) {
            if ($ExcludeVariables -notcontains $v.Name) {
                $null = $Server.SharedState.Set($v.Name, $v.Value, $true)
            }
        }
        Write-KrLog -Level Debug -Logger $Server.Logger -Message 'Collected {VarCount} user-defined variables for server configuration.' -Values $vars.Count
        $callerPath = $null
        $selfPath = $PSCommandPath
        foreach ($f in Get-PSCallStack) {
            $p = $f.InvocationInfo.ScriptName
            if ($p) {
                $rp = $null
                try { $tmp = Resolve-Path -LiteralPath $p -ErrorAction Stop; $rp = $tmp.ProviderPath } catch { Write-Debug "Failed to resolve path '$p': $_" }
                if ($rp -and (!$selfPath -or $rp -ne (Resolve-Path -LiteralPath $selfPath).ProviderPath)) {
                    $callerPath = $rp
                    break
                }
            }
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
        $fxMap = $null
        if ($fx -and $fx.Count -gt 0) {
            $fxMap = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($f in $fx) { $fxMap[$f.Name] = $f.Definition }
        }
        Write-KrLog -Level Debug -Logger $Server.Logger -Message 'Enabling Kestrun server configuration with {VarCount} variables and {FuncCount} functions.' -Values $vars.Count, ($fxMap?.Count ?? 0)
        # Apply the configuration to the server
        # Set the user-defined variables in the server configuration
        $Server.EnableConfiguration($vars, $fxMap) | Out-Null

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
