<#
    Sample Kestrun server demonstrating shared-state snapshot export/import.
    This example shows how to coordinate Export-KrSharedState and
    Import-KrSharedState around a named lock for a shared in-memory object.
    FileName: 4.3-Shared-State-Snapshots.ps1
#>

param(
    [int]$Port = $env:PORT ?? 5000
)

Initialize-KrRoot -Path $PSScriptRoot

if (-not (Get-Command Export-KrSharedState -ErrorAction SilentlyContinue)) {
    function Export-KrSharedState {
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

        $stateLock = if ($Lock) { $Lock } else { [Kestrun.Utilities.KestrunLockRegistry]::Default }
        $lockTaken = $false

        try {
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
                'String' { return $xml }
                'ByteArray' { return $Encoding.GetBytes($xml) }
                'File' {
                    if ([string]::IsNullOrWhiteSpace($Path)) {
                        throw 'Path is required when OutputType is File.'
                    }

                    $fullPath = [System.IO.Path]::GetFullPath($Path)
                    $directory = [System.IO.Path]::GetDirectoryName($fullPath)

                    if (-not [string]::IsNullOrWhiteSpace($directory)) {
                        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
                    }

                    [System.IO.File]::WriteAllText($fullPath, $xml, $Encoding)
                    return [System.IO.FileInfo]::new($fullPath)
                }
            }
        } finally {
            if ($lockTaken) {
                $null = $stateLock.Release()
            }
        }
    }
}

if (-not (Get-Command Import-KrSharedState -ErrorAction SilentlyContinue)) {
    function Import-KrSharedState {
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

        $stateLock = if ($Lock) { $Lock } else { [Kestrun.Utilities.KestrunLockRegistry]::Default }
        $lockTaken = $false

        try {
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
            if ($lockTaken) {
                $null = $stateLock.Release()
            }
        }
    }
}

New-KrServer -Name 'Shared State Snapshot Server'
Add-KrEndpoint -Port $Port

function Invoke-WithSnapshotLock {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock
    )

    $stateLock = [Kestrun.Utilities.KestrunLockRegistry]::GetOrCreate('tutorial:shared-state-snapshot')
    $acquired = $false

    try {
        $stateLock.Wait()
        $acquired = $true
        & $ScriptBlock
    } finally {
        if ($acquired) {
            $null = $stateLock.Release()
        }
    }
}

Set-KrSharedState -Name 'AppState' -Value @{
    VisitCount = 0
    Notes = @()
    LastUpdated = (Get-Date).ToUniversalTime()
} -ThreadSafe

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/state' -ScriptBlock {
    Invoke-WithSnapshotLock {
        $state = Get-KrSharedState -Name 'AppState'

        Write-KrJsonResponse -InputObject @{
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
            lastUpdated = [datetime]$state.LastUpdated
            snapshotLockKey = 'tutorial:shared-state-snapshot'
        } -StatusCode 200
    }
}

Add-KrMapRoute -Verbs Post -Pattern '/visit' -ScriptBlock {
    Invoke-WithSnapshotLock {
        $state = Get-KrSharedState -Name 'AppState'
        $state.VisitCount = [int]$state.VisitCount + 1
        $state.LastUpdated = (Get-Date).ToUniversalTime()

        Write-KrJsonResponse -InputObject @{
            message = 'Visit recorded'
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
        } -StatusCode 200
    }
}

Add-KrMapRoute -Verbs Post -Pattern '/note' -ScriptBlock {
    $body = Get-KrRequestBody
    $note = [string]$body.note

    if ([string]::IsNullOrWhiteSpace($note)) {
        Write-KrJsonResponse -InputObject @{ error = 'note is required' } -StatusCode 400
        return
    }

    Invoke-WithSnapshotLock {
        $state = Get-KrSharedState -Name 'AppState'
        $state.Notes = @($state.Notes) + $note
        $state.LastUpdated = (Get-Date).ToUniversalTime()

        Write-KrJsonResponse -InputObject @{
            message = 'Note added'
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
        } -StatusCode 200
    }
}

Add-KrMapRoute -Verbs Post -Pattern '/snapshot/export' -ScriptBlock {
    $state = Get-KrSharedState -Name 'AppState'
    $stateLock = [Kestrun.Utilities.KestrunLockRegistry]::GetOrCreate('tutorial:shared-state-snapshot')
    $snapshot = Export-KrSharedState -InputObject $state -Lock $stateLock

    Write-KrJsonResponse -InputObject @{
        snapshot = $snapshot
        visitCount = [int]$state.VisitCount
        noteCount = @($state.Notes).Count
        exportedAt = (Get-Date).ToUniversalTime()
    } -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/snapshot/reset' -ScriptBlock {
    Invoke-WithSnapshotLock {
        $state = Get-KrSharedState -Name 'AppState'
        $state.VisitCount = 0
        $state.Notes = @()
        $state.LastUpdated = (Get-Date).ToUniversalTime()

        Write-KrJsonResponse -InputObject @{
            message = 'State reset'
            visitCount = 0
            notes = @()
        } -StatusCode 200
    }
}

Add-KrMapRoute -Verbs Post -Pattern '/snapshot/import' -ScriptBlock {
    $body = Get-KrRequestBody
    $snapshot = [string]$body.snapshot

    if ([string]::IsNullOrWhiteSpace($snapshot)) {
        Write-KrJsonResponse -InputObject @{ error = 'snapshot is required' } -StatusCode 400
        return
    }

    $stateLock = [Kestrun.Utilities.KestrunLockRegistry]::GetOrCreate('tutorial:shared-state-snapshot')
    $restored = Import-KrSharedState -InputString $snapshot -Lock $stateLock
    $state = Get-KrSharedState -Name 'AppState'
    $state.VisitCount = [int]$restored.VisitCount
    $state.Notes = @($restored.Notes)
    $state.LastUpdated = [datetime]$restored.LastUpdated

    Write-KrJsonResponse -InputObject @{
        message = 'Snapshot restored'
        visitCount = [int]$state.VisitCount
        notes = @($state.Notes)
    } -StatusCode 200
}

Start-KrServer
