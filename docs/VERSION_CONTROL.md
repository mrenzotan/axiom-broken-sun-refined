# Version Control Guide — Axiom of the Broken Sun

This project uses two version control systems with clearly separated roles. **All collaborative development happens in UVCS.** Git exists for one reason only: so each team member accumulates GitHub activity.

---

## Roles at a Glance

|                      | UVCS                                                       | Git → GitHub                                              |
| -------------------- | ---------------------------------------------------------- | --------------------------------------------------------- |
| **Purpose**          | Collaborative development — source of truth for everything | GitHub activity tracking only                             |
| **Tracks**           | Everything: scripts, scenes, art, audio, Vosk model, DLLs  | Scripts, docs, and config only — no binary files          |
| **Workflow**         | Check in here for all changes — this is how the team syncs | Optional push after feature merges; required at promotions |
| **Binary files**     | Yes — handles them natively                                | No — excluded via `.gitignore`                            |
| **Conflict merging** | Yes — use UVCS for all merge decisions                     | Never pull from GitHub — UVCS is your sync mechanism      |

---

## Branch Structure

| Branch | UVCS | Git → GitHub |
| --- | --- | --- |
| `main` | Stable, build-ready. Never commit directly. | Mirrors UVCS `main`. Push only at promotion events. |
| `main/dev` (UVCS) / `dev` (git) | Primary integration branch. All feature work lands here. | Mirrors UVCS `main/dev`. Optional push after each feature merge. |
| `main/dev/DEV-###-short-desc` | Feature branches, one per Jira ticket. Created from `main/dev`. | Not mirrored. UVCS only. |

**Feature branch naming:** `main/dev/DEV-###-short-desc`
Example: `main/dev/DEV-41-save-load-system`

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
git checkout -b dev
```

**4. Add the GitHub remote**

Ask the project lead for the GitHub repository URL, then:

```bash
git remote add origin https://github.com/mrenzotan/axiom-broken-sun-refined.git
```

**5. Fetch both branches**

```bash
git fetch origin
git branch --track main origin/main
git push -u origin dev
```

Your default working branch is `dev`. Only touch `main` at promotion time.

---

## Daily Workflow

### Step 1 — Sync UVCS at the start of each session

Always sync your UVCS workspace first. This gives you all teammates' latest changes — scripts, scenes, art, everything. Do not run `git pull`; UVCS sync already covers the files git tracks.

### Step 2 — Create a feature branch in UVCS

For each Jira ticket you're working on, create a branch in UVCS from `main/dev`:

```
main/dev/DEV-###-short-desc
```

Example: `main/dev/DEV-41-save-load-system`

Check in to your feature branch as you work. This is where UVCS collaboration happens.

### Step 3 — Merge your feature branch to main/dev in UVCS

When the feature is complete, merge your UVCS feature branch → `main/dev` via the UVCS panel.

### Step 4 — [Optional] Mirror to git dev

After merging to `main/dev`, each developer can optionally push to git `dev` to record a GitHub contribution:

```bash
git checkout dev
git add -A
git commit -m "feat(DEV-###): short description"
git push origin dev
```

This step is optional but encouraged — it ties your GitHub activity to meaningful events (completed features).

### When to skip the git push

Skip the `dev` push if your feature contained **only**:

- Scene edits (`.unity`)
- Art or audio changes
- Prefab-only changes
- Binary asset changes

If there's no code or text file change, there's nothing meaningful to push.

---

## Promoting main/dev → main

When `main/dev` is stable and tested, one team member promotes it to `main`. **Whoever performs the UVCS merge is also responsible for mirroring it to git.**

**1. UVCS:** merge `main/dev` → `main` via the UVCS panel.

**2. Mirror to git — both branches must be updated:**

```bash
# Sync dev first (only if there are uncommitted changes)
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
feat(DEV-41): add SaveService with JSON persistence
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
3. Feature branches only exist in UVCS — git has only `main` and `dev`

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
UVCS feature branch  →  merge to main/dev  →  [optional] git push origin dev
UVCS main/dev        →  promote to main    →  git merge dev && push both
```

**Never push directly to git `main`.** Only `git merge dev` at promotion time.

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

### "I forgot to push after merging a feature"

No problem — just push now. All unpushed commits will go up at once:

```bash
git checkout dev
git add -A
git commit -m "chore: catch-up sync of recent feature merges"
git push origin dev
```

### "git push is rejected — local branch is behind origin"

This happens when another developer has pushed to the same git branch. Your local files are correct (UVCS is the source of truth) — only the git histories have diverged.

**Do not run `git pull`.** Pulling from GitHub is never correct — it risks overwriting your UVCS-synced files with stale history.

Instead, force-push with lease:

```bash
git push --force-with-lease
```

This is safe because GitHub is a write-only mirror — no one on the team pulls from it. `--force-with-lease` only overwrites the remote if it matches what your git client last fetched, so it fails safely if someone else pushed at the same time.
