---
name: writing-unity-game-dev-plans
description: Use when creating an implementation plan for a Unity game feature, Jira story, or gameplay system in a Unity C# project
---

# Writing Unity Game Dev Implementation Plans

## Overview

Augments `superpowers:writing-plans` with a parallel context-gathering phase that ensures plans reflect current Unity best practices, existing project conventions, and up-to-date API patterns — before a single task is written.

**REQUIRED SUB-SKILL:** Use `superpowers:writing-plans` to author the actual plan document. This skill defines WHAT to gather first, not how to format the plan.

---

## Phase 0: Parallel Context Gathering (Do This BEFORE Writing Any Tasks)

Run ALL of the following in **parallel** in a single message. Do not start writing tasks until all results are back.

### 1. Invoke supporting skills
```
Skill: game-development   → genre-specific patterns (state machines, combat loops, turn order)
Skill: unity-developer    → Unity 6 API idioms, MonoBehaviour rules, component architecture
```

### 2. Fetch the Jira ticket
Use `mcp__atlassian__getJiraIssue` with `responseContentFormat: "markdown"`.
Read the **Acceptance Criteria** carefully — edge cases and constraints are often only there.

### 3. Query current Unity docs and examples (Context7 + Exa MCP)
Use both paths together when possible:

```text
Context7 (official docs/API surface)
Step A: mcp__context7__resolve-library-id  →  libraryName: "Unity"
        Pick /websites/unity3d_manual (highest snippet count + High reputation)

Step B: mcp__context7__query-docs  →  query: [the specific system you're implementing]
        Examples: "state machine C# Unity", "ScriptableObject event channel", "Unity Test Framework EditMode"
```

```text
Exa MCP (fresh examples, blog posts, issue discussions)
Step A: use Exa search to find recent Unity 6 / C# patterns for the feature
Step B: extract the top sources and keep only guidance that aligns with official Unity docs and project architecture
```

Context7 remains the source of truth for API behavior. Exa is used to broaden implementation options and discover up-to-date community patterns.

### 4. Read the Game Design doc
`docs/GAME_PLAN.md` — contains non-negotiable architecture rules, naming conventions, and phase exit criteria that override all generic advice.

### 5. Inspect existing project conventions
Run these Glob/Read calls to avoid inventing patterns the codebase already has:

| What to check | Why |
|---|---|
| `Assets/Scripts/**/*.asmdef` | asmdef naming style (`Axiom.Battle`, not `Battle`) and `autoReferenced` / `optionalUnityReferences` pattern |
| `Assets/Tests/**/*.asmdef` | Test asmdef structure — which fields they use |
| `Assets/Scripts/<nearest-module>/` | Existing namespace, file structure to mirror |
| `Assets/Scenes/**/*.unity` | Which scenes exist; where to add the new one |

---

## Unity-Specific Plan Requirements

When writing the plan with `superpowers:writing-plans`, ensure every task includes these Unity-specific elements:

### Assembly Definitions
Every new script folder needs an `.asmdef`. Mirror the project's existing pattern exactly:
- Runtime asmdef: `Axiom.<Module>`, `autoReferenced: true`, empty `references` unless third-party packages are needed
- Test asmdef: references runtime asmdef by name + uses `optionalUnityReferences` for test runner (not `references`)

### Editor Task vs. Code Task Separation
**Claude writes all C# scripts directly. The user performs all Unity Editor tasks.**

Mark Unity Editor steps explicitly:
```markdown
> **Unity Editor task (user):** Create the Battle scene at Assets/Scenes/Battle.unity
```
Never write code steps and editor steps in the same checkbox.

### Test Placement
| Test type | When to use | Folder |
|---|---|---|
| Edit Mode (NUnit) | Pure C# classes, no scene, no MonoBehaviour | `Assets/Tests/Editor/<Module>/` |
| Play Mode | Requires scene loading, MonoBehaviour lifecycle | `Assets/Tests/PlayMode/<Module>/` |

For plain C# game logic classes (BattleManager, stats systems, AI strategies) — always Edit Mode.

### Commit Steps — UVCS ONLY (never git)

> **CRITICAL:** This project uses **Unity Version Control (UVCS)**, NOT git. CLAUDE.md may say "Git (local)" — ignore that for commit steps. UVCS is the source of truth for version control in this project.

