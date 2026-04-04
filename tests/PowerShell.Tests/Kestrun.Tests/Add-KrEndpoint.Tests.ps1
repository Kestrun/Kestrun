[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

Describe 'Add-KrEndpoint' {
    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot '..\..\..\src\PowerShell\Kestrun\Kestrun.psm1'
        Import-Module $modulePath -Force -ErrorAction Stop
    }

    BeforeEach {
        Remove-KrServer -Name 'AddKrEndpointTest' -Force -ErrorAction SilentlyContinue
        New-KrServer -Name 'AddKrEndpointTest' | Out-Null
    }

    AfterEach {
        Remove-KrServer -Name 'AddKrEndpointTest' -Force -ErrorAction SilentlyContinue
    }

    It 'uses IPv6 loopback when only Port and AddressFamily InterNetworkV6 are provided' {
        Add-KrEndpoint -Port 5050 -AddressFamily InterNetworkV6

        $listener = (Get-KrServer).Options.Listeners | Select-Object -First 1

        $listener.IPAddress | Should -Be ([System.Net.IPAddress]::IPv6Loopback)
        $listener.Port | Should -Be 5050
    }

    It 'keeps IPv4 loopback when only Port and AddressFamily InterNetwork are provided' {
        Add-KrEndpoint -Port 5051 -AddressFamily InterNetwork

        $listener = (Get-KrServer).Options.Listeners | Select-Object -First 1

        $listener.IPAddress | Should -Be ([System.Net.IPAddress]::Loopback)
        $listener.Port | Should -Be 5051
    }

    It 'returns route endpoint specs for a default loopback listener when PassThru is specified' {
        $endpointNames = @(Add-KrEndpoint -Port 5052 -PassThru)

        $endpointNames | Should -Contain 'localhost:5052'
        $endpointNames | Should -Contain '127.0.0.1:5052'
    }

    It 'returns IPv6-friendly route endpoint specs for an IPv6 loopback listener when PassThru is specified' {
        $endpointNames = @(Add-KrEndpoint -Port 5053 -AddressFamily InterNetworkV6 -PassThru)

        $endpointNames | Should -Contain 'localhost:5053'
        $endpointNames | Should -Contain '[::1]:5053'
    }
}
