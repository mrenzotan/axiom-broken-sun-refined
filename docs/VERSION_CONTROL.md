# Version Control Guide — Axiom of the Broken Sun

This project uses **two version control systems in parallel**. This document explains why, what each system owns, and exactly what to do every day.

---

## Why Two Systems?

| | UVCS | GitHub (Git) |
|---|---|---|
| **Purpose** | Full project history — every file, every change | Code visibility, GitHub activity, team portfolio |
| **Tracks** | Everything: scripts, scenes, art, audio, Vosk model, DLLs | Scripts, docs, and config only — no binary files |
| **Team use** | Primary — always check in here first | Secondary — push after every code check-in |
| **Binary files** | Yes — handles them natively | No — binary files are excluded via `.gitignore` |
| **Scene merge conflicts** | Handled (file locking available) | N/A — scene files are excluded |

### Why not Git LFS for binaries?

We tried it. The Vosk model alone (~50 MB) burns Git LFS bandwidth every time someone clones or pulls. With three developers and a monthly reset, we hit the free-tier cap quickly. The solution: **UVCS owns all binary files, git never touches them.**

---

## What Lives Where

### Tracked by UVCS (everything)
UVCS is the source of truth. It tracks the full project.

### Tracked by Git → GitHub (text files only)

| Included | Excluded |
|---|---|
| `Assets/Scripts/` — all C# source | `Assets/Art/` — textures, sprites |
| `Assets/Data/` metadata (if text) | `Assets/Audio/` — music, SFX |
| `docs/` — design docs, guides | `Assets/Scenes/` — scene files |
| `CLAUDE.md`, `.claude/` | `Assets/Prefabs/` — prefab files |
| `Packages/manifest.json` | `StreamingAssets/` — Vosk model |
| `Packages/packages-lock.json` | `ThirdParty/` — Vosk DLLs |
| `ProjectSettings/` — project config | `Library/`, `Temp/`, `Logs/` |
| `.gitignore`, `.gitattributes` | All binary file types (`.png`, `.wav`, `.dll`, etc.) |

The `.gitignore` file enforces these rules automatically — you don't need to think about it. `git add -A` is safe to run; it will only stage what should be staged.

---

## One-Time Setup (New Developer)

Do this once when you join the project. After this, your daily workflow is just two commands.

### Prerequisites
- Git installed: https://git-scm.com/downloads
- GitHub account created and shared with the project lead (to be added as a collaborator)
- UVCS workspace already synced (you should have the full project locally)

### Steps

**1. Open a terminal in the project root**

```bash
cd "C:\unity-projects\axiom-broken-sun-refined"   # or wherever your workspace lives
```

**2. Configure your git identity** (skip if already done globally)

```bash
git config --global user.name "Your Name"
git config --global user.email "your.github@email.com"
```

> Use the same email address that is registered on your GitHub account — this is what makes commits count toward your GitHub contribution graph.

**3. Initialize git in the workspace**

```bash
git init
git checkout -b main
```

**4. Add the GitHub remote**

Ask the project lead for the GitHub repository URL, then:

```bash
git remote add origin https://github.com/OWNER/axiom-broken-sun.git
```

**5. Pull the existing history**

```bash
git pull origin main
```

You now have the full git history locally. Your workspace is ready.

---

## Daily Workflow

Every day, every developer follows this pattern:

### Step 1 — Check in via UVCS (always, for everything)

Unity Version Control → **Pending Changes** → select all relevant files → **Check in**

Write a clear message:
```
feat(voice): SpellCastController polls result queue and dispatches matched spells
fix(battle): guard PlayerTurn check before dispatching spell action
chore: update SpellData asset for hydrogen blast
```

This is your primary commit. It captures the full change including any scene edits, asset changes, and code.

### Step 2 — Push to GitHub (for code changes)

After a UVCS check-in that includes any `.cs`, `.asmdef`, doc, or config changes:

```bash
git add -A
git commit -m "same message you used in UVCS"
git push
```

That's it. `git add -A` is safe — `.gitignore` blocks all binary and generated files automatically.

### When to skip the git push

Skip the GitHub push if your UVCS check-in contained **only**:
- Scene edits (`.unity`)
- Art or audio changes
- Prefab-only changes
- Binary asset changes

If there's no code or text file change, there's nothing meaningful to push to GitHub.

---

## Commit Message Format

Use the same message in both UVCS and git. Follow this format:

```
<type>(<scope>): <short description>
```

| Type | When to use |
|---|---|
| `feat` | New feature or system |
| `fix` | Bug fix |
| `chore` | Config, build, tooling, non-code changes |
| `docs` | Documentation only |
| `refactor` | Code restructure, no behaviour change |
| `test` | Adding or fixing tests |

Examples:
```
feat(voice): add SpellResultMatcher for Vosk JSON parsing
fix(battle): resolve NullReferenceException when BattleController not injected
chore: update Packages/manifest.json for Cinemachine 2.9
docs: add voice architecture notes to GAME_PLAN.md
```

---

## Conflict Resolution

### UVCS conflicts
Handle these as normal in the UVCS panel. Scene and prefab conflicts are manageable here — use file locking if two developers need to edit the same scene simultaneously.

### Git conflicts
Git conflicts should almost never happen because:
1. Git only tracks text files (scripts, docs, config)
2. Binary files that would cause unresolvable conflicts are excluded

If a conflict does occur on a `.cs` file:
```bash
git status          # see which files conflict
# open the file, resolve the conflict markers
git add <file>
git commit -m "fix: resolve merge conflict in <file>"
git push
```

---

## Quick Reference Card

```
UVCS check-in → always, for all files
git push      → after check-ins that include code or docs

git add -A && git commit -m "message" && git push
```

Do not use `git add <specific-file>` for routine pushes — `git add -A` is correct here because `.gitignore` is already protecting you from adding binaries.

---

## Troubleshooting

### "UVCS shows `.git/` or `.gitignore` as pending changes"

The UVCS ignore config needs updating. Open the UVCS panel → Preferences → Ignored files and add:
```
.git
```

### "git is trying to add a large file / `.unity` scene file"

Your `.gitignore` may be missing an entry. Check that the file extension is listed. Do not force-add it. Update `.gitignore`, run `git rm --cached <file>` if it was accidentally staged, then re-run `git add -A`.

### "My commits aren't showing on my GitHub contribution graph"

Verify your git email matches your GitHub account email:
```bash
git config user.email
```

If it's wrong:
```bash
git config --global user.email "your.github@email.com"
```

Future commits will count. Past commits authored under the wrong email won't appear retroactively.

### "I forgot to push after a UVCS check-in"

No problem — just push now. All unpushed commits will go up at once:
```bash
git add -A
git commit -m "catch-up: sync recent UVCS check-ins to GitHub"
git push
```
