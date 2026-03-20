param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}

Describe 'Service package cmdlet' {
    It 'New-KrServicePackage packages a folder that contains a valid Service.psd1 (format 1.0)' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-package-{0}' -f [Guid]::NewGuid().ToString('N'))
        $sourceFolder = Join-Path $tempRoot 'source'
        $scriptPath = Join-Path $sourceFolder 'server.ps1'
        $descriptorPath = Join-Path $sourceFolder 'Service.psd1'
        $packagePath = Join-Path $tempRoot 'from-folder.krpack'

        try {
            $null = New-Item -ItemType Directory -Path $sourceFolder -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'hello-folder'" -Encoding utf8NoBOM

            $descriptor = @(
                '@{',
                "    FormatVersion = '1.0'",
                "    Name = 'demo-folder'",
                "    Description = 'Demo folder service'",
                "    EntryPoint = 'server.ps1'",
                "    PreservePaths = @('config/settings.json','data/')",
                '}'
            ) -join [Environment]::NewLine
            Set-Content -LiteralPath $descriptorPath -Value $descriptor -Encoding utf8NoBOM

            $result = New-KrServicePackage -SourceFolder $sourceFolder -OutputPath $packagePath

            Test-Path -LiteralPath $packagePath | Should -BeTrue
            $result.Name | Should -Be 'demo-folder'
            $result.EntryPoint | Should -Be 'server.ps1'
            $result.FormatVersion | Should -Be '1.0'
            @($result.PreservePaths) | Should -Be @('config/settings.json', 'data/')

            $zip = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
            try {
                $entries = $zip.Entries | ForEach-Object { $_.FullName }
                $entries | Should -Contain 'Service.psd1'
                $entries | Should -Contain 'server.ps1'
            } finally {
                $zip.Dispose()
            }
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServicePackage can create package from script and generates Service.psd1 with EntryPoint' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-package-{0}' -f [Guid]::NewGuid().ToString('N'))
        $scriptPath = Join-Path $tempRoot 'app.ps1'
        $packagePath = Join-Path $tempRoot 'from-script.krpack'
        $extractPath = Join-Path $tempRoot 'extracted'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'hello-script'" -Encoding utf8NoBOM

            $result = New-KrServicePackage -ScriptPath $scriptPath -Name 'demo-script' -Version ([Version]'2.4.0') -OutputPath $packagePath -PreservePaths @('logs/', 'db/app.db')

            Test-Path -LiteralPath $packagePath | Should -BeTrue
            $result.Name | Should -Be 'demo-script'
            $result.EntryPoint | Should -Be 'app.ps1'
            $result.Version | Should -Be '2.4.0'
            @($result.PreservePaths) | Should -Be @('logs/', 'db/app.db')

            Expand-Archive -LiteralPath $packagePath -DestinationPath $extractPath -Force
            $descriptor = Import-PowerShellDataFile -LiteralPath (Join-Path $extractPath 'Service.psd1')

            $descriptor['FormatVersion'] | Should -Be '1.0'
            $descriptor['Name'] | Should -Be 'demo-script'
            $descriptor['EntryPoint'] | Should -Be 'app.ps1'
            $descriptor['Version'] | Should -Be '2.4.0'
            @($descriptor['PreservePaths']) | Should -Be @('logs/', 'db/app.db')
            Test-Path -LiteralPath (Join-Path $extractPath 'app.ps1') | Should -BeTrue
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServicePackage infers name from script file and uses name-version default package path' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-package-{0}' -f [Guid]::NewGuid().ToString('N'))
        $scriptPath = Join-Path $tempRoot 'demo-app.ps1'
        $extractPath = Join-Path $tempRoot 'extracted'
        $previousLocation = Get-Location

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'hello-script-defaults'" -Encoding utf8NoBOM
            Set-Location -LiteralPath $tempRoot

            $result = New-KrServicePackage -ScriptPath $scriptPath -Version ([Version]'3.2.1')

            $expectedPackagePath = Join-Path $tempRoot 'demo-app-3.2.1.krpack'
            $result.PackagePath | Should -Be $expectedPackagePath
            Test-Path -LiteralPath $expectedPackagePath | Should -BeTrue
            $result.Name | Should -Be 'demo-app'
            $result.Version | Should -Be '3.2.1'

            Expand-Archive -LiteralPath $expectedPackagePath -DestinationPath $extractPath -Force
            $descriptor = Import-PowerShellDataFile -LiteralPath (Join-Path $extractPath 'Service.psd1')
            $descriptor['Name'] | Should -Be 'demo-app'
            $descriptor['Description'] | Should -Be 'demo-app'
            $descriptor['Version'] | Should -Be '3.2.1'
            $descriptor['EntryPoint'] | Should -Be 'demo-app.ps1'
        } finally {
            Set-Location -LiteralPath $previousLocation
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServicePackage fails for folder input when Service.psd1 is missing' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-package-{0}' -f [Guid]::NewGuid().ToString('N'))
        $sourceFolder = Join-Path $tempRoot 'source'

        try {
            $null = New-Item -ItemType Directory -Path $sourceFolder -Force
            Set-Content -LiteralPath (Join-Path $sourceFolder 'server.ps1') -Value "Write-Output 'hello'" -Encoding utf8NoBOM

            { New-KrServicePackage -SourceFolder $sourceFolder } | Should -Throw '*Service descriptor not found*'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServicePackage rejects EntryPoint paths that escape package root at sibling boundaries' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-package-{0}' -f [Guid]::NewGuid().ToString('N'))
        $sourceFolder = Join-Path $tempRoot 'source'
        $siblingFolder = Join-Path $tempRoot 'source2'
        $descriptorPath = Join-Path $sourceFolder 'Service.psd1'

        try {
            $null = New-Item -ItemType Directory -Path $sourceFolder -Force
            $null = New-Item -ItemType Directory -Path $siblingFolder -Force
            Set-Content -LiteralPath (Join-Path $siblingFolder 'app.ps1') -Value "Write-Output 'outside-root'" -Encoding utf8NoBOM

            $descriptor = @(
                '@{',
                "    FormatVersion = '1.0'",
                "    Name = 'escape-entrypoint'",
                "    Description = 'EntryPoint boundary escape attempt'",
                "    EntryPoint = '../source2/app.ps1'",
                '}'
            ) -join [Environment]::NewLine
            Set-Content -LiteralPath $descriptorPath -Value $descriptor -Encoding utf8NoBOM

            { New-KrServicePackage -SourceFolder $sourceFolder } | Should -Throw '*EntryPoint escapes the package root*'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrServicePackage rejects PreservePaths entries that escape package root at sibling boundaries' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-service-package-{0}' -f [Guid]::NewGuid().ToString('N'))
        $sourceFolder = Join-Path $tempRoot 'source'
        $siblingFolder = Join-Path $tempRoot 'source2'
        $descriptorPath = Join-Path $sourceFolder 'Service.psd1'

        try {
            $null = New-Item -ItemType Directory -Path $sourceFolder -Force
            $null = New-Item -ItemType Directory -Path $siblingFolder -Force
            Set-Content -LiteralPath (Join-Path $sourceFolder 'server.ps1') -Value "Write-Output 'inside-root'" -Encoding utf8NoBOM
            Set-Content -LiteralPath (Join-Path $siblingFolder 'settings.json') -Value '{}' -Encoding utf8NoBOM

            $descriptor = @(
                '@{',
                "    FormatVersion = '1.0'",
                "    Name = 'escape-preservepaths'",
                "    Description = 'PreservePaths boundary escape attempt'",
                "    EntryPoint = 'server.ps1'",
                "    PreservePaths = @('../source2/settings.json')",
                '}'
            ) -join [Environment]::NewLine
            Set-Content -LiteralPath $descriptorPath -Value $descriptor -Encoding utf8NoBOM

            { New-KrServicePackage -SourceFolder $sourceFolder } | Should -Throw '*PreservePaths entry*escapes the package root*'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
