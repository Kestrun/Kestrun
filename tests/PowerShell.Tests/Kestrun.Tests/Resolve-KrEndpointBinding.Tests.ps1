[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

Describe 'Resolve-KrEndpointBinding' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    }

    BeforeEach {
        $script:originalAspNetCoreUrls = [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS')
        $script:originalPort = [Environment]::GetEnvironmentVariable('PORT')
    }

    AfterEach {
        [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $script:originalAspNetCoreUrls)
        [Environment]::SetEnvironmentVariable('PORT', $script:originalPort)
    }

    Context 'private resolver behavior' {
        It 'returns explicit Uri binding before any other source' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://localhost:6001')
            [Environment]::SetEnvironmentVariable('PORT', '7001')

            $uri = [uri]'https://example.test:5443'
            $binding = & (Get-Module Kestrun) {
                param($InnerUri)
                Resolve-KrEndpointBinding `
                    -BoundParameters @{ Uri = $InnerUri } `
                    -Uri $InnerUri
            } $uri

            $binding.Mode | Should -Be 'Uri'
            $binding.Source | Should -Be 'Explicit'
            $binding.Uri | Should -Be $uri
            $binding.RawUrl | Should -Be $uri.AbsoluteUri
        }

        It 'returns explicit HostName binding and uses the default port when Port is not explicitly supplied' {
            $binding = & (Get-Module Kestrun) {
                Resolve-KrEndpointBinding `
                    -BoundParameters @{ HostName = 'api.example.test' } `
                    -HostName 'api.example.test' `
                    -DefaultPort 5100
            }

            $binding.Mode | Should -Be 'HostName'
            $binding.Source | Should -Be 'Explicit'
            $binding.HostName | Should -Be 'api.example.test'
            $binding.Port | Should -Be 5100
        }

        It 'returns explicit PortIPAddress binding when Port and IPAddress are supplied' {
            $ipAddress = [System.Net.IPAddress]::Parse('127.0.0.1')
            $binding = & (Get-Module Kestrun) {
                param($InnerIpAddress)
                Resolve-KrEndpointBinding `
                    -BoundParameters @{ Port = 8080; IPAddress = $InnerIpAddress } `
                    -Port 8080 `
                    -IPAddress $InnerIpAddress
            } $ipAddress

            $binding.Mode | Should -Be 'PortIPAddress'
            $binding.Source | Should -Be 'Explicit'
            $binding.Port | Should -Be 8080
            $binding.IPAddress | Should -Be $ipAddress
        }

        It 'uses the first ASPNETCORE_URLS entry when no explicit binding was supplied' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://localhost:6010;http://127.0.0.1:6011')
            [Environment]::SetEnvironmentVariable('PORT', $null)

            $binding = & (Get-Module Kestrun) {
                Resolve-KrEndpointBinding -BoundParameters @{}
            }

            $binding.Mode | Should -Be 'HostName'
            $binding.Source | Should -Be 'Environment:ASPNETCORE_URLS'
            $binding.HostName | Should -Be 'localhost'
            $binding.Port | Should -Be 6010
            $binding.Scheme | Should -Be 'http'
            $binding.RawUrl | Should -Be 'http://localhost:6010'
        }

        It 'maps ASPNETCORE_URLS wildcard hosts to IPAddress Any' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://+:6012')
            [Environment]::SetEnvironmentVariable('PORT', $null)

            $binding = & (Get-Module Kestrun) {
                Resolve-KrEndpointBinding -BoundParameters @{}
            }

            $binding.Mode | Should -Be 'PortIPAddress'
            $binding.Source | Should -Be 'Environment:ASPNETCORE_URLS'
            $binding.Port | Should -Be 6012
            $binding.IPAddress | Should -Be ([System.Net.IPAddress]::Any)
            $binding.Scheme | Should -Be 'http'
        }

        It 'falls back to PORT when ASPNETCORE_URLS is not set' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $null)
            [Environment]::SetEnvironmentVariable('PORT', '6013')

            $binding = & (Get-Module Kestrun) {
                Resolve-KrEndpointBinding -BoundParameters @{}
            }

            $binding.Mode | Should -Be 'PortIPAddress'
            $binding.Source | Should -Be 'Environment:PORT'
            $binding.Port | Should -Be 6013
            $binding.IPAddress | Should -Be ([System.Net.IPAddress]::Any)
        }

        It 'ignores environment variables when IgnoreEnvironment is specified' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://localhost:6014')
            [Environment]::SetEnvironmentVariable('PORT', '6015')

            $binding = & (Get-Module Kestrun) {
                Resolve-KrEndpointBinding `
                    -BoundParameters @{} `
                    -DefaultPort 5010 `
                    -DefaultIPAddress ([System.Net.IPAddress]::Loopback) `
                    -IgnoreEnvironment
            }

            $binding.Mode | Should -Be 'PortIPAddress'
            $binding.Source | Should -Be 'Default'
            $binding.Port | Should -Be 5010
            $binding.IPAddress | Should -Be ([System.Net.IPAddress]::Any)
        }

        It 'returns the current fallback binding when no explicit or environment binding exists' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $null)
            [Environment]::SetEnvironmentVariable('PORT', $null)

            $defaultIp = [System.Net.IPAddress]::Parse('127.0.0.2')
            $binding = & (Get-Module Kestrun) {
                param($InnerDefaultIp)
                Resolve-KrEndpointBinding `
                    -BoundParameters @{} `
                    -DefaultPort 5020 `
                    -DefaultIPAddress $InnerDefaultIp
            } $defaultIp

            $binding.Mode | Should -Be 'PortIPAddress'
            $binding.Source | Should -Be 'Default'
            $binding.Port | Should -Be 5020
            $binding.IPAddress | Should -Be ([System.Net.IPAddress]::Any)
        }

        It 'throws when PORT contains an invalid value' {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $null)
            [Environment]::SetEnvironmentVariable('PORT', '70000')

            {
                & (Get-Module Kestrun) {
                    Resolve-KrEndpointBinding -BoundParameters @{}
                }
            } | Should -Throw "Environment variable PORT has invalid value '70000'. Expected an integer between 1 and 65535."
        }
    }
}
