---
description: "Use when shipping a C#-only Kestrun change with focused, fast validation before broader checks."
name: "Ship CSharp Fast Validate"
argument-hint: "Describe the C# change and expected behavior."
agent: "agent"
---

Implement the requested C# change and validate it with the fastest reliable workflow for Kestrun.

Requirements:

1. Confirm scope is C# only. If the change impacts PowerShell module behavior or shared assets, switch to the full workflow.
2. Read relevant workspace instructions and existing C# patterns before editing.
3. Make the smallest safe change needed.
4. Use focused validation while iterating:
   - dotnet test on relevant C# test projects or targeted tests.
5. Before final report:
   - If change touches src/CSharp/Kestrun.Tool only: run Invoke-Build Build-KestrunTool.
   - If change touches core library under src/CSharp/Kestrun: run Invoke-Build Restore ; Invoke-Build Build, then run relevant tests.
6. Add or update xUnit tests for behavior changes.
7. Do not revert unrelated local changes.

Output format:

- Summary: what changed and why.
- Scope Check: why C# fast path was appropriate.
- Files: edited file list.
- Validation: commands run and pass or fail outcomes.
- Follow-ups: only if needed.
