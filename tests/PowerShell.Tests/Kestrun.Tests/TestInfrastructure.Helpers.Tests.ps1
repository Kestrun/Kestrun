[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    $repoRoot = $PSScriptRoot
    for ($index = 0; $index -lt 3; $index++) {
        $repoRoot = Split-Path -Parent -Path $repoRoot
    }

    Import-Module (Join-Path -Path $repoRoot -ChildPath 'Utility' -AdditionalChildPath 'Modules', 'Helper.psm1') -Force
}


Describe 'Get-KestrunTrxFailedSelector' {
    It 'extracts fully qualified failed tests from a trx result' {
        $trxPath = Join-Path -Path $TestDrive -ChildPath 'sample.trx'
        @'
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions>
    <UnitTest name="SampleTests.FailingTest" id="11111111-1111-1111-1111-111111111111">
      <Execution id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" />
      <TestMethod className="Kestrun.Tests.SampleTests" name="FailingTest" codeBase="Kestrun.Tests.dll" adapterTypeName="executor://xunit/VsTestRunner2/netcoreapp" />
    </UnitTest>
    <UnitTest name="SampleTests.PassingTest" id="22222222-2222-2222-2222-222222222222">
      <Execution id="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" />
      <TestMethod className="Kestrun.Tests.SampleTests" name="PassingTest" codeBase="Kestrun.Tests.dll" adapterTypeName="executor://xunit/VsTestRunner2/netcoreapp" />
    </UnitTest>
  </TestDefinitions>
  <Results>
    <UnitTestResult executionId="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" testId="11111111-1111-1111-1111-111111111111" testName="SampleTests.FailingTest" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>expected failure</Message>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult executionId="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" testId="22222222-2222-2222-2222-222222222222" testName="SampleTests.PassingTest" outcome="Passed" />
  </Results>
</TestRun>
'@ | Set-Content -LiteralPath $trxPath -Encoding utf8NoBOM

        $selectors = @(Get-KestrunTrxFailedSelector -TrxPath $trxPath -ProjectPath '.\tests\CSharp.Tests\Kestrun.Tests\Kestrun.Tests.csproj' -Framework 'net10.0' -Label 'Kestrun.Tests')

        $selectors.Count | Should -Be 1
        $selectors[0].ProjectPath | Should -Be '.\tests\CSharp.Tests\Kestrun.Tests\Kestrun.Tests.csproj'
        $selectors[0].Framework | Should -Be 'net10.0'
        $selectors[0].Label | Should -Be 'Kestrun.Tests'
        $selectors[0].DisplayName | Should -Be 'SampleTests.FailingTest'
        $selectors[0].FullyQualifiedName | Should -Be 'Kestrun.Tests.SampleTests.FailingTest'
        $selectors[0].ErrorMessage | Should -Be 'expected failure'
    }
}
