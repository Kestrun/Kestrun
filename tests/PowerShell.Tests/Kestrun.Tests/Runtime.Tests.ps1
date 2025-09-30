param()
# Runtime feature / version tests for Kestrun PowerShell surface
# Mirrors C# unit tests for KestrunRuntimeInfo

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Runtime Version Information' {
    It 'Get-KrBuiltTargetFrameworkVersion returns major >= 8' {
        $ver = Get-KrBuiltTargetFrameworkVersion
        $ver | Should -Not -BeNullOrEmpty
        $ver.Major | Should -BeGreaterOrEqual 8
    }

    It 'Get-KrFeatureSupport returns objects for KnownFeature enum' {
        $rows = Get-KrFeatureSupport | Where-Object { $_.Feature -eq 'Http3' }
        $rows | Should -Not -BeNullOrEmpty
        $rows[0].BuiltTFM.Major | Should -BeGreaterOrEqual 8
        $rows[0].PSObject.Properties.Name | Should -Contain 'Supported'
    }

    It 'SuppressReadingTokenFromFormBody capability gating behaves by TFM' {
        $rows = Get-KrFeatureSupport | Where-Object { $_.Feature -eq 'SuppressReadingTokenFromFormBody' }
        $rows | Should -Not -BeNullOrEmpty
        # Built TFM could be 8 or 9; support only when >= 9
        $tfm = $rows[0].BuiltTFM
        if ($tfm.Major -ge 9) {
            $rows[0].Supported | Should -BeTrue
        } else {
            $rows[0].Supported | Should -BeFalse
        }
    }

    It 'Get-KrFeatureSupport -Capabilities lists features and support flag' {
        $caps = Get-KrFeatureSupport -Capabilities | Sort-Object Feature
        $caps | Should -Not -BeNullOrEmpty
        ($caps | Get-Member -Name Feature) | Should -Not -BeNullOrEmpty
        ($caps | Get-Member -Name Supported) | Should -Not -BeNullOrEmpty
        ($caps | Where-Object Feature -EQ 'Http3') | Should -Not -BeNullOrEmpty
    }
}
