---
applyTo: "docs/_includes/examples/pwsh/**"
---

# Tutorial Sample Instructions

Tutorial sample changes must follow `.github/copilot-instructions.md` as the global source of truth.
These rules apply when creating or editing PowerShell tutorial sample files under `docs/_includes/examples/pwsh/`.

## Purpose

- Treat each sample as runnable repository source, not pseudocode.
- Keep samples aligned with the tutorial page that embeds them and the Pester test that exercises them.
- Prefer one clear teaching goal per sample.

## File And Naming Conventions

- Place new PowerShell tutorial samples in `docs/_includes/examples/pwsh/`.
- Use the existing numeric naming pattern: `N.M-Topic.ps1` or the established section-specific variant already used nearby.
- Match the script name to the corresponding tutorial page and tutorial test numeric prefix.
- Put supporting sample assets under the existing example tree when needed, typically under `docs/_includes/examples/pwsh/Assets/`.

## Authoring Standards

- Do not invent cmdlets, routes, middleware, parameters, defaults, or behavior.
- Verify sample behavior against the current PowerShell module, C# implementation, existing examples, or tests.
- Keep scripts concise and instructional, but fully executable.
- Prefer the repository’s PowerShell cmdlet style and terminology already used in nearby samples.
- Preserve the surrounding style of the section you are editing rather than reformatting older examples wholesale.
- Use comments sparingly and only where they improve tutorial readability.

## Script Shape

- Prefer a runnable script with an explicit port parameter:
  `param([int]$Port = $env:PORT ?? 5000)`
- Build a complete startup flow when the sample hosts a server:
  - optional logging setup when the scenario benefits from visible logs
  - `New-KrServer`
  - `Add-KrEndpoint -Port $Port` or the scenario-specific listener setup
  - middleware, configuration, routes, or feature registration
  - `Enable-KrConfiguration` when required by the sample pattern
  - `Start-KrServer`
- Keep localhost-safe defaults unless the tutorial specifically demonstrates another binding mode.
- Avoid interactive prompts, manual pauses, or environment-specific assumptions unless the tutorial is explicitly about that scenario.

## Test And Harness Compatibility

- Samples should remain runnable under the shared Pester tutorial harness in `tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1`.
- Do not hard-code ports in a way that prevents `Start-ExampleScript` from rewriting or supplying the listener port.
- Prefer relative paths and startup behavior that work when the harness launches the sample from its script directory.
- For package-ready service samples, assume the script may run from staged `Application/` content with Kestrun already preloaded by the host; do not require repo-root discovery or repo-relative module imports to start.
- If a package-ready sample keeps local-development import convenience, that logic must stay optional and must not fail startup when the repository tree is absent.
- If a sample requires external services, credentials, certificates, or platform-specific features, keep the core example explicit and document the limitation clearly in the matching tutorial/test.
- Keep examples deterministic enough for smoke or behavior testing when practical.

## Tutorial And Test Alignment

- When adding a new tutorial sample, also add or update:
  - the matching tutorial page under `docs/pwsh/tutorial/`
  - the matching Pester tutorial test under `tests/PowerShell.Tests/Kestrun.Tests/Tutorial/`
- Ensure the tutorial page references the sample using the repository’s existing `pwsh/tutorial/examples/...` link style.
- Keep the tutorial narrative, embedded sample, and test expectations synchronized when behavior changes.
- If sample conventions change, update `docs/contributing/tutorial-template.md` or `docs/contributing/testing.md` when those guides are affected.

## Feature-Specific Guidance

- For OpenAPI samples, follow the repository’s current OpenAPI attribute and component conventions from `.github/copilot-instructions.md`.
- For multipart, uploads, SSE, SignalR, localization, authentication, or other advanced areas, verify limits, middleware, and helper usage from nearby examples and tests before introducing new patterns.
- Prefer existing helper functions, route patterns, and response helpers over custom one-off implementations when the repository already has a standard approach.

## Change Discipline

- Prefer minimal, reviewable edits over broad sample rewrites.
- Do not modify production code when the request is only to add or adjust tutorial samples unless explicitly asked.
- If sample behavior is uncertain, omit the claim or mark the uncertainty instead of guessing.

## Completion Notes

When reporting tutorial sample work, explain:

- what sample was created or updated
- what code, tests, or neighboring samples were used to verify it
- whether the matching tutorial page and tutorial test were updated
- any remaining environment-specific limitations or uncertainty
