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

### Tutorial Example Tests (Kestrun Docs)

The PowerShell tutorial examples in `docs/_includes/examples/pwsh` each have a corresponding smoke/behavior test under:

`tests/PowerShell.Tests/Kestrun.Tests/Tutorial`

Helper script: `TutorialExampleTestHelper.ps1` centralizes lifecycle
(dynamic port allocation, start/stop, route probing) and assertion utilities
(`Invoke-ExampleRequest`, `Assert-JsonFieldValue`, `Assert-YamlContainsKeyValue`).

#### Adding a New Tutorial Test

1. Copy an existing pattern (e.g. `2.3-Route-Parameters.Tests.ps1`).
2. In `BeforeAll`, dot-source the helper and call:
   `Start-ExampleScript -Name '<script>.ps1'`.
3. Use either `Test-ExampleRouteSet -Instance $script:instance` for generic
   200 checks or explicit assertions via `Invoke-ExampleRequest` for richer
   validation (content-type, body fields, etc.).
4. In `AfterAll`, stop the script with
   `Stop-ExampleScript -Instance $script:instance`.
5. Keep the test name aligned with the example script (same numeric prefix)
   for discoverability.

#### Skipped / Limited Scenarios

Some examples are intentionally only smoke-tested or skipped (marked with `It -Skip` or minimal assertions) due to environment or complexity:

| Category | Reason |
|----------|--------|
| Authentication (JWT, API Key, Cookies, Windows, Multiple Schemes) | Requires token/key issuance or OS-specific configuration not suitable for fast CI. |
| Certificates (self-signed, CSR, import/export) | Involves crypto operations, certificate store access, or external tooling. |
| HTTPS / Named Pipes / Unix Sockets / Mixed Protocols | Platform or privilege sensitive; covered by dedicated lower-level tests elsewhere. |
| Antiforgery workflows | Multi-step token issuance + header/cookie coordination increases brittleness for tutorial smoke coverage. |
| Full demo / large integration scripts | High startup and execution cost; kept as documentation examples rather than regression tests. |

Where possible, prefer adding focused unit/integration tests in module-level suites rather than overloading tutorial tests with advanced setup.

#### Assertion Levels

Choose the lightest required validation:

1. Smoke: ensure routes return 200 (`Test-ExampleRouteSet`).
2. Format: assert content-type and key body fields (see `2.1-Multiple-Content-Types`).
3. Behavior: multi-verb or stateful assertions (see `2.3-Route-Parameters`).

#### YAML Normalization Note

Some YAML responses may surface as numeric code-point lines (implementation detail of streaming writer). The helper normalizes these to text before assertions.

#### Troubleshooting Tutorial Tests

| Symptom | Suggested Action |
|---------|------------------|
| Process exits early | Re-run with `$VerbosePreference='Continue'`; check captured stderr via helper warning output. |
| Port refused | Increase readiness polling or verify `Add-KrListener` was rewritten correctly (dynamic port). |
| YAML assertion fails | Confirm normalization; dump raw content with a one-off diagnostic script. |

#### Fast Local Run

Run a single example test:

```powershell
Invoke-Pester tests/PowerShell.Tests/Kestrun.Tests/Tutorial/2.1-Multiple-Content-Types.Tests.ps1
```

Run all tutorial tests (may take a few minutes):

```powershell
Invoke-Pester tests/PowerShell.Tests/Kestrun.Tests/Tutorial
```

#### Tag Glossary & Selective Runs

Tutorial and related Pester tests use a small set of category tags to enable
fast vs. focused execution in CI and during local development:

| Tag | Meaning / Scope | Typical Use |
|-----|-----------------|-------------|
| `Tutorial` | Any test targeting a documentation tutorial example. | Bulk include / filtering; general categorization. |
| `Redirects` | Redirect behavior (3xx) examples. | Narrow reruns when editing redirect logic. |
| `Errors` | Error handling / status code examples. | Targeted changes to error pages or exception pipeline. |
| `Caching` | Response/time-based caching semantics. | Changes to caching middleware/headers. |
| `Logging` | Log enrichment / structured logging features. | Logging pipeline modifications. |
| `Correlation` | Correlation / trace / request ID propagation. | Diagnostic context tracing updates. |
| `Slow` | Startup or multi‑probe tests that take noticeably longer (e.g. >10s) and are split into a separate CI job. | Excluded from the primary fast test pass; run in a dedicated job or on demand locally. |

Fast subset (exclude `Slow`):

```powershell
Invoke-Pester tests/PowerShell.Tests/Kestrun.Tests/Tutorial -ExcludeTag Slow
```

Slow examples only:

```powershell
Invoke-Pester tests/PowerShell.Tests/Kestrun.Tests/Tutorial -Tag Slow
```

All tutorial tests (fast + slow):

```powershell
Invoke-Pester tests/PowerShell.Tests/Kestrun.Tests/Tutorial
```

CI Note: The core CI job runs the fast subset (excluding `Slow`). A separate
job executes just the slow tests so regressions surface without extending
the critical path for most contributions.

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
