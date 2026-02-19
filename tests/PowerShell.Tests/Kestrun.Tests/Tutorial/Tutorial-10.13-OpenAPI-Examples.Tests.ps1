param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.13 OpenAPI Examples' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.13-OpenAPI-Examples.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Buy ticket (POST) accepts multiple request content types' -TestCases @(
        @{
            ContentType = 'application/json'
            Body = (@{
                    ticketType = 'general'
                    ticketDate = '2023-09-07'
                    email = 'todd@example.com'
                } | ConvertTo-Json)
        }
        @{
            ContentType = 'application/x-www-form-urlencoded'
            Body = 'ticketType=general&ticketDate=2023-09-07&email=todd%40example.com'
        }
        @{
            ContentType = 'application/xml'
            Body = @'
<BuyTicketRequest>
  <ticketType>general</ticketType>
  <ticketDate>2023-09-07</ticketDate>
  <email>todd@example.com</email>
</BuyTicketRequest>
'@
        }
        @{
            ContentType = 'application/yaml'
            Body = @'
ticketType: general
ticketDate: 2023-09-07
email: todd@example.com
'@
        }
    ) {
        param($ContentType, $Body)

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/tickets" -Method Post -Body $Body -ContentType $ContentType -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201

        # Default response should be JSON (no Accept header set).
        $json = $result.Content | ConvertFrom-Json
        $json.ticketType | Should -Be 'general'
        $json.ticketDate | Should -Be '2023-09-07'
        $json.ticketId | Should -Not -BeNullOrEmpty
    }

    It 'Buy ticket (POST) responds with requested content type' -TestCases @(
        @{ Accept = 'application/json' }
        @{ Accept = 'application/xml' }
        @{ Accept = 'application/yaml' }
        @{ Accept = 'application/x-www-form-urlencoded' }
    ) {
        param($Accept)

        # Send a JSON body (request parsing is covered by the request Content-Type test).
        $body = (@{
                ticketType = 'general'
                ticketDate = '2023-09-07'
                email = 'todd@example.com'
            } | ConvertTo-Json)

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/tickets" -Method Post -Body $body -ContentType 'application/json' -Headers @{ Accept = $Accept } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201

        # Content-Type can include charset, so match on prefix.
        $result.Headers.'Content-Type' | Should -Match "^$([regex]::Escape($Accept))"

        # For some content types, Invoke-WebRequest returns a byte/int array in .Content.
        $contentText = if ($result.Content -is [string]) {
            $result.Content
        } elseif ($result.Content -is [System.Array]) {
            [System.Text.Encoding]::UTF8.GetString([byte[]]$result.Content)
        } else {
            $result.Content.ToString()
        }

        switch ($Accept) {
            'application/json' {
                $json = $contentText | ConvertFrom-Json
                $json.ticketType | Should -Be 'general'
                $json.ticketDate | Should -Be '2023-09-07'
                $json.ticketId | Should -Not -BeNullOrEmpty
                $json.message | Should -Be 'Museum general entry ticket purchased'
            }
            'application/xml' {
                [xml]$xml = $contentText
                ($xml.SelectSingleNode('//*[local-name()="ticketType"]').InnerText) | Should -Be 'general'
                ($xml.SelectSingleNode('//*[local-name()="ticketDate"]').InnerText) | Should -Be '2023-09-07'
                ($xml.SelectSingleNode('//*[local-name()="ticketId"]').InnerText) | Should -Not -BeNullOrEmpty
                ($xml.SelectSingleNode('//*[local-name()="message"]').InnerText) | Should -Be 'Museum general entry ticket purchased'
            }
            'application/yaml' {
                $yaml = $contentText | ConvertFrom-KrYaml
                $yaml.ticketType | Should -Be 'general'
                $yaml.ticketDate | Should -Be '2023-09-07'
                $yaml.ticketId | Should -Not -BeNullOrEmpty
                $yaml.message | Should -Be 'Museum general entry ticket purchased'
            }
            'application/x-www-form-urlencoded' {
                $pairs = [Microsoft.AspNetCore.WebUtilities.QueryHelpers]::ParseQuery($contentText)

                $pairs.ticketType | Should -Be 'general'
                $pairs.ticketDate | Should -Be '2023-09-07'
                $pairs.ticketId | Should -Not -BeNullOrEmpty
                $pairs.message | Should -Be 'Museum general entry ticket purchased'
            }
        }
    }

    It 'Get museum hours (GET) returns 200' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/museum-hours" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $json = $result.Content | ConvertFrom-Json
        $json.Count | Should -BeGreaterThan 0
        $json[0].date | Should -Not -BeNullOrEmpty
    }

    It 'Search tickets supports inline parameter examples' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/tickets/search?ticketDate=2023-09-07" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $json = $result.Content | ConvertFrom-Json
        $json.ok | Should -BeTrue
    }

    It 'OpenAPI includes component examples and example refs' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $doc = $result.Content | ConvertFrom-Json

        # Component examples exist
        $doc.components.examples.BuyGeneralTicketsRequestExample | Should -Not -BeNullOrEmpty
        $doc.components.examples.BuyGeneralTicketsRequestDataValueExample | Should -Not -BeNullOrEmpty
        $doc.components.examples.BuyGeneralTicketsResponseExample | Should -Not -BeNullOrEmpty
        $doc.components.examples.GetMuseumHoursResponseExample | Should -Not -BeNullOrEmpty
        $doc.components.examples.GetMuseumHoursResponseExternalExample | Should -Not -BeNullOrEmpty

        # Vendor extensions are present (example components)
        $doc.components.examples.BuyGeneralTicketsRequestExample.'x-kestrun-demo'.scenario | Should -Be 'buy-ticket'
        $doc.components.examples.BuyGeneralTicketsResponseExample.'x-kestrun-demo'.statusCode | Should -Be 201

        # OpenAPI 3.1: dataValue/serializedValue are emitted as x-oai-* extensions
        $doc.components.examples.BuyGeneralTicketsRequestDataValueExample.PSObject.Properties['x-oai-dataValue'] | Should -Not -BeNullOrEmpty
        $doc.components.examples.BuyGeneralTicketsRequestDataValueExample.PSObject.Properties['x-oai-serializedValue'] | Should -Not -BeNullOrEmpty

        # External examples use externalValue
        $doc.components.examples.GetMuseumHoursResponseExternalExample.externalValue | Should -Be 'https://example.com/openapi/examples/museum-hours.json'

        $postTicket = $doc.paths.'/tickets'.post

        $contentTypes = @(
            'application/json'
            'application/xml'
            'application/x-www-form-urlencoded'
            'application/yaml'
        )

        foreach ($ct in $contentTypes) {
            # Request content types exist
            $postTicket.requestBody.content.$ct | Should -Not -BeNullOrEmpty

            # Request example ref exists for each content type
            $postTicket.requestBody.content.$ct.examples.general_entry.'$ref' | Should -Be '#/components/examples/BuyGeneralTicketsRequestExample'
            $postTicket.requestBody.content.$ct.examples.general_entry_dataValue.'$ref' | Should -Be '#/components/examples/BuyGeneralTicketsRequestDataValueExample'

            # Response content types exist
            $postTicket.responses.'201'.content.$ct | Should -Not -BeNullOrEmpty

            # Response example ref exists for each content type
            $postTicket.responses.'201'.content.$ct.examples.general_entry.'$ref' | Should -Be '#/components/examples/BuyGeneralTicketsResponseExample'
        }

        # Response example ref on GET /museum-hours (200)
        $hoursDefaultRef = $doc.paths.'/museum-hours'.get.responses.'200'.content.'application/json'.examples.default_example.'$ref'
        $hoursDefaultRef | Should -Be '#/components/examples/GetMuseumHoursResponseExample'

        $hoursExternalRef = $doc.paths.'/museum-hours'.get.responses.'200'.content.'application/json'.examples.external_example.'$ref'
        $hoursExternalRef | Should -Be '#/components/examples/GetMuseumHoursResponseExternalExample'
    }

    It 'OpenAPI inlines parameter examples for ticketDate (no $ref)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $doc = $result.Content | ConvertFrom-Json

        $params = $doc.paths.'/tickets/search'.get.parameters
        $ticketDateParam = $params | Where-Object { $_.name -eq 'ticketDate' }
        $ticketDateParam | Should -Not -BeNullOrEmpty

        $ticketDateParam.examples.today.value | Should -Not -BeNullOrEmpty
        $ticketDateParam.examples.nextSaturday.value | Should -Not -BeNullOrEmpty
        $ticketDateParam.examples.nextSunday.value | Should -Not -BeNullOrEmpty

        # Inline examples should not use $ref (these are stored in Kestrun inline store, not components/examples)
        $ticketDateParam.examples.today.PSObject.Properties['$ref'] | Should -BeNullOrEmpty
        $ticketDateParam.examples.nextSaturday.PSObject.Properties['$ref'] | Should -BeNullOrEmpty
        $ticketDateParam.examples.nextSunday.PSObject.Properties['$ref'] | Should -BeNullOrEmpty

        $doc.components.examples.PSObject.Properties['TodayParameter'] | Should -BeNullOrEmpty
        $doc.components.examples.PSObject.Properties['NextSaturdayParameter'] | Should -BeNullOrEmpty
        $doc.components.examples.PSObject.Properties['NextSundayParameter'] | Should -BeNullOrEmpty
    }

    It 'OpenAPI v3.0 output matches 10.13 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches 10.13 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches 10.13 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}
