param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Docker deployment cmdlet' {
    It 'New-KrDockerDeployment creates a self-contained deployment bundle from a .krpack package' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-docker-deployment-{0}' -f [Guid]::NewGuid().ToString('N'))
        $scriptPath = Join-Path $tempRoot 'Service.ps1'
        $packagePath = Join-Path $tempRoot 'demo-service.krpack'
        $outputPath = Join-Path $tempRoot 'docker'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'hello-docker'" -Encoding utf8NoBOM

            $null = New-KrServicePackage -ScriptPath $scriptPath -Name 'Demo_Service' -Description 'Demo service' -Version ([Version]'1.2.3') -OutputPath $packagePath
            $result = New-KrDockerDeployment -PackagePath $packagePath -OutputPath $outputPath

            $result.ServiceName | Should -Be 'demo-service'
            $result.ImageName | Should -Be 'kestrun-demo-service:1.2.3'
            $result.EntryPoint | Should -Be 'Service.ps1'
            $result.Version | Should -Be '1.2.3'

            $composePath = Join-Path $outputPath 'docker-compose.yml'
            $dockerfilePath = Join-Path $outputPath 'Dockerfile'
            $entrypointPath = Join-Path $outputPath 'entrypoint.sh'
            $dockerignorePath = Join-Path $outputPath '.dockerignore'
            $copiedPackagePath = Join-Path $outputPath 'app.krpack'
            $copiedModuleManifestPath = Join-Path $outputPath 'Kestrun\Kestrun.psd1'

            Test-Path -LiteralPath $composePath | Should -BeTrue
            Test-Path -LiteralPath $dockerfilePath | Should -BeTrue
            Test-Path -LiteralPath $entrypointPath | Should -BeTrue
            Test-Path -LiteralPath $dockerignorePath | Should -BeTrue
            Test-Path -LiteralPath $copiedPackagePath | Should -BeTrue
            Test-Path -LiteralPath $copiedModuleManifestPath | Should -BeTrue

            $compose = ConvertFrom-KrYaml (Get-Content -LiteralPath $composePath -Raw)
            $compose['services'].Keys | Should -Contain 'demo-service'
            $compose['services']['demo-service']['image'] | Should -Be 'kestrun-demo-service:1.2.3'
            @($compose['services']['demo-service']['ports']) | Should -Be @('8080:8080')
            $compose['services']['demo-service']['environment']['PORT'] | Should -Be '8080'
            $compose['services']['demo-service']['environment']['ASPNETCORE_URLS'] | Should -Be 'http://+:8080'

            $dockerfile = Get-Content -LiteralPath $dockerfilePath -Raw
            $dockerfile | Should -Match 'FROM mcr\.microsoft\.com/dotnet/aspnet:10\.0'
            $dockerfile | Should -Match 'apt-get install -y --no-install-recommends powershell'
            $dockerfile | Should -Match 'packages\.microsoft\.com/config/\$\{ID\}/\$\{VERSION_ID\}/packages-microsoft-prod\.deb'
            $dockerfile | Should -Match 'ENV PORT=8080'
            $dockerfile | Should -Match 'COPY Kestrun/'
            $dockerfile | Should -Match 'COPY app\.krpack'
            $dockerfile | Should -Match 'COPY entrypoint\.sh'
            $dockerfile | Should -Match '/opt/microsoft/powershell/7/profile\.ps1'
            $dockerfile | Should -Match 'ENTRYPOINT \["/opt/kestrun/entrypoint\.sh"\]'

            $entrypoint = Get-Content -LiteralPath $entrypointPath -Raw
            $entrypoint | Should -Match '#!/bin/sh'
            $entrypoint | Should -Match 'Expand-Archive -LiteralPath'
            $entrypoint | Should -Match 'Import-PowerShellDataFile -LiteralPath'
            $entrypoint | Should -Match 'exec pwsh -NoLogo -File'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrDockerDeployment honours custom service, image, and port settings' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-docker-deployment-{0}' -f [Guid]::NewGuid().ToString('N'))
        $scriptPath = Join-Path $tempRoot 'server.ps1'
        $packagePath = Join-Path $tempRoot 'demo-service.krpack'
        $outputPath = Join-Path $tempRoot 'docker'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            Set-Content -LiteralPath $scriptPath -Value "Write-Output 'hello-docker-custom'" -Encoding utf8NoBOM

            $null = New-KrServicePackage -ScriptPath $scriptPath -Name 'Demo Service' -Description 'Demo service' -Version ([Version]'2.0.0') -OutputPath $packagePath
            $result = New-KrDockerDeployment -PackagePath $packagePath -OutputPath $outputPath -ServiceName 'frontend_api' -ImageName 'registry.example/demo:2.0.0' -PublishedPort 5000 -ContainerPort 5001

            $result.ServiceName | Should -Be 'frontend-api'
            $result.ImageName | Should -Be 'registry.example/demo:2.0.0'
            $result.PublishedPort | Should -Be 5000
            $result.ContainerPort | Should -Be 5001

            $compose = ConvertFrom-KrYaml (Get-Content -LiteralPath (Join-Path $outputPath 'docker-compose.yml') -Raw)
            $compose['services'].Keys | Should -Contain 'frontend-api'
            $compose['services']['frontend-api']['container_name'] | Should -Be 'frontend-api'
            @($compose['services']['frontend-api']['ports']) | Should -Be @('5000:5001')
            $compose['services']['frontend-api']['environment']['PORT'] | Should -Be '5001'
            $compose['services']['frontend-api']['environment']['ASPNETCORE_URLS'] | Should -Be 'http://+:5001'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    It 'New-KrDockerDeployment maps BikeRentalShop application data folders to durable named volumes' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-docker-bike-rental-{0}' -f [Guid]::NewGuid().ToString('N'))
        $projectRoot = Get-ProjectRootDirectory
        $sourceFolder = Join-Path $projectRoot 'docs\_includes\examples\pwsh\BikeRentalShop\Web'
        $packagePath = Join-Path $tempRoot 'bike-rental-shop-web.krpack'
        $outputPath = Join-Path $tempRoot 'docker'

        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force

            $null = New-KrServicePackage -SourceFolder $sourceFolder -OutputPath $packagePath -Force
            $result = New-KrDockerDeployment -PackagePath $packagePath -OutputPath $outputPath

            $result.ServiceName | Should -Be 'bike-rental-shop-web'

            $compose = ConvertFrom-KrYaml (Get-Content -LiteralPath (Join-Path $outputPath 'docker-compose.yml') -Raw)
            $serviceVolumes = @($compose['services']['bike-rental-shop-web']['volumes'])
            $serviceVolumes.Count | Should -Be 2
            $serviceVolumes[0] | Should -Match '^bike-rental-shop-web-appdata-.*:/opt/kestrun/application-data/'
            $serviceVolumes[1] | Should -Match '^bike-rental-shop-web-appdata-.*:/opt/kestrun/application-data/'
            ($serviceVolumes -join "`n") | Should -Match ':/opt/kestrun/application-data/data-'
            ($serviceVolumes -join "`n") | Should -Match ':/opt/kestrun/application-data/logs-'

            $volumeKeys = @($compose['volumes'].Keys)
            $volumeKeys.Count | Should -Be 2
            $volumeKeys | Should -Contain ($serviceVolumes[0] -split ':', 2)[0]
            $volumeKeys | Should -Contain ($serviceVolumes[1] -split ':', 2)[0]

            $entrypoint = Get-Content -LiteralPath (Join-Path $outputPath 'entrypoint.sh') -Raw
            $entrypoint | Should -Match "RelativePath = 'data/'"
            $entrypoint | Should -Match "RelativePath = 'logs/'"
            $entrypoint | Should -Match 'New-Item -ItemType SymbolicLink'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
