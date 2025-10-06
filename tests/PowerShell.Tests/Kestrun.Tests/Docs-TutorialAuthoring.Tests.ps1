param()

Describe 'Tutorial Docs Authoring Compliance' -Tag 'Docs' {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
    $tutorialRoot = Join-Path $repoRoot 'docs/pwsh/tutorial'

    It 'Tutorial root exists' {
        Test-Path $tutorialRoot | Should -BeTrue
    }

    $files = Get-ChildItem -Path $tutorialRoot -Recurse -File -Filter *.md |
        Where-Object { $_.Name -ne 'index.md' } # Skip section index pages

    It 'Has at least one tutorial page' {
        $files.Count | Should -BeGreaterThan 0
    }

    foreach ($file in $files) {
        $rel = $file.FullName.Substring($repoRoot.Path.Length).TrimStart('\\', '/')
        $text = Get-Content -Path $file.FullName -Raw

        Context "File: $rel" {
            It 'Filename matches N.Title.md pattern' {
                ($file.Name -match '^[0-9]+\.[^\\/]+\.md$') | Should -BeTrue
            }

            It 'Has YAML front matter' {
                ($text -match '(?s)^---\s*\n(.*?)\n---\s*') | Should -BeTrue
            }

            $fm = if ($text -match '(?s)^---\s*\n(.*?)\n---\s*') { $Matches[1] } else { '' }
            $title = if ($fm) { ($fm | Select-String -Pattern '^title:\s*(.+)$' -AllMatches).Matches.Groups[1].Value } else { '' }
            $parent = if ($fm) { ($fm | Select-String -Pattern '^parent:\s*(.+)$' -AllMatches).Matches.Groups[1].Value } else { '' }
            $nav = if ($fm) { ($fm | Select-String -Pattern '^nav_order:\s*(.+)$' -AllMatches).Matches.Groups[1].Value } else { '' }

            It 'Front matter has title, parent, nav_order' {
                $title | Should -Not -BeNullOrEmpty
                $parent | Should -Not -BeNullOrEmpty
                $nav | Should -Not -BeNullOrEmpty
            }

            It 'H1 heading matches title' {
                $h1 = ([regex]::Match($text, '^#\s+(.+)$', 'Multiline')).Groups[1].Value
                $h1 | Should -Be $title
            }

            It 'Contains required sections' {
                foreach ($hdr in '## Full source', '## Step-by-step', '## Try it', '## References', '### Previous / Next') {
                    ($text -match [regex]::Escape($hdr)) | Should -BeTrue -Because "Missing section $hdr"
                }
            }

            It 'Includes example script via include directive' {
                ($text -match '\{% include examples/pwsh/.+?\.ps1 %\}') | Should -BeTrue
            }

            It 'Code fences specify a language' {
                # Look for fence start lines with no language token
                ($text -match '(?m)^```\s*$') | Should -BeFalse
            }

            It 'Step-by-step is numbered starting at 1' {
                ($text -match '## Step-by-step\s*\r?\n1\.') | Should -BeTrue
            }
        }
    }
}
