param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.13 OpenAPI Examples' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.13-OpenAPI-Examples.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Buy ticket (POST) returns 201' {
        $body = @{
            ticketType = 'general'
            ticketDate = '2023-09-07'
            email = 'todd@example.com'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/tickets" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201

        $json = $result.Content | ConvertFrom-Json
        $json.ticketType | Should -Be 'general'
        $json.ticketDate | Should -Be '2023-09-07'
        $json.ticketId | Should -Not -BeNullOrEmpty
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
        $doc.components.examples.BuyGeneralTicketsResponseExample | Should -Not -BeNullOrEmpty
        $doc.components.examples.GetMuseumHoursResponseExample | Should -Not -BeNullOrEmpty

        # Request example ref on POST /tickets
        $postTicket = $doc.paths.'/tickets'.post
        $reqRef = $postTicket.requestBody.content.'application/json'.examples.general_entry.'$ref'
        $reqRef | Should -Be '#/components/examples/BuyGeneralTicketsRequestExample'

        # Response example ref on POST /tickets (201)
        $respRef = $postTicket.responses.'201'.content.'application/json'.examples.general_entry.'$ref'
        $respRef | Should -Be '#/components/examples/BuyGeneralTicketsResponseExample'

        # Response example ref on GET /museum-hours (200)
        $hoursRef = $doc.paths.'/museum-hours'.get.responses.'200'.content.'application/json'.examples.default_example.'$ref'
        $hoursRef | Should -Be '#/components/examples/GetMuseumHoursResponseExample'
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
}
