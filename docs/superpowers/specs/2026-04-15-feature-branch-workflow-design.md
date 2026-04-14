# Feature Branch Workflow Design

**Date:** 2026-04-15  
**Status:** Approved  
**Scope:** Version control workflow update — from single-branch to dual-branch feature flow for two-developer team

---

## Context

Previously, all development happened directly on UVCS `main`, mirrored commit-for-commit to GitHub `main`. With two developers now working simultaneously, a feature branch flow is needed to:

- Prevent developers from stepping on each other's work
- Maintain GitHub contribution activity for both developers
- Keep a stable, build-ready `main` branch separate from active development

---

## Branch Structure

| Branch | UVCS | Git → GitHub |
|---|---|---|
| `main` | Stable, build-ready. Never commit directly. | Mirrors UVCS `main`. Push only at promotion. |
| `main/dev` (UVCS) / `dev` (git) | Primary integration branch. All feature work lands here first. | Mirrors UVCS `main/dev`. Push after each feature merge. Optional per developer. |
| `main/dev/DEV-###-short-desc` | Feature branches, one per Jira ticket. Created from `main/dev`. | Not mirrored. UVCS only. |

**Feature branch naming convention:** `main/dev/DEV-###-short-desc`  
Example: `main/dev/DEV-41-save-load-system`

---

## Daily Workflow

### Start of session
Sync UVCS workspace (as before). Do not run `git pull`.

### Starting a new feature
In the UVCS panel, create a new branch from `main/dev`:
```
main/dev/DEV-###-short-desc
```

### During development
Check in to your feature branch via UVCS as normal. No git mirroring during feature development.

### Feature complete — merge to main/dev
1. UVCS: merge your feature branch → `main/dev`
2. **[Optional, per developer]** Mirror to git `dev`:

```bash
git checkout dev
git add -A
git commit -m "feat(DEV-###): short description"
git push origin dev
```

This step is optional but encouraged — it records a GitHub contribution for each completed feature.

### Promoting main/dev → main (stable milestone)
When `main/dev` is stable and tested, one team member promotes it to `main`. **Whoever performs the UVCS merge is also responsible for mirroring it to git.**

1. UVCS: merge `main/dev` → `main`
2. Mirror to git — both branches must be updated:

```bash
# Sync dev first (only if there are uncommitted changes — check with git status)
git checkout dev
git add -A
git diff --cached --quiet || git commit -m "chore: sync dev before promotion to main"
git push origin dev

# Merge dev into main and push
git checkout main
git merge dev
git push origin main

# Return to dev for continued work
git checkout dev
```

---

## When to Skip the Git Push

Same rule as before — skip the optional `dev` push if your feature contained **only**:
- Scene edits (`.unity`)
- Art or audio changes
- Prefab-only changes
- Binary asset changes

If there's no code or text file change, there's nothing meaningful to push.

**Never push to git `main` directly during feature work.** Git `main` only moves forward at UVCS promotion events.

---

## Commit Message Format

No change. Same format applies to both `dev` and `main` git pushes:

```
<type>(DEV-###): <short description>
```

---

## Bootstrapping the New Structure (One-Time)

Git currently has only `main`. The 27 uncommitted changes in the current workspace represent dev work on UVCS `main/dev` that has never been mirrored. To set up the dual-branch structure:

```bash
# Create git dev branch from current main
git checkout -b dev

# Commit all current changes to dev
git add -A
git commit -m "chore: bootstrap dev branch — sync UVCS main/dev state to git"

# Push dev to GitHub (creates it remotely)
git push -u origin dev

# dev is now the default working branch
```

After this, GitHub has both `main` and `dev`, with `dev` one commit ahead of `main`.

---

## Files to Update

1. **`docs/VERSION_CONTROL.md`** — rewrite to reflect new branch structure, updated daily workflow, promotion workflow, and optional dev push guidance.
2. **`CLAUDE.md`** — update the version control section to reference the dual-branch model.
