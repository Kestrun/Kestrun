param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Service descriptor cmdlets' {
    It 'New-KrServiceDescriptor creates a descriptor and Get-KrServiceDescriptor reads it' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'server.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $created = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0') -Script './server.ps1' -ServiceLogPath './logs/service.log' -PreservePaths @('config/settings.json', 'data/', 'logs/')

            Test-Path -LiteralPath $descriptorPath | Should -BeTrue
            $created.Name | Should -Be 'demo'
            $created.Description | Should -Be 'Demo service'
            $created.Version | Should -Be '1.2.0'
            $created.Script | Should -Be './server.ps1'
            $created.ServiceLogPath | Should -Be './logs/service.log'
            @($created.PreservePaths) | Should -Be @('config/settings.json', 'data/', 'logs/')

            $read = Get-KrServiceDescriptor -Path $descriptorPath
            $read.Name | Should -Be 'demo'
            $read.Description | Should -Be 'Demo service'
            $read.Version | Should -Be '1.2.0'
            @($read.PreservePaths) | Should -Be @('config/settings.json', 'data/', 'logs/')
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor updates allowed fields and keeps Name unchanged' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'server.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0') -Script './server.ps1'

            $updated = Set-KrServiceDescriptor -Path $descriptorPath -Description 'Updated service' -Version ([Version]'1.2.1') -ServiceLogPath './logs/updated.log' -PreservePaths @('db/app.db', 'cache/')
            $updated.Name | Should -Be 'demo'
            $updated.Description | Should -Be 'Updated service'
            $updated.Version | Should -Be '1.2.1'
            $updated.ServiceLogPath | Should -Be './logs/updated.log'
            @($updated.PreservePaths) | Should -Be @('db/app.db', 'cache/')

            $cleared = Set-KrServiceDescriptor -Path $descriptorPath -ClearScript
            $cleared.Name | Should -Be 'demo'
            [string]::IsNullOrWhiteSpace($cleared.Script) | Should -BeTrue

            $preserveCleared = Set-KrServiceDescriptor -Path $descriptorPath -ClearPreservePaths
            @($preserveCleared.PreservePaths) | Should -Be @()
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor does not expose Name as an updatable parameter' {
        $command = Get-Command Set-KrServiceDescriptor
        $command.Parameters.ContainsKey('Name') | Should -BeFalse
    }
}
