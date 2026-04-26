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

    It 'uses IPv6 any when only Port and AddressFamily InterNetworkV6 are provided' {
        Add-KrEndpoint -Port 5050 -AddressFamily InterNetworkV6

        $listener = (Get-KrServer).Options.Listeners | Select-Object -First 1

        $listener.IPAddress | Should -Be ([System.Net.IPAddress]::IPv6Any)
        $listener.Port | Should -Be 5050
    }

    It 'uses IPv4 any when only Port and AddressFamily InterNetwork are provided' {
        Add-KrEndpoint -Port 5051 -AddressFamily InterNetwork

        $listener = (Get-KrServer).Options.Listeners | Select-Object -First 1

        $listener.IPAddress | Should -Be ([System.Net.IPAddress]::Any)
        $listener.Port | Should -Be 5051
    }

    It 'uses IPv4 any when only Port is provided' {
        Add-KrEndpoint -Port 5052

        $listener = (Get-KrServer).Options.Listeners | Select-Object -First 1

        $listener.IPAddress | Should -Be ([System.Net.IPAddress]::Any)
        $listener.Port | Should -Be 5052
    }

    It 'returns route endpoint specs for a default wildcard listener when PassThru is specified' {
        $endpointNames = @(Add-KrEndpoint -Port 5053 -PassThru)

        $endpointNames | Should -Contain 'localhost:5053'
        $endpointNames | Should -Contain '127.0.0.1:5053'
    }

    It 'returns IPv6-friendly route endpoint specs for an IPv6 wildcard listener when PassThru is specified' {
        $endpointNames = @(Add-KrEndpoint -Port 5054 -AddressFamily InterNetworkV6 -PassThru)

        $endpointNames | Should -Contain 'localhost:5054'
        $endpointNames | Should -Contain '[::1]:5054'
    }

    It 'creates an HTTPS listener with a localhost development certificate when SelfSignedCert is specified' {
        { Add-KrEndpoint -Port 5054 -SelfSignedCert } | Should -Not -Throw

        $listener = (Get-KrServer).Options.Listeners | Select-Object -First 1

        $listener.UseHttps | Should -BeTrue
        $listener.X509Certificate | Should -Not -BeNullOrEmpty
        $listener.X509Certificate.Subject | Should -Be 'CN=localhost'
        $listener.X509Certificate.Issuer | Should -Be 'CN=Kestrun Development Root CA'
        $listener.Port | Should -Be 5054
    }
}
