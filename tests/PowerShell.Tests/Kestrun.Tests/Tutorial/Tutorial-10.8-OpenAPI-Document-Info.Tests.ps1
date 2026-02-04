param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.8 OpenAPI Document Info' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.8-OpenAPI-Document-Info.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Check /info route output' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/info" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json -AsHashtable

        $json.title | Should -Be 'Document Info API'
        $json.version | Should -Be '1.0.0'
        $json.description | Should -Be 'Shows how to populate document metadata.'
        $json.termsOfService | Should -Be 'https://example.com/terms'
        $json.contact | Should -Be 'support@example.com'

        $json.licenseName | Should -Be 'Apache 2.0'
        $json.licenseIdentifier | Should -Be 'Apache-2.0'
        $json.licenseUrl | Should -Be $null
    }

    It 'Check OpenAPI 3.0 Info' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.0/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json -AsHashtable

        $info = $json.info
        $info.title | Should -Be 'Document Info API'
        $info.version | Should -Be '1.0.0'
        $info.description | Should -Be 'Shows how to populate document metadata.'

        $info.contact.name | Should -Be 'API Support'
        $info.contact.email | Should -Be 'support@example.com'

        $info.contact.'x-contact-department' | Should -Be 'Developer Relations'
        $info.contact.'x-contact-hours' | Should -Be '9am-5pm PST'
        $info.contact.'x-logo'.url | Should -Be 'https://www.kestrun.dev/assets/kestrun_abstract_transparent.png'
        $info.contact.'x-logo'.altText | Should -Be 'Kestrun logo'

        $info.license.name | Should -Be 'Apache 2.0'
        $info.license.ContainsKey('identifier') | Should -BeFalse
        $info.license.ContainsKey('url') | Should -BeFalse

        $servers = $json.servers
        $servers.Count | Should -BeGreaterOrEqual 4

        $serverUrls = $servers | ForEach-Object { $_.url }
        $serverUrls | Should -Contain 'https://{env}.api.example.com'
        $serverUrls | Should -Contain 'https://api.example.com'
        $serverUrls | Should -Contain 'https://staging-api.example.com'
        $serverUrls | Should -Contain '/'

        $envServer = $servers | Where-Object { $_.url -eq 'https://{env}.api.example.com' } | Select-Object -First 1
        $envServer.description | Should -Be 'Environment-specific endpoint'
        $envServer.variables.env.default | Should -Be 'dev'
        $envServer.variables.env.enum | Should -Contain 'dev'
        $envServer.variables.env.enum | Should -Contain 'staging'
        $envServer.variables.env.enum | Should -Contain 'prod'
        $envServer.variables.env.description | Should -Be 'Deployment environment'
    }

    It 'Check OpenAPI 3.1 Info' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json -AsHashtable

        $info = $json.info
        $info.title | Should -Be 'Document Info API'
        $info.version | Should -Be '1.0.0'
        $info.description | Should -Be 'Shows how to populate document metadata.'

        $info.contact.name | Should -Be 'API Support'
        $info.contact.email | Should -Be 'support@example.com'

        $info.contact.'x-contact-department' | Should -Be 'Developer Relations'
        $info.contact.'x-contact-hours' | Should -Be '9am-5pm PST'
        $info.contact.'x-logo'.url | Should -Be 'https://www.kestrun.dev/assets/kestrun_abstract_transparent.png'
        $info.contact.'x-logo'.altText | Should -Be 'Kestrun logo'

        $info.license.name | Should -Be 'Apache 2.0'
        $info.license.identifier | Should -Be 'Apache-2.0'
        $info.license.ContainsKey('url') | Should -BeFalse

        $servers = $json.servers
        $servers.Count | Should -BeGreaterOrEqual 4

        $serverUrls = $servers | ForEach-Object { $_.url }
        $serverUrls | Should -Contain 'https://{env}.api.example.com'
        $serverUrls | Should -Contain 'https://api.example.com'
        $serverUrls | Should -Contain 'https://staging-api.example.com'
        $serverUrls | Should -Contain '/'

        $envServer = $servers | Where-Object { $_.url -eq 'https://{env}.api.example.com' } | Select-Object -First 1
        $envServer.description | Should -Be 'Environment-specific endpoint'
        $envServer.variables.env.default | Should -Be 'dev'
        $envServer.variables.env.enum | Should -Contain 'dev'
        $envServer.variables.env.enum | Should -Contain 'staging'
        $envServer.variables.env.enum | Should -Contain 'prod'
        $envServer.variables.env.description | Should -Be 'Deployment environment'
    }

    It 'Check OpenAPI 3.2 Info' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json -AsHashtable

        $info = $json.info
        $info.title | Should -Be 'Document Info API'
        $info.version | Should -Be '1.0.0'
        $info.description | Should -Be 'Shows how to populate document metadata.'

        $info.contact.name | Should -Be 'API Support'
        $info.contact.email | Should -Be 'support@example.com'

        $info.contact.'x-contact-department' | Should -Be 'Developer Relations'
        $info.contact.'x-contact-hours' | Should -Be '9am-5pm PST'
        $info.contact.'x-logo'.url | Should -Be 'https://www.kestrun.dev/assets/kestrun_abstract_transparent.png'
        $info.contact.'x-logo'.altText | Should -Be 'Kestrun logo'

        $info.license.name | Should -Be 'Apache 2.0'
        $info.license.identifier | Should -Be 'Apache-2.0'
        $info.license.ContainsKey('url') | Should -BeFalse

        $servers = $json.servers
        $servers.Count | Should -BeGreaterOrEqual 4

        $serverUrls = $servers | ForEach-Object { $_.url }
        $serverUrls | Should -Contain 'https://{env}.api.example.com'
        $serverUrls | Should -Contain 'https://api.example.com'
        $serverUrls | Should -Contain 'https://staging-api.example.com'
        $serverUrls | Should -Contain '/'

        $envServer = $servers | Where-Object { $_.url -eq 'https://{env}.api.example.com' } | Select-Object -First 1
        $envServer.description | Should -Be 'Environment-specific endpoint'
        $envServer.variables.env.default | Should -Be 'dev'
        $envServer.variables.env.enum | Should -Contain 'dev'
        $envServer.variables.env.enum | Should -Contain 'staging'
        $envServer.variables.env.enum | Should -Contain 'prod'
        $envServer.variables.env.description | Should -Be 'Deployment environment'
    }

    It 'OpenAPI output matches 10.8 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance
    }
}

