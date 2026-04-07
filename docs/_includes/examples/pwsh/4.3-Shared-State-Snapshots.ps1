<#
    Sample Kestrun server demonstrating shared-state snapshot export/import.
    This example shows how to snapshot and restore a thread-safe shared
    state object while using a named lock for grouped updates.
    FileName: 4.3-Shared-State-Snapshots.ps1
#>

param(
    [int]$Port = $env:PORT ?? 5000
)

Initialize-KrRoot -Path $PSScriptRoot

New-KrServer -Name 'Shared State Snapshot Server'
Add-KrEndpoint -Port $Port

$snapshotLockKey = 'tutorial:shared-state-snapshot'

Set-KrSharedState -Name 'AppState' -Value @{
    VisitCount = 0
    Notes = @()
    LastUpdated = (Get-Date).ToUniversalTime()
} -ThreadSafe

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/state' -ScriptBlock {
    $response = Use-KrLock -Key $snapshotLockKey -ScriptBlock {
        $state = Get-KrSharedState -Name 'AppState'

        @{
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
            lastUpdated = [datetime]$state.LastUpdated
            snapshotLockKey = $snapshotLockKey
        }
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/visit' -ScriptBlock {
    $response = Use-KrLock -Key $snapshotLockKey -ScriptBlock {
        $state = Get-KrSharedState -Name 'AppState'
       ($state.VisitCount = [int]$state.VisitCount + 1)
       ($state.LastUpdated = (Get-Date).ToUniversalTime())

        return @{
            message = 'Visit recorded'
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
        }
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/note' -ScriptBlock {
    $body = Get-KrRequestBody
    $note = [string]$body.note

    if ([string]::IsNullOrWhiteSpace($note)) {
        Write-KrJsonResponse -InputObject @{ error = 'note is required' } -StatusCode 400
        return
    }

    $response = Use-KrLock -Key $snapshotLockKey -ScriptBlock {
        $state = Get-KrSharedState -Name 'AppState'
        $state.Notes = @($state.Notes) + $note
        $state.LastUpdated = (Get-Date).ToUniversalTime()

        return @{
            message = 'Note added'
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
        }
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/snapshot/export' -ScriptBlock {
    $snapshotData = Use-KrLock -Key $snapshotLockKey -ScriptBlock {
        $state = Get-KrSharedState -Name 'AppState'
        $snapshotState = [pscustomobject]@{
            VisitCount = [int]$state.VisitCount
            Notes = @($state.Notes)
            LastUpdated = [datetime]$state.LastUpdated
        }

        @{
            snapshot = Export-KrSharedState -InputObject $snapshotState
            visitCount = $snapshotState.VisitCount
            noteCount = @($snapshotState.Notes).Count
        }
    }

    Write-KrJsonResponse -InputObject @{
        snapshot = $snapshotData.snapshot
        visitCount = $snapshotData.visitCount
        noteCount = $snapshotData.noteCount
        exportedAt = (Get-Date).ToUniversalTime()
    } -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/snapshot/reset' -ScriptBlock {
    $response = Use-KrLock -Key $snapshotLockKey -ScriptBlock {
        $state = Get-KrSharedState -Name 'AppState'
        $null = ($state.VisitCount = 0)
        $null = ($state.Notes = @())
        $null = ($state.LastUpdated = (Get-Date).ToUniversalTime())

        return @{
            message = 'State reset'
            visitCount = 0
            notes = @()
        }
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/snapshot/import' -ScriptBlock {
    $body = Get-KrRequestBody
    $snapshot = [string]$body.snapshot

    if ([string]::IsNullOrWhiteSpace($snapshot)) {
        Write-KrJsonResponse -InputObject @{ error = 'snapshot is required' } -StatusCode 400
        return
    }

    $restored = Import-KrSharedState -InputString $snapshot
    $response = Use-KrLock -Key $snapshotLockKey -ScriptBlock {
        $state = Get-KrSharedState -Name 'AppState'
        $null = ($state.VisitCount = [int]$restored.VisitCount)
        $null = ($state.Notes = if ($null -eq $restored.Notes) { @() } else { @($restored.Notes) })
        $null = ($state.LastUpdated = [datetime]$restored.LastUpdated)

        return @{
            message = 'Snapshot restored'
            visitCount = [int]$state.VisitCount
            notes = @($state.Notes)
        }
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

Start-KrServer