Every commit step in the plan must use this exact format:
```markdown
- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `<type>(DEV-##): <short description>`
  - `Assets/Scripts/Voice/Foo.cs`
  - `Assets/Scripts/Voice/Foo.cs.meta`
```

Where `DEV-##` is the Jira ticket number for the plan. Examples: `feat(DEV-22): add SpellCastController`, `fix(DEV-34): resolve null ref in BattleController`. See `docs/VERSION_CONTROL.md` for the full type list (`feat`, `fix`, `chore`, `docs`, `refactor`, `test`).

**Never write** `git add`, `git commit`, or any other git command in a plan for this project.

### MonoBehaviour Separation Rule (Non-Negotiable)
From GAME_PLAN.md architecture standards:
- `MonoBehaviour` → lifecycle only (`Start`, `Update`, `OnDestroy`, event wiring)
- All logic → plain C# class injected into the MonoBehaviour
- Test the plain C# class in Edit Mode; test the MonoBehaviour wrapper in Play Mode (if at all)

---

## Common Mistakes

| Mistake | Fix |
|---|---|
| Writing tasks before checking existing asmdefs | Check asmdefs first — wrong naming breaks compile |
| Using `git add` / `git commit` | **UVCS only.** CLAUDE.md may mention "Git (local)" — that is stale. Every commit step must use Unity Version Control → Pending Changes → Check in. |
| Mixing Unity Editor steps with code steps | Separate them with explicit `> Unity Editor task (user):` callouts |
| Putting logic in MonoBehaviour | Move logic to plain C# class; MonoBehaviour only wires lifecycle |
| Skipping Context7 and Exa research | Game-engine APIs and ecosystem patterns change quickly — always pull current docs plus recent implementation references |
| Not reading the Jira ticket's Acceptance Criteria | AC contains guard clauses and edge cases not in the story title |

---

## Phase 1: Post-Plan Review (Mandatory — Run After `superpowers:writing-plans` Saves the File)

`superpowers:writing-plans` includes a generic self-review. That is not enough for Unity C# plans. After the plan file is saved, **read it back with the Read tool** and run every check below. Fix inline before offering execution options.

### 1. Read the saved plan file

```
Read: docs/superpowers/plans/<saved-filename>.md
```

Do not rely on memory. The Read tool output is the ground truth.

### 2. C# guard clause ordering

For every method in the plan's code blocks, trace the null/empty guard order:

- **Rule:** Early-exit paths that bypass a parameter must come *before* that parameter's null guard.
- **Example failure:** Checking `model == null` before checking if the spell list is empty — an empty list never needs the model, so it should exit before the model guard fires.
- **Fix:** Reorder guards so parameters that are irrelevant to an early-exit path are checked *after* the exit.

### 3. Test coverage gaps — every non-trivial code path needs a test

For each method in the implementation tasks, walk every branch:

| Branch type | Ask yourself |
|---|---|
| Null argument | Is there a test that passes null and asserts `ArgumentNullException`? |
| Empty collection | Is there a test for the empty case that asserts the expected return value? |
| Async early-exit | If an async method returns early (e.g. `Task.FromResult(null)`), is that path tested *without* requiring external dependencies (model files, network)? |
| Happy path | Are there tests for single-item and multi-item inputs? |

Any untested branch that is reachable without external dependencies (no model file, no scene, no hardware) must get a test added to the plan.

### 4. UVCS staged file audit

For every file the plan **creates or modifies**, confirm it appears in the nearest UVCS check-in step:

- Every `.cs` file → its `.cs.meta` must also be listed
- Every `.asmdef` file → its `.asmdef.meta` must also be listed
- Every new **folder** created by the user in the Unity Editor → its `FolderName.meta` (sibling of the folder, not inside it) must be listed
- If a file is created in Task 1 but the first UVCS step is in Task 2, ensure all Task 1 files are in that step

### 5. Method signature consistency

Scan the test file code blocks and the implementation code blocks side by side:

- Method names must be spelled identically
- Parameter names, types, and order must match
- Return types must match (e.g. `Task<VoskRecognizer>` vs `Task<VoskRecognizer?>`)
- Namespace in `using` statements must match the `namespace` declaration in the implementation

### 6. Unity Editor task isolation

Verify every Unity Editor action has its own `> **Unity Editor task (user):**` callout and is **not** in the same checkbox as a code step. Mixed steps cause confusion about who does what.
