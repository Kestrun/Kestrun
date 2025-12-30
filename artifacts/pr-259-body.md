Fixes #258

## Summary
PowerShell class identity and type visibility can be unreliable across pooled runspaces when OpenAPI component schemas are emitted as PowerShell classes.

This PR switches the OpenAPI component export to generate C# classes and compile them into a deterministic, cached DLL so the same types are loaded consistently across all runspaces.

## What changed
- Exporter now generates C# source and compiles it using Roslyn (`Microsoft.CodeAnalysis`).
- Output assembly path is deterministic: SHA-256 of the generated source **excluding** the generated comment header.
- Cache location (per runtime): `%TEMP%\Kestrun\OpenApiClasses\<netX.Y>\<hash>.dll`.
- If the DLL already exists, compilation is skipped.
- Runspace injection:
  - `.dll` exports are injected via `InitialSessionState.Assemblies`.
  - Legacy `.ps1` remains supported via `InitialSessionState.StartupScripts`.
- Cleanup: cached DLLs are preserved; only legacy temp `.ps1` files are deleted.
- Reliability/concurrency:
  - Scan all loaded assemblies for component types (handles `ReflectionTypeLoadException`).
  - Best-effort load compiled DLL into default `AssemblyLoadContext`.
  - Mutex + temp file + atomic move to avoid races under parallel execution.

## Tests
- `Invoke-Build Restore ; Invoke-Build Build` ✅
- `Invoke-Build Test-xUnit` ✅ (net8.0)
- `Invoke-Build Test-xUnit -Frameworks net10.0` ✅ (net10.0)
- `Invoke-Build Test-Pester` ⏭️ skipped (per request; currently fails immediately after discovery and needs separate triage)

## Checklist
- [x] Branch name follows conventions (`fix-258-openapi-class-identity`)
- [x] Commits use Conventional Commits
- [x] Build completed via Invoke-Build
- [x] xUnit tests passed for net8.0 + net10.0
- [ ] Pester tests (skipped; follow-up needed)
