---
layout: default
title: Testing
parent: Contributing
nav_order: 2
---

# Testing

Kestrun uses both xUnit (C#) and Pester (PowerShell). This guide shows how to execute and extend tests.

## Test Layout

| Path | Purpose |
|------|---------|
| `tests/CSharp.Tests` | xUnit test project (multi-target) |
| `tests/PowerShell.Tests` | Pester tests for module + scripts |

## Running All Tests

```powershell
Invoke-Build Test
```

Equivalent to running `Invoke-Build Kestrun.Tests` (C#) then `Invoke-Build Test-Pester`.

## Running Only C# Tests

```powershell
Invoke-Build Kestrun.Tests
```

## Running Only PowerShell Tests

```powershell
Invoke-Build Test-Pester
```

## Filtering C# Tests

Use standard xUnit filters via `dotnet test` directly:

```powershell
dotnet test .\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj -t --list-tests
```

Example trait/class filter:

```powershell
dotnet test .\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj -f net9.0 --filter "FullyQualifiedName~Routing"
```

## Pester Configuration

The build script dynamically constructs a `[PesterConfiguration]`. Tags are excluded by platform:

| Platform | Excluded Tag |
|----------|--------------|
| Windows | `Exclude_Windows` |
| Linux | `Exclude_Linux` |
| macOS | `Exclude_MacOs` |

Add tags to tests to skip on specific OSes.

## Adding New C# Tests

1. Add a file under `tests/CSharp.Tests/...`.
2. Use clear naming: `ClassName_Scenario_Expected.cs`.
3. Use `Fact` or `Theory` appropriately.
4. Keep tests deterministic (no external network unless mocked).

## Adding New PowerShell Tests

1. Place `*.Tests.ps1` under `tests/PowerShell.Tests`.
2. Structure with `Describe` / `Context` / `It` blocks.
3. Use `BeforeAll` for expensive setup; `BeforeEach` for per-test state.
4. Assert with `Should -Be`, `Should -Match`, etc.

## Coverage

```powershell
Invoke-Build Coverage        # Collect raw coverage
Invoke-Build Report-Coverage # Generate HTML + badges
```

Generates Cobertura + HTML in `coverage/`.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Platform-specific failures | Add appropriate `Exclude_*` tag |
| Assembly load errors | Ensure build succeeded before test |
| Pester module not found | `Install-PSResource Pester -Scope CurrentUser` |
| Coverage empty | Ensure tests exercised relevant code paths |

---
Last updated: {{ site.time | date: '%Y-%m-%d' }}
