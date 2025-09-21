param()
Describe 'Example 9.2-Structured-Xml-Yaml-Csv' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.2-Structured-Xml-Yaml-Csv.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'XML routes expose structured data' {
        # XML root element presence
        $xml = Invoke-WebRequest -Uri "$($script:instance.Url)/xml" -UseBasicParsing -TimeoutSec 8
        $xml.StatusCode | Should -Be 200
        $xml.Content | Should -Match '<Id>1</Id>'
    }
    It 'YAML routes expose structured data' {
        # YAML env key (normalization logic in helper tested indirectly)
        $yaml = Invoke-WebRequest -Uri "$($script:instance.Url)/yaml" -UseBasicParsing -TimeoutSec 8
        $yaml.StatusCode | Should -Be 200
        Assert-YamlContainsKeyValue -Yaml $yaml.Content -Key env -Expected dev
    }
    It 'CSV route exposes structured data' {
        # CSV header validation
        $csv = Invoke-WebRequest -Uri "$($script:instance.Url)/csv" -UseBasicParsing -TimeoutSec 8
        $csv.StatusCode | Should -Be 200
        # CSV header is lower-case and includes three columns
        $headers = (ConvertFrom-Csv $csv.Content | Get-Member -MemberType NoteProperty).Name
        $headers | Should -Contain 'Square'
        $headers | Should -Contain 'Id'
    }
}
