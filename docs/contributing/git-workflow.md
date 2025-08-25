---
layout: default
title: Git Workflow
parent: Contributing
nav_order: 4
---

# Git Workflow

A lightweight branching and PR model keeps velocity high and history clean.

## Branch Naming

| Type | Prefix Example |
|------|----------------|
| Feature | `feature/route-caching` |
| Fix | `fix/nullref-startup` |
| Docs | `docs/tutorial-auth` |
| Chore | `chore/update-deps` |
| Experiment | `exp/async-scheduling` |

## Typical Flow

```bash
git checkout -b feature/my-improvement
# hack hack
Invoke-Build Test
git add .
git commit -m "Add: Improve route cache warmup"
git push origin feature/my-improvement
```

Open a Pull Request targeting `main`.

## Commit Message Style

```text
<Type>: <Concise summary>

Optional extended description explaining rationale.
```

Recommended types: Add, Fix, Change, Remove, Docs, Refactor, Test, Chore.

## Keeping Branch Up To Date

```bash
git fetch origin
git merge origin/main
# or rebase if you prefer linear history
git rebase origin/main
```

Resolve conflicts early—avoid giant merge bombs.

## Draft vs. Ready

Open a draft PR early for visibility. Mark ready when:

- Build passes
- Tests added/updated
- Docs updated (if needed)

## Review Guidelines

| Role | Responsibility |
|------|----------------|
| Author | Clear description, rationale, tests |
| Reviewer | Code correctness, clarity, scope |
| Maintainer | Merge readiness & release alignment |

## Handling Feedback

- Keep discussions focused per thread.
- Squash trivial fixup commits where possible.
- Re-request review after substantial changes.

## Merging

Squash merge preferred (clean history). Avoid merging failing builds.

---
Last updated: {{ site.time | date: '%Y-%m-%d' }}
