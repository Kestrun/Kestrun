<#
.SYNOPSIS
    Exports a PowerShell object to a serialized XML representation.

.DESCRIPTION
    Export-KrSharedState serializes a PowerShell object using
    [System.Management.Automation.PSSerializer] and returns the serialized
    data as a string, as a byte array, or writes it to a file.

    Access is synchronized through a shared lock so that callers using the
    same lock serialize shared state in a thread-safe manner within the
    current process.

.PARAMETER InputObject
    The object to serialize.

.PARAMETER Path
    The destination file path when OutputType is File.

.PARAMETER OutputType
    Specifies how the serialized XML is returned:
    - String
    - ByteArray
    - File

.PARAMETER Lock
    The semaphore used to synchronize access to shared state. If not provided,
    the default shared-state lock is used.

.PARAMETER TimeoutMilliseconds
    The maximum time to wait for the lock. Use -1 to wait indefinitely.

.PARAMETER Encoding
    The text encoding used when converting to bytes or writing to a file.

.EXAMPLE
    $xml = Export-KrSharedState -InputObject $state

.EXAMPLE
    $bytes = Export-KrSharedState -InputObject $state -OutputType ByteArray

.EXAMPLE
    Export-KrSharedState -InputObject $state -OutputType File -Path '.\state.xml'

.EXAMPLE
    $lock = Get-KrLock 'sharedstate:cache'
    Export-KrSharedState -InputObject $state -Lock $lock
#>
function Export-KrSharedState {
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([string], [byte[]], [System.IO.FileInfo])]
    [CmdletBinding(DefaultParameterSetName = 'ToString')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [AllowNull()]
        [object]$InputObject,

        [Parameter(ParameterSetName = 'ToFile', Mandatory)]
        [string]$Path,

        [Parameter()]
        [ValidateSet('String', 'ByteArray', 'File')]
        [string]$OutputType = 'String',

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

        # Acquire lock
        if ($TimeoutMilliseconds -lt 0) {
            $stateLock.Wait()
            $lockTaken = $true
        } else {
            $lockTaken = $stateLock.Wait($TimeoutMilliseconds)
            if (-not $lockTaken) {
                throw 'Timeout waiting for shared state lock.'
            }
        }

        $xml = [System.Management.Automation.PSSerializer]::Serialize($InputObject)

        switch ($OutputType) {
            'String' {
                return $xml
            }

            'ByteArray' {
                return $Encoding.GetBytes($xml)
            }

            'File' {
                if ([string]::IsNullOrWhiteSpace($Path)) {
                    throw "Path is required when OutputType is 'File'."
                }

                $fullPath = [System.IO.Path]::GetFullPath($Path)
                $directory = [System.IO.Path]::GetDirectoryName($fullPath)

                if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
                    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
                }

                [System.IO.File]::WriteAllText($fullPath, $xml, $Encoding)
                return Get-Item -LiteralPath $fullPath
            }

            default {
                throw "Unsupported OutputType '$OutputType'."
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
