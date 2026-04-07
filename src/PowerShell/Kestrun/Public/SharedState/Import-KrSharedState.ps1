<#
.SYNOPSIS
    Imports a PowerShell object from a serialized XML representation.

.DESCRIPTION
    Import-KrSharedState deserializes a PowerShell object that was previously
    created by Export-KrSharedState. The serialized content can come from a
    string, a byte array, or a file.

    Access is synchronized through a shared lock so that callers using the
    same lock deserialize shared state in a thread-safe manner within the
    current process.

.PARAMETER InputString
    The serialized XML content as a string.

.PARAMETER InputBytes
    The serialized XML content as a byte array.

.PARAMETER Path
    The file path containing the serialized XML content.

.PARAMETER Lock
    The semaphore used to synchronize access to shared state. If not provided,
    the default shared-state lock is used.

.PARAMETER TimeoutMilliseconds
    The maximum time to wait for the lock. Use -1 to wait indefinitely.

.PARAMETER Encoding
    The text encoding used when converting bytes to text or reading from a file.

.EXAMPLE
    $state = Import-KrSharedState -InputString $xml

.EXAMPLE
    $state = Import-KrSharedState -InputBytes $bytes

.EXAMPLE
    $state = Import-KrSharedState -Path '.\state.xml'

.EXAMPLE
    $lock = Get-KrLock 'sharedstate:cache'
    $state = Import-KrSharedState -InputString $xml -Lock $lock
#>
function Import-KrSharedState {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'FromString')]
    param(
        [Parameter(ParameterSetName = 'FromString', Mandatory, Position = 0)]
        [AllowEmptyString()]
        [string]$InputString,

        [Parameter(ParameterSetName = 'FromBytes', Mandatory, Position = 0)]
        [byte[]]$InputBytes,

        [Parameter(ParameterSetName = 'FromFile', Mandatory, Position = 0)]
        [string]$Path,

        [Parameter()]
        [System.Threading.SemaphoreSlim]$Lock,

        [Parameter()]
        [int]$TimeoutMilliseconds = 30000,

        [Parameter()]
        [System.Text.Encoding]$Encoding = [System.Text.Encoding]::UTF8
    )

    $stateLock = $null
    $lockTaken = $false

    try {
        # Resolve lock
        $stateLock = ($Lock)? $Lock : [Kestrun.Utilities.KestrunLockRegistry]::Default

        # Acquire
        if ($TimeoutMilliseconds -lt 0) {
            $stateLock.Wait()
            $lockTaken = $true
        } else {
            $lockTaken = $stateLock.Wait($TimeoutMilliseconds)
            if (-not $lockTaken) {
                throw 'Timeout waiting for shared state lock.'
            }
        }

        switch ($PSCmdlet.ParameterSetName) {
            'FromString' {
                return [System.Management.Automation.PSSerializer]::Deserialize($InputString)
            }

            'FromBytes' {
                $xml = $Encoding.GetString($InputBytes)
                return [System.Management.Automation.PSSerializer]::Deserialize($xml)
            }

            'FromFile' {
                $fullPath = [System.IO.Path]::GetFullPath($Path)

                if (-not (Test-Path -LiteralPath $fullPath)) {
                    throw "File not found: $fullPath"
                }

                $xml = [System.IO.File]::ReadAllText($fullPath, $Encoding)
                return [System.Management.Automation.PSSerializer]::Deserialize($xml)
            }
        }
    } finally {
        if ($stateLock -and $lockTaken) {
            try {
                $null = $stateLock.Release()
            } catch {
                Write-KrLog -Level Verbose -Message 'Failed to release shared state lock' -Exception $_
            }
        }
    }
}
