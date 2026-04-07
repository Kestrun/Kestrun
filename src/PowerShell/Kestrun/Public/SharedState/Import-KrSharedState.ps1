<#
    .SYNOPSIS
        Imports a shared state object from a string, byte array, or file, using a mutex to ensure thread-safe access to the shared state data.
    .DESCRIPTION
        The Import-KrSharedState function allows you to import a shared state object that has been serialized as a string, byte array, or stored in a file.
        It uses a mutex to ensure that only one thread can access the shared state data at a time, preventing race conditions and ensuring data integrity.
        The function supports deserialization of the shared state object using PowerShell's PSSerializer.
    .PARAMETER InputString
        A string containing the serialized shared state object. This parameter is used when the shared state data is provided as a string.
    .PARAMETER InputBytes
        A byte array containing the serialized shared state object. This parameter is used when the shared state data is provided as a byte array.
    .PARAMETER Path
        The file path to a file containing the serialized shared state object. This parameter is used when the shared state data is stored in a file.
    .PARAMETER LockKey
        An optional string that specifies the logical lock key used to synchronize access to the shared state data. If not provided, a default lock key is used.
    .PARAMETER TimeoutMilliseconds
        The maximum time in milliseconds to wait for the mutex before timing out. The default value is 30000 (30 seconds).
    .PARAMETER Encoding
        The text encoding to use when reading the serialized shared state data. The default value is UTF8.
    .EXAMPLE
        $sharedState = Import-KrSharedState -InputString $serializedState
        This example demonstrates how to import a shared state object from a serialized string.
        The $serializedState variable contains the serialized representation of the shared state, and the Import-KrSharedState function deserializes it back into a PowerShell object that can be used in the script.
    .EXAMPLE
        $sharedState = Import-KrSharedState -InputBytes $serializedStateBytes
        This example demonstrates how to import a shared state object from a byte array.
        The $serializedStateBytes variable contains the serialized representation of the shared state as a byte array,
        and the Import-KrSharedState function deserializes it back into a PowerShell object that can be used in the script.
    .EXAMPLE
        $sharedState = Import-KrSharedState -Path 'C:\path\to\sharedState.xml'
        This example demonstrates how to import a shared state object from a file.
        The specified file contains the serialized representation of the shared state, and the Import-KrSharedState function reads the file, deserializes the content,
        and returns it as a PowerShell object that can be used in the script.
    .NOTES
        This function is part of the Kestrun.SharedState module and is used to import shared state objects that have been exported using the Export-KrSharedState function.
        It ensures thread-safe access to the shared state data by using a mutex, and it supports multiple input formats for flexibility in how the shared state data is provided to the script.
        The function also includes error handling to manage issues such as timeouts when waiting for the mutex or problems with deserialization.
#>function Import-KrSharedState {
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
