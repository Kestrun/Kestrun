---
description: "Use when editing the PowerShell module under src/PowerShell/Kestrun. Covers cmdlet shape, help requirements, compatibility, and C# wrapper patterns."
applyTo: "src/PowerShell/Kestrun/**"
---

# Kestrun PowerShell Module Rules

Use this file for changes to public cmdlets, module wiring, and PowerShell-facing wrappers.

## Cmdlet Shape

- Use approved verbs and existing Kr naming patterns.
- Keep cmdlets pipeline-friendly and predictable.
- Prefer parameter sets that express intent clearly and avoid ambiguous binding.
- Preserve existing public parameter names and behavior unless explicitly changing contract.
- Keep public cmdlets focused on user-facing orchestration, not heavy runtime logic.

## Wrapper Pattern

- Implement runtime behavior in C# core first when introducing new features.
- Keep PowerShell cmdlets as thin wrappers over validated C# capabilities.
- Reuse existing helper cmdlets and shared patterns before adding new one-off helpers.
- Maintain parity between C# behavior and PowerShell surface semantics.

## Help And Discoverability

- Add comment-based help for public functions and cmdlets.
- Keep synopsis and examples aligned with actual behavior.
- Update related docs when user-visible behavior changes.

## Compatibility And Runtime Expectations

- Keep compatibility with supported PowerShell and framework targets used by the module.
- Avoid patterns that depend on interactive prompts or environment-specific state.
- Use deterministic defaults suitable for local runs and CI.
- Preserve fluent chaining patterns used across module examples.

## Validation And Tests

- Validate module-impacting changes with canonical flow:
  - Invoke-Build Restore ; Invoke-Build Build
  - Invoke-Build Test
- Add or update Pester tests under tests/PowerShell.Tests for changed cmdlet behavior.
- For cross-surface features, ensure corresponding C# tests are updated when needed.
- Prefer focused Invoke-Pester runs while iterating, then run broader suites as needed.

## Documentation And References

- For cmdlet and module contribution standards, use [CONTRIBUTING.md](../../CONTRIBUTING.md).
- For canonical build orchestration and sync tasks, use [Kestrun.build.ps1](../../Kestrun.build.ps1).
- For feature-specific implementation details and patterns, use [.github/copilot-instructions.md](../copilot-instructions.md).
