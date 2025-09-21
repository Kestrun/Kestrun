param()
Describe 'Example 9.4-Html-Files' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.4-Html-Files.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'HTML/template/download routes behave as expected' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $p = $script:instance.Port
    # /page -> must succeed and contain expected static about page heading
        $page = Invoke-WebRequest -Uri "$($script:instance.Url)/page" -UseBasicParsing -TimeoutSec 8
        $page.StatusCode | Should -Be 200
    ($page.Content -like '*About This Site*') | Should -BeTrue -Because 'Static about page should be served'
        # /inline -> must succeed and render template
        $inline = Invoke-WebRequest -Uri "$($script:instance.Url)/inline" -UseBasicParsing -TimeoutSec 8
        $inline.StatusCode | Should -Be 200
        ($inline.Content -like '*<h1>Inline</h1>*') | Should -BeTrue
        # /download -> must succeed and include attachment header
        $dl = Invoke-WebRequest -Uri "$($script:instance.Url)/download" -UseBasicParsing -TimeoutSec 8
        $dl.StatusCode | Should -Be 200
        (($dl.Headers.Keys | Where-Object { $_ -match 'Content-Disposition' } | ForEach-Object { $dl.Headers[$_] }) -join ';') | Should -Match 'attachment;'
    }
}
