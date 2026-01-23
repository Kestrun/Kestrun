param()

Describe 'Tutorial 21.1 - Localization' -Tag 'Tutorial', 'Localization' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '21.1-Localization.ps1' -StartupTimeoutSeconds 40
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'returns Italian strings via query culture' {
        $url = "$($script:instance.Url)/hello?lang=it-IT"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'it-IT'
        $resp.hello | Should -Be 'Ciao'
        $resp.save | Should -Be 'Salva'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }

    It 'defaults to en-US when no culture provided' {
        $url = "$($script:instance.Url)/hello"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'en-US'
        $resp.hello | Should -Be 'Hello'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }

    It 'returns French strings via query culture' {
        $url = "$($script:instance.Url)/hello?lang=fr-FR"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'fr-FR'
        $resp.hello | Should -Be 'Bonjour'
        $resp.save | Should -Be 'Enregistrer'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }

    It 'uses Accept-Language header when present' {
        $url = "$($script:instance.Url)/hello"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json'; 'Accept-Language' = 'es-ES' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'es-ES'
        $resp.hello | Should -Be 'Hola'
        $resp.save | Should -Be 'Guardar'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }

    It 'uses cookie when present' {
        $url = "$($script:instance.Url)/hello"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json'; Cookie = 'lang=de-DE' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'de-DE'
        $resp.hello | Should -Be 'Hallo'
        $resp.save | Should -Be 'Speichern'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }

    It 'returns Swiss Italian strings via query culture' {
        $url = "$($script:instance.Url)/hello?lang=it-CH"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'it-CH'
        $resp.hello | Should -Be 'Ciao'
        $resp.save | Should -Be 'Salva'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }

      It 'returns Canadian French strings via Accept-Language header' {
        $url = "$($script:instance.Url)/hello"
        $params = @{ Uri = $url; TimeoutSec = 10; Headers = @{ Accept = 'application/json'; 'Accept-Language' = 'fr-CA' } }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $resp = Invoke-RestMethod @params
        $resp.culture | Should -Be 'fr-CA'
        $resp.hello | Should -Be 'Bonjour du Canada !'
        $resp.save | Should -Be 'Enregistrer'
        $ci = [System.Globalization.CultureInfo]::new($resp.culture)
        $expectedDate = (Get-Date).ToString('D', $ci)
        $expectedCurrency = (1234.56).ToString('C', $ci)
        $resp.dateSample | Should -Be $expectedDate
        $resp.currencySample | Should -Be $expectedCurrency
        $expectedCalendar = $ci.Calendar.GetType().Name
        $resp.calendarName | Should -Be $expectedCalendar
    }
}
