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
}
