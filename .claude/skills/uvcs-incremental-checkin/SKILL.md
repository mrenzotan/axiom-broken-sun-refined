---
name: uvcs-incremental-checkin
description: Check in pending Unity Version Control (UVCS/Plastic SCM) changes from the command line using incremental, semantic commits. Use when the user asks to check in pending changes, create incremental UVCS commits, split work into semantic check-ins, or batch asset files together.
---

# UVCS Incremental Check-In

## Purpose

Create clean UVCS check-ins from pending changes by:

1. Working from the command line (`cm`)
2. Splitting code changes into small semantic check-ins
3. Grouping asset-only changes into a single asset check-in when appropriate

## Command-Line First

Use UVCS CLI commands (not GUI) for all operations:

- Inspect pending changes with `cm status`
- Check file history or context with `cm diff` when needed
- Create check-ins with `cm checkin`

If your workspace or repo context is unclear, verify it before check-in.

## Commit Strategy

### 1) Analyze pending changes

Run `cm status` and categorize files by intent:

- gameplay logic
- UI
- audio
- bug fix
- refactor
- tests
- docs
- assets (textures, models, audio files, prefabs, materials, etc.)

### 2) Build incremental batches

Create multiple small check-ins for code when changes are logically separable.

Prefer one intent per check-in:

- one bug fix
- one feature slice
- one refactor step

Do not mix unrelated concerns in a single code check-in.

### 3) Asset handling rule

If there are asset changes, you may check in assets as one grouped batch, especially when:

- assets are related to the same feature
- assets are bulk imports/updates
- splitting would add noise without improving traceability

Use a clear message that states the asset group purpose.

## Semantic Message Format

Use concise semantic messages:

`type(DEV-XX): short summary`

Where:

- `DEV-XX` is the Jira task identifier scope
- `XX` is the numeric Jira task number (for example `DEV-67`)

Good types:

- `feat`
- `fix`
- `refactor`
- `perf`
- `test`
- `docs`
- `chore`
- `assets`

Examples:

- `fix(DEV-67): prevent hover state from persisting after scene swap`
- `feat(DEV-67): add hover and confirm SFX triggers`
- `refactor(DEV-48): simplify transition guard checks`
- `assets(DEV-48): add updated button sprites and click SFX clips`

## Suggested Workflow Checklist

Use this sequence each time:

1. Run `cm status`
2. Group files by intent
3. Select first logical batch
4. Write semantic message (`type(DEV-XX): summary`) using the Jira task number
5. Check in that batch with `cm checkin`
6. Repeat for remaining code batches
7. Check in asset files as one grouped batch (if present)
8. Run `cm status` again and confirm no unintended pending files remain

## Output Expectations

When completing a UVCS check-in task, report:

- how many check-ins were created
- each check-in message
- which files were grouped as assets
- any files intentionally left pending

## Guardrails

- Keep check-ins incremental and reviewable
- Prefer clarity over clever naming
- Do not include unrelated files in the same check-in
- If uncertain about grouping, choose smaller code check-ins and one asset batch
