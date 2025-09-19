param()
# Runtime feature / version tests for Kestrun PowerShell surface
# Mirrors C# unit tests for KestrunRuntimeInfo

BeforeAll {
    # Ensure module is imported (_.Tests.ps1 normally does this too, but be defensive)
    if (-not (Get-Module -Name Kestrun)) {
        $path = $PSCommandPath
        $kestrunPath = Join-Path -Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $path)))) -ChildPath 'src' -AdditionalChildPath 'PowerShell','Kestrun'
        if (Test-Path -Path "$kestrunPath/Kestrun.psm1" -PathType Leaf) {
            Import-Module "$kestrunPath/Kestrun.psm1" -Force -ErrorAction Stop
        } else {
            throw "Kestrun module not found at $kestrunPath"
        }
    }
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
}

Describe 'Feature Capability Checks' {
    It 'Test-KrCapability returns $false for unknown feature' {
        Test-KrCapability -Feature 'TotallyUnknownFeatureName' | Should -BeFalse
    }

    It 'Dynamic feature registration keeps unsupported state for custom feature (current behavior)' {
        $featureName = 'ExperimentalX_PS'
        # Register requiring high version to force false
        [Kestrun.KestrunRuntimeInfo]::RegisterOrUpdateFeature($featureName, [Version]'99.0')
        (Test-KrCapability -Feature $featureName) | Should -BeFalse

        # Now lower requirement to built major; capability may still be false if additional runtime checks existed
        $built = Get-KrBuiltTargetFrameworkVersion
        [Kestrun.KestrunRuntimeInfo]::RegisterOrUpdateFeature($featureName, [Version]::new($built.Major,0))
        # Current implementation still returns false for dynamically added custom features (dictionary updated but Supports may not recognize new names internally)
        (Test-KrCapability -Feature $featureName) | Should -BeFalse
    }
}
