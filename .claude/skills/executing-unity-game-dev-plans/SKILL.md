---
name: executing-unity-game-dev-plans
description: Executes an existing Unity implementation plan produced after writing-unity-game-dev-plans — batched tasks, UVCS check-ins, Unity Editor handoffs, and Test Runner verification. Use when the user wants to implement, run, or continue work from docs/superpowers/plans, execute a Jira-linked Unity plan, or says "execute the plan" / "do the next tasks" for this project.
---

# Executing Unity Game Dev Plans

## Overview

Runs **after** a plan exists that followed [writing-unity-game-dev-plans](../writing-unity-game-dev-plans/SKILL.md) (parallel research, Unity-specific tasks, UVCS steps, editor callouts).

**REQUIRED SUB-SKILL:** Use `superpowers:executing-plans` for the core loop (load plan → critical review → `TodoWrite` → batches of tasks → report → repeat). This skill adds **non-negotiable Unity and UVCS execution rules**; it does not replace batching or checkpoints.

**Announce at start:** "I'm using executing-unity-game-dev-plans (with superpowers:executing-plans) to run this plan."

---

## Step 0: Load the Plan (Ground Truth)

1. **Read** the plan file with the Read tool — path is usually `docs/superpowers/plans/<filename>.md`. Do not execute from memory.
2. If the plan references **Jira** (`DEV-##`), optionally refresh acceptance criteria with `mcp__atlassian__getJiraIssue` when the ticket may have changed since the plan was written.
3. Keep `docs/GAME_PLAN.md` and `CLAUDE.md` in mind when the plan is ambiguous — architecture there overrides improvisation.

---

## Unity-Specific Execution Rules

### Code vs. Unity Editor

From the planning skill: **the agent writes C# (and other text assets); the user performs Unity Editor tasks.**

- When a task hits `> **Unity Editor task (user):**`, **stop treating that checkbox as agent-completable**. Implement all preceding code-side steps, then output a short **User checklist** (scene path, prefab, inspector fields, Test Runner window) and continue only after the user confirms or asks to skip with explicit consent.
- Never merge editor actions into the same todo item as code edits.

### Assembly definitions and compile order

- If the plan creates a new folder + `.asmdef`, create **asmdef before** or **with** the first scripts in that folder so the IDE/Unity does not drop scripts into `Assembly-CSharp` by accident.
- Every new `.cs` / `.asmdef` needs its **`.meta`** on disk; UVCS steps must list both (see below).

### MonoBehaviour and tests

- **Logic in plain C#**; MonoBehaviour wires lifecycle only — enforce this while coding, not only in the plan text.
- **Edit Mode** tests for pure logic; **Play Mode** only when the plan requires scene or MonoBehaviour lifecycle. Prefer the folders the plan names (`Assets/Tests/Editor/...`, `Assets/Tests/PlayMode/...`).

### Verification (no Unity CLI in this repo)

`CLAUDE.md`: there is **no** command-line Unity test/build pipeline here unless the user added one.

- If the plan says "run tests," state **Unity Editor → Window → General → Test Runner** and which assembly/mode; run any **available** shell linters the project actually has; do not invent fake CLI Unity output.
- Use `superpowers:verification-before-completion` before claiming a batch is fully verified.

---

## Version Control — UVCS, Not Git

Plans from `writing-unity-game-dev-plans` use **Unity Version Control (UVCS)** check-in steps, not git.

- After completing a batch that matches a plan **Check in via UVCS** step, stage and check in exactly the files the plan lists (every `.cs` / `.asmdef` with its `.meta`; new folders → `FolderName.meta` sibling per plan rules).
- Prefer the project skill [uvcs-incremental-checkin](../uvcs-incremental-checkin/SKILL.md) (`cm status`, `cm checkin`) when the environment supports the UVCS CLI.
- **Never** replace a planned UVCS step with `git add` / `git commit` for this project's delivery path. If the user maintains a **separate** git mirror, follow their explicit instructions only.

**Git worktrees:** Use `superpowers:using-git-worktrees` only when the user asked for git-based isolation. Do not treat git as the source of truth for check-ins on this project.

---

## Batching and Checkpoints

Align with `superpowers:executing-plans`:

- Default **first batch = three tasks** (or fewer if the plan has discrete UVCS/editor gates that require a stop sooner).
- Mark todos **completed** only when the task's **code** steps and **in-agent** verifications are done; for editor-dependent tasks, mark completed only after user confirmation or documented skip.
- End each batch with: what changed, what remains, verification notes, **"Ready for feedback."**

---

## When to Stop

Same discipline as `superpowers:executing-plans`:

- Blocker (compile error, unclear step, missing asset, test failure): **stop** and ask; do not guess.
- Plan is wrong or unsafe: raise the issue; optionally return to plan author with a minimal diff proposal to the markdown plan **only if** the user wants the plan updated.

---

## After All Plan Tasks

1. Run final verification the plan specifies (tests, manual steps).
2. Ensure the **last** UVCS step in the plan is satisfied or explicitly deferred by the user.
3. For wrap-up options (merge, handoff, next epic), use `superpowers:finishing-a-development-branch` when it fits the user's workflow; frame UVCS state honestly (pending check-ins, shelved work, etc.).

---

## Related skills

| Skill | Role |
|-------|------|
| [writing-unity-game-dev-plans](../writing-unity-game-dev-plans/SKILL.md) | Produces the plan this skill executes |
| `superpowers:executing-plans` | Batching, checkpoints, stop conditions |
| [uvcs-incremental-checkin](../uvcs-incremental-checkin/SKILL.md) | Semantic UVCS check-ins from CLI |
| `superpowers:verification-before-completion` | Evidence before "done" claims |
| `superpowers:systematic-debugging` | When a verification or test fails mid-batch |
