param()
Describe 'Example 9.2-Structured-Xml-Yaml-Csv' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.2-Structured-Xml-Yaml-Csv.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'XML/YAML/CSV routes expose structured data' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $p = $script:instance.Port
        # XML root element presence
        $xml = Invoke-WebRequest -Uri "http://127.0.0.1:$p/xml" -UseBasicParsing -TimeoutSec 8
        $xml.StatusCode | Should -Be 200
        $xml.Content | Should -Match '<Id>1</Id>'
        # YAML env key (normalization logic in helper tested indirectly)
        $yaml = Invoke-WebRequest -Uri "http://127.0.0.1:$p/yaml" -UseBasicParsing -TimeoutSec 8
        $yaml.StatusCode | Should -Be 200
        Assert-YamlContainsKeyValue -Yaml $yaml.Content -Key env -Expected dev
        # CSV header validation
        $csv = Invoke-WebRequest -Uri "http://127.0.0.1:$p/csv" -UseBasicParsing -TimeoutSec 8
        $csv.StatusCode | Should -Be 200
        # CSV header is lower-case and includes three columns
        ($csv.Content -split "`n")[0] | Should -Match 'id,name,score'
    }
}
