---
applyTo: "docs/guides/razor.md,docs/pwsh/tutorial/11.razor/**,docs/pwsh/tutorial/21.Localization/2.Razor-Localization.md,docs/_includes/examples/pwsh/11.*.ps1,docs/_includes/examples/pwsh/21.2-Razor-Localization.ps1,docs/_includes/examples/pwsh/Assets/Pages/**,docs/_includes/examples/pwsh/Assets/Pages-Localization/**,tests/PowerShell.Tests/Kestrun.Tests/Tutorial/Tutorial-11*.Tests.ps1,tests/PowerShell.Tests/Kestrun.Tests/Tutorial/Tutorial-21.2-Razor-Localization.Tests.ps1"
---

# PowerShell Razor Instructions

PowerShell Razor changes must follow `.github/copilot-instructions.md` as the global source of truth.
These rules apply when implementing, documenting, or testing Kestrun PowerShell-backed Razor Pages.

## Core Model

- Treat Kestrun Razor Pages as a paired-file pattern:
  - `Page.cshtml` renders markup
  - `Page.cshtml.ps1` prepares request-specific data in PowerShell
- Do not invent unsupported Razor integration behavior, cmdlets, model types, or middleware ordering.
- Verify Razor behavior against the current guide, tutorial examples, page assets, and tutorial tests.
- Keep page rendering behavior and the tutorial/docs description aligned.

## Standard Setup

- For sample servers, prefer:
  - `Initialize-KrRoot -Path $PSScriptRoot` when the example relies on relative Razor asset paths
  - `New-KrServer`
  - `Add-KrEndpoint -Port $Port` or the scenario-specific endpoint setup
  - `Add-KrPowerShellRazorPagesRuntime -RootPath './Assets/Pages'` or the correct scenario-specific pages folder
  - `Enable-KrConfiguration`
  - `Start-KrServer`
- Configure `Add-KrPowerShellRazorPagesRuntime` before `Enable-KrConfiguration`.
- Keep page roots relative to the initialized content root so examples work under the tutorial harness.

## Page And Model Conventions

- Use the sibling naming convention exactly: `Page.cshtml` plus `Page.cshtml.ps1`.
- In PowerShell page scripts, assign `$Model` to provide data for Razor.
- In Razor pages, read PowerShell-provided data from `Model.Data`.
- Preserve the repository’s current model wrapper usage in sample pages:
  - `@model Kestrun.Razor.PwshKestrunModel`
- Keep view logic simple. Put request handling, shaping, and conditional data preparation in the `.cshtml.ps1` file rather than pushing heavy logic into the Razor template.
- Use `$Context` in the page script for request-derived values such as method, path, headers, query data, and forms.
- If a page script short-circuits the response by writing directly to the response, make that explicit and return instead of falling through to Razor rendering.

## View File Structure

- Preserve standard Razor conventions used by the sample site:
  - `_ViewImports.cshtml` for `@using` directives and tag helpers
  - `_ViewStart.cshtml` for the default layout
  - `Shared/_Layout.cshtml` for shared page chrome
- Do not assign a layout inside `Shared/_Layout.cshtml`.
- Keep navigation and shared links compatible with `@Context.Request.PathBase` when the sample or tutorial already follows that pattern.
- Keep `.cshtml` files valid Razor syntax and `.cshtml.ps1` files valid PowerShell scripts.

## PowerShell Razor Authoring Guidance

- Prefer concise page models built from `PSCustomObject` unless a stronger type is already part of the example.
- Keep application-wide state in the main server script only when the sample intentionally demonstrates shared variables across page scripts.
- Use comments sparingly; prefer readable file structure and descriptive variable names.
- Avoid interactive prompts, blocking input, or brittle environment assumptions in page scripts.
- Keep examples runnable and understandable as tutorial source, not pseudocode.

## Antiforgery And Forms

- For unsafe verbs on Razor-backed pages, follow the repository’s antiforgery pattern instead of inventing a custom one.
- Keep the antiforgery cookie, header, and token endpoint aligned with the existing Razor antiforgery example and guide.
- If a page handles POST data, document and test the expected request flow clearly.

## Localization In Razor

- For localized Razor pages, access the per-request localizer from `HttpContext.Items["KrLocalizer"]` or the `KrStrings` alias, matching the repository guide.
- Keep localization middleware ordering compatible with Razor rendering when the sample depends on localized strings.
- Verify that the Razor page, sibling PowerShell script, and localization assets all refer to the same culture keys and folder layout.

## Documentation Guidance

- Keep the Razor guide, tutorial pages, and page-reference docs synchronized with the sample assets they describe.
- When documenting Razor behavior, describe the actual Kestrun model handoff:
  - PowerShell assigns `$Model`
  - Razor reads `Model.Data`
- Do not describe PowerShell Razor Pages as plain ASP.NET Core Razor Pages without the Kestrun-specific PowerShell runtime layer.
- Preserve existing tutorial structure, references, and page navigation unless a refactor is explicitly requested.

## Testing And Verification

- When changing Razor tutorial behavior, update the matching tutorial test under `tests/PowerShell.Tests/Kestrun.Tests/Tutorial/`.
- Prefer the shared tutorial harness:
  - `Start-ExampleScript`
  - `Stop-ExampleScript`
  - `Write-KrExampleInstanceOnFailure`
- Assert observable HTML behavior, request-derived content, redirects, or antiforgery flows instead of internal implementation details.
- Keep tests focused on rendered page behavior and PowerShell-backed model outcomes.
- If the Razor sample uses localization, verify the rendered page content for both default and alternate cultures.

## Change Discipline

- Prefer minimal, reviewable edits over broad rewrites of the sample site.
- Preserve file naming, folder structure, and route expectations used by the existing tutorials and tests.
- Do not modify unrelated example pages or shared assets while addressing a focused Razor task.
- If behavior is uncertain, resolve it from the current sample, guide, or tests before documenting it.

## Completion Notes

When reporting PowerShell Razor work, explain:

- what Razor guide, sample, asset, or test was updated
- what repository sources were used to verify the change
- what rendered behavior or route was validated
- any remaining uncertainty or environment-specific limitation
