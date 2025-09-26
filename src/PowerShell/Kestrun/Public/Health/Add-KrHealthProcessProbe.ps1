<#
    .SYNOPSIS
        Registers a health probe that executes an external process.
    .DESCRIPTION
        Creates a Kestrun ProcessProbe that launches the specified command, waits for completion, and
        interprets the exit code or JSON payload to determine health. Provide tags to support filtering
        and optionally adjust the timeout enforced on the subprocess.
    .PARAMETER Server
        The Kestrun host instance to configure. If omitted, the current server context is resolved automatically.
    .PARAMETER Name
        Unique name for the probe.
    .PARAMETER FilePath
        The executable or script to launch.
    .PARAMETER Arguments
        Command-line arguments passed to the process. Defaults to an empty string.
    .PARAMETER Tags
        Optional set of tags used to include or exclude the probe when requests filter by tag.
    .PARAMETER Timeout
        Optional timeout applied to the process execution. Defaults to 10 seconds.
    .PARAMETER PassThru
        Emits the configured server instance so the call can be chained.
    .EXAMPLE
        Add-KrHealthProcessProbe -Name DiskSpace -FilePath 'pwsh' -Arguments '-File ./Scripts/Check-Disk.ps1' -Tags 'infra'
        Registers a process probe that runs a PowerShell script to evaluate disk capacity.
#>
function Add-KrHealthProcessProbe {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$FilePath,

        [string]$Arguments = '',

        [string[]]$Tags,

        [timespan]$Timeout,

        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Call New-KrServer first or pipe an existing host instance.'
        }
    }
    process {
        $normalizedTags = @()
        if ($PSBoundParameters.ContainsKey('Tags')) {
            $normalizedTags = @($Tags | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
        }

        if ($normalizedTags.Count -eq 0) {
            $normalizedTags = @()
        }

        $probe = if ($PSBoundParameters.ContainsKey('Timeout')) {
            if ($Timeout -le [timespan]::Zero) {
                throw 'Timeout must be greater than zero.'
            }
            [Kestrun.Health.ProcessProbe]::new($Name, $normalizedTags, $FilePath, $Arguments, $Timeout)
        } else {
            [Kestrun.Health.ProcessProbe]::new($Name, $normalizedTags, $FilePath, $Arguments)
        }

        try {
            $hostResult = $Server.AddProbe($probe)
            Write-KrLog -Level Information -Message "Process health probe '{0}' registered." -Properties $Name
            if ($PassThru.IsPresent) {
                return $hostResult
            }
        } catch {
            Write-KrLog -Level Error -Message "Failed to register process health probe '{0}'." -Properties $Name -ErrorRecord $_
            throw
        }
    }
}
