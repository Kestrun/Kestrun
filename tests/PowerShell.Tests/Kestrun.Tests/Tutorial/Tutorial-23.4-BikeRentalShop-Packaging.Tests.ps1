param()

BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
    $modulePath = Get-KestrunModulePath
    Import-Module $modulePath -Force
    $script:root = Get-ProjectRootDirectory
}

Describe 'Tutorial 23.4/23.5 - BikeRentalShop packaging lifecycle' -Tag 'Tutorial' {
    It 'packages the three BikeRentalShop example folders with preserved application data folders' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-bike-rental-packaging-{0}' -f [guid]::NewGuid().ToString('N'))
        $targets = @(
            @{ Folder = 'Synchronized'; Name = 'bike-rental-shop'; Package = 'bike-rental-shop.krpack' },
            @{ Folder = 'Concurrent'; Name = 'bike-rental-shop-concurrent'; Package = 'bike-rental-shop-concurrent.krpack' },
            @{ Folder = 'Web'; Name = 'bike-rental-shop-web'; Package = 'bike-rental-shop-web.krpack' }
        )

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force

            foreach ($target in $targets) {
                $sourceFolder = Join-Path $script:root "examples/PowerShell/BikeRentalShop/$($target.Folder)"
                $packagePath = Join-Path $tempRoot $target.Package

                $result = New-KrServicePackage -SourceFolder $sourceFolder -OutputPath $packagePath -Force

                Test-Path -LiteralPath $packagePath -PathType Leaf | Should -BeTrue
                $result.Name | Should -Be $target.Name
                @($result.ApplicationDataFolders) | Should -Be @('data/', 'logs/')
            }
        } finally {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'uses the dotnet kestrun tool surface for install and update commands' {
        $output = & dotnet kestrun service help 2>&1 | Out-String

        $LASTEXITCODE | Should -Be 0
        $output | Should -Match 'service install \[--package <path-or-url-to-\.krpack>\]'
        $output | Should -Match 'service update --name <service-name> \[--package <path-or-url-to-\.krpack>\]'
    }
}
