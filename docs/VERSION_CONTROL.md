# Version Control Guide — Axiom of the Broken Sun

This project uses two version control systems with clearly separated roles. **All collaborative development happens in UVCS.** Git exists for one reason only: so each team member accumulates GitHub activity.

---

## Roles at a Glance

|                      | UVCS                                                       | Git → GitHub                                              |
| -------------------- | ---------------------------------------------------------- | --------------------------------------------------------- |
| **Purpose**          | Collaborative development — source of truth for everything | GitHub activity tracking only                             |
| **Tracks**           | Everything: scripts, scenes, art, audio, Vosk model, DLLs  | Scripts, docs, and config only — no binary files          |
| **Workflow**         | Check in here for all changes — this is how the team syncs | One-way push only — mirrors your UVCS check-ins to GitHub |
| **Binary files**     | Yes — handles them natively                                | No — excluded via `.gitignore`                            |
| **Conflict merging** | Yes — use UVCS for all merge decisions                     | Never pull from GitHub — UVCS is your sync mechanism      |

### Why not use GitHub for collaboration?

Unity projects have binary assets (scenes, prefabs, art, audio) that git cannot merge. UVCS understands Unity's file formats, supports file locking for scenes, and keeps all three developers in sync including assets. Git is text-only and not suitable for Unity collaboration.

### Why not Git LFS for binaries?

The Vosk model alone (~50 MB) burns Git LFS bandwidth on every clone and pull. With three developers and a monthly reset, the free-tier cap runs out quickly. The solution: **UVCS owns all binary files, git never touches them.**

---

## What Lives Where

### Tracked by UVCS (everything)

UVCS is the source of truth. It tracks the full project.

### Tracked by Git → GitHub (text files only)

| Included                            | Excluded                                             |
| ----------------------------------- | ---------------------------------------------------- |
| `Assets/Scripts/` — all C# source   | `Assets/Art/` — textures, sprites                    |
| `Assets/Data/` metadata (if text)   | `Assets/Audio/` — music, SFX                         |
| `docs/` — design docs, guides       | `Assets/Scenes/` — scene files                       |
| `CLAUDE.md`, `.claude/`             | `Assets/Prefabs/` — prefab files                     |
| `Packages/manifest.json`            | `StreamingAssets/` — Vosk model                      |
| `Packages/packages-lock.json`       | `ThirdParty/` — Vosk DLLs                            |
| `ProjectSettings/` — project config | `Library/`, `Temp/`, `Logs/`                         |
| `.gitignore`, `.gitattributes`      | All binary file types (`.png`, `.wav`, `.dll`, etc.) |

The `.gitignore` file enforces these rules automatically — you don't need to think about it. `git add -A` is safe to run; it will only stage what should be staged.

---

## One-Time Setup (New Developer)

Do this once when you join the project.

### Prerequisites

- Git installed: https://git-scm.com/downloads
- GitHub account created and shared with the project lead (to be added as a collaborator)
- UVCS workspace already synced (you should have the full project locally, including `.gitignore` and `.gitattributes`)

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

> Use the same email address registered on your GitHub account — this is what makes commits count toward your GitHub contribution graph.

**3. Initialize git in the workspace**

```bash
git init
git checkout -b main
```

**4. Add the GitHub remote**

Ask the project lead for the GitHub repository URL, then:

```bash
git remote add origin https://github.com/mrenzotan/axiom-broken-sun-refined.git
```

**5. Push your first commit**

```bash
git add -A
git commit -m "chore: initial mirror setup for [your name]"
git push -u origin main
```

`git add -A` is safe here — `.gitignore` (synced from UVCS) automatically blocks all binary and generated files. If you see thousands of untracked files, it means `.gitignore` is missing from your workspace. Re-sync UVCS before continuing.

Do **not** run `git pull`. Your UVCS workspace already has the latest files — pulling from GitHub is unnecessary and risks conflicts.

Your workspace is ready.

---

## Daily Workflow

### Step 1 — Sync UVCS at the start of each session

Always sync your UVCS workspace first. This gives you all teammates' latest changes — scripts, scenes, art, everything. Do not run `git pull`; UVCS sync already covers the files git tracks.

### Step 2 — Do all your work and check in via UVCS

Unity Version Control → **Pending Changes** → select all relevant files → **Check in**

UVCS is where collaboration happens. All changes — code, scenes, art, audio — go here first.

### Step 3 — Mirror code changes to GitHub

After a UVCS check-in that includes any `.cs`, `.asmdef`, doc, or config changes:

```bash
git add -A
git commit -m "same message you used in UVCS"
git push
```

`git add -A` is safe — `.gitignore` blocks all binary and generated files automatically.

### When to skip the git push

Skip the GitHub push if your UVCS check-in contained **only**:

- Scene edits (`.unity`)
- Art or audio changes
- Prefab-only changes
- Binary asset changes

If there's no code or text file change, there's nothing meaningful to push to GitHub.

---

## Commit Message Format

Use the same message in both UVCS and git. Always include the Jira ticket ID:

```
<type>(DEV-###): <short description>
```

| Type       | When to use                              |
| ---------- | ---------------------------------------- |
| `feat`     | New feature or system                    |
| `fix`      | Bug fix                                  |
| `chore`    | Config, build, tooling, non-code changes |
| `docs`     | Documentation only                       |
| `refactor` | Code restructure, no behaviour change    |
| `test`     | Adding or fixing tests                   |

Examples:

```
feat(DEV-20): add SpellResultMatcher for Vosk JSON parsing
fix(DEV-34): resolve NullReferenceException when BattleController not injected
chore(DEV-12): update Packages/manifest.json for Cinemachine 2.9
docs(DEV-5): add voice architecture notes to GAME_PLAN.md
```

For changes that span no specific ticket (rare):

```
chore: update .gitignore to exclude ProjectSettings private files
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

### "UVCS shows `.git/` as pending changes"

The UVCS ignore config needs updating. Open the UVCS panel → Preferences → Ignored files and add:

```
.git
```

Do **not** add `.gitignore` or `.gitattributes` to the ignore list — these files are intentionally tracked by UVCS so all developers receive them on sync.

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

### "git push is rejected after syncing UVCS — local branch is behind origin/main"

This happens when teammates have pushed to GitHub since your last push, and you've accumulated UVCS check-ins without mirroring them. Your local files are correct (UVCS is the source of truth) — only the git histories have diverged.

**Do not run `git pull`.** Pulling from GitHub is never correct — it risks overwriting your UVCS-synced files with stale history.

Instead, stage and commit your changes, then force-push:

```bash
git add -A
git commit -m "chore: catch-up sync of recent UVCS check-ins to GitHub"
git push
```

If git rejects the push (non-fast-forward error):

```bash
git push --force
```

This is safe because GitHub is a write-only mirror — no one on the team pulls from it. Force-pushing rewrites the remote git history to match your correct local state. To avoid this situation, push to GitHub after every UVCS check-in that includes code or docs.
