param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Service descriptor cmdlets' {
    It 'New-KrServiceDescriptor accepts directory path and auto-appends Service.psd1' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $scriptPath = Join-Path $tempRoot 'Service.ps1'
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM

            $created = New-KrServiceDescriptor -Path $tempRoot -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0')

            Test-Path -LiteralPath $descriptorPath | Should -BeTrue
            $created.Path | Should -Be $descriptorPath
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServiceDescriptor creates a descriptor and Get-KrServiceDescriptor reads it' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'server.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $created = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0') -EntryPoint './server.ps1' -ServiceLogPath './logs/service.log' -PreservePaths @('config/settings.json', 'data/', 'logs/') -ApplicationDataFolders @('data/', 'state/')

            Test-Path -LiteralPath $descriptorPath | Should -BeTrue
            $created.Name | Should -Be 'demo'
            $created.Description | Should -Be 'Demo service'
            $created.Version | Should -Be '1.2.0'
            $created.EntryPoint | Should -Be './server.ps1'
            $created.ServiceLogPath | Should -Be './logs/service.log'
            @($created.PreservePaths) | Should -Be @('config/settings.json', 'data/', 'logs/')
            @($created.ApplicationDataFolders) | Should -Be @('data/', 'state/')

            $read = Get-KrServiceDescriptor -Path $descriptorPath
            $read.Name | Should -Be 'demo'
            $read.Description | Should -Be 'Demo service'
            $read.Version | Should -Be '1.2.0'
            @($read.PreservePaths) | Should -Be @('config/settings.json', 'data/', 'logs/')
            @($read.ApplicationDataFolders) | Should -Be @('data/', 'state/')
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServiceDescriptor fails when EntryPoint file is missing under descriptor path' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force

            {
                New-KrServiceDescriptor -Path $tempRoot -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0') -EntryPoint './missing.ps1'
            } | Should -Throw -ExpectedMessage 'EntryPoint file not found under descriptor path: ./missing.ps1'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServiceDescriptor fails when EntryPoint resolves outside descriptor path' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath (Join-Path $tempRoot '..\outside.ps1') -Value "Write-Output 'outside'" -Encoding utf8NoBOM

            {
                New-KrServiceDescriptor -Path $tempRoot -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0') -EntryPoint '..\outside.ps1'
            } | Should -Throw
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }

            $outsidePath = Join-Path $tempRoot '..\outside.ps1'
            if (Test-Path -LiteralPath $outsidePath) {
                Remove-Item -LiteralPath $outsidePath -Force -ErrorAction SilentlyContinue
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
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0') -EntryPoint './server.ps1'

            $updated = Set-KrServiceDescriptor -Path $descriptorPath -Description 'Updated service' -Version ([Version]'1.2.1') -ServiceLogPath './logs/updated.log' -PreservePaths @('db/app.db', 'cache/') -ApplicationDataFolders @('data/', 'cache/state/')
            $updated.Name | Should -Be 'demo'
            $updated.Description | Should -Be 'Updated service'
            $updated.Version | Should -Be '1.2.1'
            $updated.ServiceLogPath | Should -Be './logs/updated.log'
            @($updated.PreservePaths) | Should -Be @('db/app.db', 'cache/')
            @($updated.ApplicationDataFolders) | Should -Be @('data/', 'cache/state/')

            $preserveCleared = Set-KrServiceDescriptor -Path $descriptorPath -ClearPreservePaths
            @($preserveCleared.PreservePaths) | Should -Be @()

            $appDataCleared = Set-KrServiceDescriptor -Path $descriptorPath -ClearApplicationDataFolders
            @($appDataCleared.ApplicationDataFolders) | Should -Be @()
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Get-KrServiceDescriptor accepts directory path and reads Service.psd1' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'Service.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0')

            $read = Get-KrServiceDescriptor -Path $tempRoot
            $read.Path | Should -Be $descriptorPath
            $read.Name | Should -Be 'demo'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor accepts directory path and updates descriptor' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'Service.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0')

            $updated = Set-KrServiceDescriptor -Path $tempRoot -Version ([Version]'1.2.1') -Description 'Updated service'
            $updated.Version | Should -Be '1.2.1'
            $updated.Description | Should -Be 'Updated service'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor fails when EntryPoint file is missing under descriptor path' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'Service.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0')

            {
                Set-KrServiceDescriptor -Path $tempRoot -EntryPoint './missing.ps1'
            } | Should -Throw -ExpectedMessage 'EntryPoint file not found under descriptor path: ./missing.ps1'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor fails with clear error when descriptor Description is invalid' {
        Mock -CommandName Get-KrServiceDescriptor -ModuleName Kestrun -MockWith {
            [pscustomobject]@{
                Path = 'C:\temp\Service.psd1'
                Name = 'demo'
                Description = $null
                Version = '1.2.0'
                EntryPoint = 'Service.ps1'
                ServiceLogPath = $null
                PreservePaths = @()
                ApplicationDataFolders = @()
            }
        }

        {
            Set-KrServiceDescriptor -Path 'C:\temp\Service.psd1' -ServiceLogPath './logs/service.log'
        } | Should -Throw -ExpectedMessage 'Service descriptor is missing a valid Description. Update the descriptor or pass -Description with a non-empty value.'
    }

    It 'Set-KrServiceDescriptor fails with clear error when descriptor Version is invalid' {
        Mock -CommandName Get-KrServiceDescriptor -ModuleName Kestrun -MockWith {
            [pscustomobject]@{
                Path = 'C:\temp\Service.psd1'
                Name = 'demo'
                Description = 'Demo service'
                Version = $null
                EntryPoint = 'Service.ps1'
                ServiceLogPath = $null
                PreservePaths = @()
                ApplicationDataFolders = @()
            }
        }

        {
            Set-KrServiceDescriptor -Path 'C:\temp\Service.psd1' -ServiceLogPath './logs/service.log'
        } | Should -Throw -ExpectedMessage 'Service descriptor is missing a valid Version. Pass -Version with a value compatible with `[System.Version`].'
    }

    It 'Set-KrServiceDescriptor fails with clear error when Description is explicitly null' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'Service.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0')

            {
                Set-KrServiceDescriptor -Path $descriptorPath -Description $null
            } | Should -Throw -ExpectedMessage 'Parameter -Description cannot be null or empty.'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor fails with clear error when Version is explicitly null' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'Service.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM
            $null = New-KrServiceDescriptor -Path $descriptorPath -Name 'demo' -Description 'Demo service' -Version ([Version]'1.2.0')

            {
                Set-KrServiceDescriptor -Path $descriptorPath -Version $null
            } | Should -Throw -ExpectedMessage 'Parameter -Version cannot be null or empty and must be compatible with `[System.Version`].'
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

    It 'Set-KrServiceDescriptor preserves FormatVersion and EntryPoint for format 1.0 descriptors' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-descriptor-{0}' -f [Guid]::NewGuid().ToString('N'))
        $descriptorPath = Join-Path $tempRoot 'Service.psd1'
        $scriptPath = Join-Path $tempRoot 'server.ps1'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'descriptor'" -Encoding utf8NoBOM

            $descriptor = @(
                '@{',
                "    FormatVersion = '1.0'",
                "    Name = 'demo-package'",
                "    Description = 'Demo package service'",
                "    Version = '1.0.0'",
                "    EntryPoint = 'server.ps1'",
                '}'
            ) -join [Environment]::NewLine
            Set-Content -LiteralPath $descriptorPath -Value $descriptor -Encoding utf8NoBOM

            $updated = Set-KrServiceDescriptor -Path $descriptorPath -Description 'Updated package service' -Version ([Version]'1.1.0') -EntryPoint './server.ps1'
            $updated.EntryPoint | Should -Be './server.ps1'

            $raw = Import-PowerShellDataFile -LiteralPath $descriptorPath
            $raw['FormatVersion'] | Should -Be '1.0'
            $raw['EntryPoint'] | Should -Be './server.ps1'
            $raw.ContainsKey('Script') | Should -BeFalse
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Set-KrServiceDescriptor exposes EntryPoint and does not expose Script/ClearScript parameters' {
        $command = Get-Command Set-KrServiceDescriptor
        $command.Parameters.ContainsKey('EntryPoint') | Should -BeTrue
        $command.Parameters.ContainsKey('Script') | Should -BeFalse
        $command.Parameters.ContainsKey('ClearScript') | Should -BeFalse
    }
}
