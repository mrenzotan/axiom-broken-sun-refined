# GitHub Mirror Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Set up a scripts-only Git mirror that pushes to GitHub alongside UVCS — no Git LFS, no binary files, no bandwidth problems — so all three team members accumulate GitHub activity while UVCS remains the authoritative source for everything.

**Architecture:** UVCS owns the full project (all art, audio, scenes, Vosk model, DLLs). Git tracks only text files (C# scripts, docs, config, package manifests). The `.gitignore` is the gatekeeper — it blocks every binary and generated file from ever entering git. Both `.gitignore` and `.gitattributes` are checked into UVCS so all team members receive them automatically on workspace sync.

**Tech Stack:** Git (no LFS), GitHub, UVCS (Unity Version Control)

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `.gitignore` | Block all binary, generated, and UVCS-internal files from git |
| Create | `.gitattributes` | Declare text encoding for all tracked file types |
| Create | `docs/VERSION_CONTROL.md` | Team reference guide for the dual-VCS workflow |
| Modify | `CLAUDE.md` | Update version control section to document dual-VCS setup |
| Modify | UVCS ignore config | Prevent UVCS from managing `.git/` internals |

---

## Task 1: Create `.gitignore`

**Files:**
- Create: `.gitignore` (project root)

This file is the most important piece — it must block every file that could bloat the GitHub repo or cause LFS bandwidth problems. When in doubt, exclude.

- [ ] **Create** `.gitignore` in the project root (`C:\unity-projects\axiom-broken-sun-refined\.gitignore`):

```gitignore
# ── Unity generated — never commit these ──────────────────────────────────────
[Ll]ibrary/
[Tt]emp/
[Ll]ogs/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Uu]ser[Ss]ettings/
*.pidb
*.pdb
*.mdb
sysinfo.txt
*.apk
*.aab

# ── Binary & large assets — UVCS only ─────────────────────────────────────────
# All art, audio, and large third-party files live exclusively in UVCS.
# Git never touches these — this is how we avoid Git LFS bandwidth limits.
Assets/Art/
Assets/Audio/
Assets/Prefabs/
Assets/Scenes/
Assets/Tilemaps/
ThirdParty/
StreamingAssets/

# ── Unity binary file types — UVCS only ───────────────────────────────────────
# These extensions are excluded globally even if the folder above is renamed.
*.png
*.jpg
*.jpeg
*.gif
*.tga
*.tiff
*.psd
*.mp3
*.wav
*.ogg
*.aif
*.fbx
*.obj
*.blend
*.dll
*.so
*.dylib
*.ttf
*.otf

# ── Unity scene & asset serialization — UVCS only ────────────────────────────
# .unity and .prefab files are technically YAML text but contain complex
# scene graphs that produce unreadable diffs and risk merge conflicts for
# the team. UVCS handles these; git does not.
*.unity
*.prefab
*.asset
*.mat
*.anim
*.controller
*.overrideController
*.physicMaterial
*.physicsMaterial2D
*.playable
*.renderTexture
*.mixer
*.shadervariants
*.flare
*.fontsettings
*.guiskin
*.mask
*.spriteatlas
*.terrainlayer
*.brush

# ── UVCS / Plastic SCM internals — git must not track these ───────────────────
.plastic/
plastic.ignore

# ── IDE & OS ──────────────────────────────────────────────────────────────────
.vs/
.vscode/
*.user
*.suo
*.userosscache
*.sln.docstates
.DS_Store
Thumbs.db
desktop.ini

# ── NuGet ────────────────────────────────────────────────────────────────────
[Pp]ackages/
!Packages/manifest.json
!Packages/packages-lock.json
```

---

## Task 2: Create `.gitattributes`

**Files:**
- Create: `.gitattributes` (project root)

Declares explicit line-ending and diff behaviour for every text file type git will track. Prevents Windows/macOS line-ending mismatches across the team.

- [ ] **Create** `.gitattributes` in the project root:

```gitattributes
# Auto-detect text/binary for unspecified files
* text=auto

# ── C# source ─────────────────────────────────────────────────────────────────
*.cs        text diff=csharp eol=lf
*.cs.meta   text eol=lf

# ── Assembly definitions ──────────────────────────────────────────────────────
*.asmdef        text eol=lf
*.asmdef.meta   text eol=lf

# ── Unity Input System ────────────────────────────────────────────────────────
*.inputactions        text eol=lf
*.inputactions.meta   text eol=lf

# ── Package manifests ─────────────────────────────────────────────────────────
*.json  text eol=lf
*.yaml  text eol=lf
*.yml   text eol=lf

# ── Docs & config ────────────────────────────────────────────────────────────
*.md    text eol=lf
*.txt   text eol=lf
*.xml   text eol=lf

# ── Shader source (text, not compiled) ───────────────────────────────────────
*.shader        text eol=lf
*.shader.meta   text eol=lf
*.hlsl          text eol=lf
*.cginc         text eol=lf

# ── Explicitly binary — git must never diff or merge these ───────────────────
*.png    binary
*.jpg    binary
*.jpeg   binary
*.tga    binary
*.tiff   binary
*.wav    binary
*.mp3    binary
*.ogg    binary
*.dll    binary
*.so     binary
*.dylib  binary
*.ttf    binary
*.otf    binary
*.fbx    binary
*.blend  binary
```

---

## Task 3: Add `.git/` to UVCS ignore

**Files:**
- Modify: UVCS workspace ignore configuration

UVCS must not attempt to manage git's internal `.git/` directory. If it does, it will try to track thousands of git-internal files and show them as pending changes.

- [ ] **Open** Unity → Window → Plastic SCM (or the UVCS panel). Navigate to **Preferences → Ignored files** (or open the `ignore.conf` file directly — typically at the workspace root or inside `.plastic/`).

- [ ] **Add** the following entries if they are not already present:

```
.git
.gitignore
.gitattributes
```

> **Why `.gitignore` and `.gitattributes`?** These files should be distributed via UVCS (so teammates get them), but UVCS does not need to *track changes* to them under the UVCS ignore system — only check them in once. If your UVCS version asks, add them to the workspace but not to the ignore list; the ignore list is specifically for `.git/` internals.

> **Simpler alternative:** If the UVCS ignore UI is hard to find, create or edit `C:\unity-projects\axiom-broken-sun-refined\ignore.conf` and add `.git` on its own line.

---

## Task 4: Initialize the Git repository

**Files:**
- No new files. Shell commands only.

- [ ] **Open** a terminal in the project root (`C:\unity-projects\axiom-broken-sun-refined`) and run:

```bash
git init
git checkout -b main
```

Expected output:
```
Initialized empty Git repository in C:/unity-projects/axiom-broken-sun-refined/.git/
Switched to a new branch 'main'
```

- [ ] **Configure** your git identity if not already set globally:

```bash
git config user.name "Your Name"
git config user.email "your@email.com"
```

---

## Task 5: Create the GitHub repository

> **User action (GitHub website):**
> 1. Go to github.com → New repository
> 2. Name it (e.g. `axiom-broken-sun`)
> 3. Set to **Private** (recommended while in active development)
> 4. **Do not** initialize with README, .gitignore, or license — the repo must be empty
> 5. Copy the remote URL (HTTPS or SSH)

- [ ] **Add** the GitHub remote in your terminal:

```bash
git remote add origin https://github.com/YOUR_USERNAME/axiom-broken-sun.git
```

---

## Task 6: Initial commit and push

- [ ] **Stage** all files that pass the `.gitignore` filter:

```bash
git add -A
```

- [ ] **Verify** what git picked up — confirm no binary files, no `Library/`, no `StreamingAssets/`:

```bash
git status
```

Expected: only `.cs` files, `.asmdef` files, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/` text files, `docs/`, `CLAUDE.md`, `.claude/`, `.gitignore`, `.gitattributes`.

If any unexpected large files appear, add them to `.gitignore` before continuing.

- [ ] **Commit:**

```bash
git commit -m "chore: initial GitHub mirror — scripts, docs, config only (UVCS is primary VCS)"
```

- [ ] **Push:**

```bash
git push -u origin main
```

Expected: push succeeds with no LFS prompts. Verify on GitHub that only text files are present.

---

## Task 7: Update `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

Update the version control section to document the dual-VCS setup so Claude and future contributors understand the setup.

- [ ] **Open** `CLAUDE.md`. Find the version control line (currently says `Git (local) — optionally Unity Version Control (UVCS) for team scale`) and replace the entire Tech Stack table row with:

```markdown
| **Version Control**    | UVCS (Unity Version Control) — primary, tracks all files including binary assets · Git (scripts-only mirror → GitHub) — secondary, for GitHub activity and code visibility; see `docs/VERSION_CONTROL.md` |
```

---

## Task 8: Check new files into UVCS

`.gitignore`, `.gitattributes`, and the updated `CLAUDE.md` must also be checked into UVCS so all team members receive them when they sync their workspace.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `chore: add git mirror config (.gitignore, .gitattributes) and update CLAUDE.md`
  - `.gitignore`
  - `.gitattributes`
  - `CLAUDE.md`
  - `docs/VERSION_CONTROL.md`

---

## Task 9: Per-developer setup guide

Each additional team member must run a one-time setup on their machine. This is documented in `docs/VERSION_CONTROL.md` — point them there. The short version:

1. Sync UVCS workspace (they receive `.gitignore` and `.gitattributes` automatically)
2. In the workspace root: `git init && git checkout -b main`
3. `git remote add origin https://github.com/YOUR_USERNAME/axiom-broken-sun.git`
4. `git pull origin main`
5. Configure their git identity (`git config user.name / user.email`)

After that, their daily workflow is identical to yours — see `docs/VERSION_CONTROL.md`.

---

## Self-Review

### Spec coverage

| Requirement | Task |
|---|---|
| No Git LFS | Covered by `.gitignore` blocking all binary extensions + asset types |
| No bandwidth surprises | `StreamingAssets/` (Vosk model) explicitly excluded in Task 1 |
| GitHub activity for all 3 members | Each member pushes from their own GitHub account (Task 9) |
| UVCS stays primary | UVCS check-in documented in Task 8; workflow in reference doc |
| Team members receive git config automatically | `.gitignore` + `.gitattributes` checked into UVCS in Task 8 |
| UVCS doesn't manage `.git/` internals | Task 3 |
| CLAUDE.md updated | Task 7 |
