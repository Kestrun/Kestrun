---
layout: default
title: Contributing
tagline: How to build, test, document, and submit changes
nav_order: 90
has_children: true
---

# Contributing to Kestrun

Welcome! This section gathers everything you need to build, test, document, and contribute improvements.
If you are in a hurry, start with the Quick Start below.

## 📌 Quick Start

```powershell
# Install prerequisites (PowerShell 7.4+, .NET SDK 8 & 9)
Install-PSResource -Name 'InvokeBuild','Pester' -Scope CurrentUser

# Restore, build, test
Invoke-Build Restore
Invoke-Build Build
Invoke-Build Test
```

## Sections

| Topic | Description |
|-------|-------------|
| Build Pipeline | Core build commands and configuration knobs |
| Testing | Running C# (xUnit) + PowerShell (Pester) tests |
| Style & Quality | Coding conventions (C# + PowerShell) |
| Logging & Output | Console / emoji style conventions |
| Documentation | Just-the-Docs rules and structure |
| Git Workflow | Branching, commits, pull requests |
| Troubleshooting | Common issues & fixes |

Use the navigation sidebar to explore each topic.

---
Last updated: {{ site.time | date: '%Y-%m-%d' }}
