param()

Describe 'CORS Policy Configuration' -Tag 'Unit', 'Middleware' {

    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
    }

    Context 'Policy Builder' {

        It 'Creates builder with AllowAnyOrigin' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsOrigin -Any
            $builder | Should -Not -BeNullOrEmpty
            $builder.GetType().Name | Should -Be 'CorsPolicyBuilder'
        }

        It 'Creates builder with specific origins' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsOrigin -Origins 'http://localhost:5000', 'http://example.com'
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Creates builder with AllowAnyMethod' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsMethod -Any
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Creates builder with specific methods' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsMethod -Methods GET, POST, PUT
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Creates builder with AllowAnyHeader' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsHeader -Any
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Creates builder with specific headers' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsHeader -Headers 'Content-Type', 'Authorization'
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Supports AllowCredentials' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsCredentials -Allow
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Supports DisallowCredentials' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsCredentials -Disallow
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Supports WithExposedHeaders' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsExposedHeaders -Headers 'X-Custom-Header', 'X-Another-Header'
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Supports SetPreflightMaxAge' {
            $builder = New-KrCorsPolicyBuilder | Set-KrCorsPreflightMaxAge -Seconds 3600
            $builder | Should -Not -BeNullOrEmpty
        }

        It 'Chains multiple configurations' {
            $builder = New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Set-KrCorsMethod -Methods GET, POST |
                Set-KrCorsHeader -Headers 'Content-Type' |
                Set-KrCorsCredentials -Allow |
                Set-KrCorsExposedHeaders -Headers 'X-Total-Count' |
                Set-KrCorsPreflightMaxAge -Seconds 7200

            $builder | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Policy Registration' {

        BeforeEach {
            $script:server = New-KrServer -Name 'TestCors' -PassThru
            Add-KrEndpoint -Port 5555
        }

        It 'Registers default policy and sets CorsPolicyDefined flag' {
            $server.CorsPolicyDefined | Should -Be $false

            New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Set-KrCorsMethod -Any |
                Add-KrCorsPolicy -Default

            $server.CorsPolicyDefined | Should -Be $true
        }

        It 'Registers named policy and sets CorsPolicyDefined flag' {
            $server.CorsPolicyDefined | Should -Be $false

            New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Set-KrCorsMethod -Methods GET |
                Add-KrCorsPolicy -Name 'MyPolicy'

            $server.CorsPolicyDefined | Should -Be $true
        }

        It 'Registers AllowAll default policy' {
            Add-KrCorsPolicy -Default -AllowAll
            $server.CorsPolicyDefined | Should -Be $true
        }

        It 'Registers AllowAll named policy' {
            Add-KrCorsPolicy -Name 'AllowAllPolicy' -AllowAll
            $server.CorsPolicyDefined | Should -Be $true
        }

        It 'Registers multiple named policies' {
            New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Set-KrCorsMethod -Methods GET |
                Add-KrCorsPolicy -Name 'ReadOnly'

            New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Set-KrCorsMethod -Methods POST, PUT, DELETE |
                Add-KrCorsPolicy -Name 'WriteOnly'

            $server.CorsPolicyDefined | Should -Be $true
        }

        It 'Throws when piping builder with -AllowAll' {
            { New-KrCorsPolicyBuilder | Add-KrCorsPolicy -Name 'Test' -AllowAll } |
                Should -Throw
        }
    }

    Context 'Policy Builder PassThru' {

        It 'Returns builder when -PassThru is specified' {
            $server = New-KrServer -Name 'TestPassThru' -PassThru
            Add-KrEndpoint -Port 5556

            $builder = New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Set-KrCorsMethod -Any |
                Add-KrCorsPolicy -Name 'TestPolicy' -PassThru

            $builder | Should -Not -BeNullOrEmpty
            $builder.GetType().Name | Should -Be 'CorsPolicyBuilder'
        }

        It 'Allows chaining after PassThru' {
            $server = New-KrServer -Name 'TestChaining' -PassThru
            Add-KrEndpoint -Port 5557

            $builder = New-KrCorsPolicyBuilder |
                Set-KrCorsOrigin -Origins 'http://localhost:5000' |
                Add-KrCorsPolicy -Name 'Policy1' -PassThru |
                Set-KrCorsMethod -Methods POST |
                Add-KrCorsPolicy -Name 'Policy2'

            $server.CorsPolicyDefined | Should -Be $true
        }
    }

    Context 'Deprecated AddCorsPolicyMiddleware' {

        It 'Still works with AllowAnyOrigin' {
            $server = New-KrServer -Name 'TestDeprecated' -PassThru
            Add-KrEndpoint -Port 5558

            { $server | Add-KrCorsPolicyMiddleware -Name 'OldStyle' -AllowAnyOrigin -AllowAnyMethod -AllowAnyHeader } |
                Should -Not -Throw
        }

        It 'Still works with Builder' {
            $server = New-KrServer -Name 'TestDeprecated2' -PassThru
            Add-KrEndpoint -Port 5559

            $builder = New-KrCorsPolicyBuilder | Set-KrCorsOrigin -Any
            { $server | Add-KrCorsPolicyMiddleware -Name 'OldStyle2' -Builder $builder } |
                Should -Not -Throw
        }
    }
}
