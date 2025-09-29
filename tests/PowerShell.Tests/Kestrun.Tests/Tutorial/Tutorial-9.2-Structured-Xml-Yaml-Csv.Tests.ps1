param()
Describe 'Example 9.2-Structured-Xml-Yaml-Csv' {
    BeforeAll {. (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '9.2-Structured-Xml-Yaml-Csv.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
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
        $yamlContent = [string]::new($yaml.Content) | ConvertFrom-KrYaml
        $yamlContent | Should -Not -BeNullOrEmpty
        $yamlContent.Count | Should -Be 2
        $yamlContent[0].tags.Count | Should -Be 2
        $yamlContent[0].enabled | Should -BeTrue
        $yamlContent[0].env | Should -Be 'dev'
        $yamlContent[1].Id | Should -Be 1
        $yamlContent[1].Name | Should -Be 'Alpha'
        $yamlContent[1].Nested.Count | Should -Be 2
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
