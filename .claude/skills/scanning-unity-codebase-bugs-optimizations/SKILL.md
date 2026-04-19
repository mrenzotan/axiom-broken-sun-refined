---
name: scanning-unity-codebase-bugs-optimizations
description: Runs a structured full-repository scan of a Unity C# project for bugs, performance pitfalls, and architecture violations against project standards. Requires dispatching parallel readonly explore subagents for repo-wide scans. Use when the user requests a codebase audit, bug hunt, optimization pass, technical debt review, pre-release quality sweep, or full-project health check.
---

# Scanning a Unity Codebase for Bugs and Optimizations

## Overview

Mirrors the **parallel discovery → structured output → mandatory verification** rhythm of `writing-unity-game-dev-plans`, but the deliverable is a **findings report** (and optional fix backlog), not an implementation plan.

**REQUIRED SUB-SKILL:** Use `superpowers:systematic-debugging` when any finding depends on reproducing runtime behavior; use `superpowers:verification-before-completion` before claiming “no issues” or “all clear.” This skill defines **what to scan and how to prioritize**, not how to fix every issue.

---

## Mandatory: dispatch parallel subagents

**Orchestrator rule:** A full-repo or multi-module audit must **not** rely on one agent doing all exploration serially. Dispatch **parallel `Task` subagents** (`subagent_type: explore`, `readonly: true`) in a **single message** so independent areas are searched concurrently.

1. **Parent agent** loads standards once (`docs/GAME_PLAN.md`, `CLAUDE.md`, scope from user) and defines non-overlapping slices.
2. **Launch at least two** explore subagents in parallel, each with a tight prompt: search roots (e.g. `Assets/Scripts/Battle/`), concrete signals (grep patterns, architecture rules), and the deliverable (“return file paths + severity guesses + one-line evidence”).
3. **Parent merges** subagent results: dedupe findings, reconcile conflicts, run Phase 1 `Read` verification on anything cited as P0/P1.

**Suggested parallel slices** (pick those that fit scope; combine small slices rather than spawning dozens):

| Subagent | Focus |
|----------|--------|
| Architecture | Layout vs `CLAUDE.md`, forbidden singletons, SO vs hardcoded data, UI folder rules |
| Correctness / lifecycle | Events, null guards, scene/`DontDestroyOnLoad`, async/Task |
| Performance | Hot `Update` paths, allocations, LINQ, `GetComponent` in loops, `Resources`/`Find*` |
| Voice / threading | Only if `Assets/Scripts/Voice/` or mic pipeline in scope — producer/consumer, main-thread rules |
| Tests / asmdefs | `Assets/Tests/**`, `.asmdef` graph and coverage gaps |

**When subagents may be skipped:** User narrowed scope to **one** module **and** roughly **≤15** `.cs` files — then the parent may use parallel `SemanticSearch`/`Grep` only.

**Coordination:** Follow `superpowers:dispatching-parallel-agents` / `.agents:dispatching-parallel-agents` — independent slices, no shared mutable state; parent owns the final report.

---

## Phase 0: Parallel Context Gathering (Before Deep File Reads)

Run the following in **parallel** in a single message where possible. Do not claim audit completeness until inventory and standards are loaded.

### 1. Invoke supporting skills

```text
Skill: unity-developer   → Unity 6 / URP / MonoBehaviour lifecycle, common perf traps
Skill: game-development  → gameplay/state patterns that often hide bugs (combat loops, turn order, input)
```

Optional when findings touch web research or unfamiliar APIs:

```text
Context7: resolve Unity manual + query-docs for the specific API under suspicion
Exa: only for “is this a known Unity 6 regression/pattern?” — never override official docs
```

### 2. Load non-negotiable standards (project)

| Source | Why |
|--------|-----|
| `docs/GAME_PLAN.md` | Architecture rules, naming, phase criteria — **violations are P1–P0** |
| `CLAUDE.md` | Folder layout, singleton policy, voice threading, dead-code policy |
| `docs/VERSION_CONTROL.md` | If the audit output includes check-in guidance (UVCS message types) |

Attach large mechanic docs (`docs/game-mechanics/...`, `docs/LORE_AND_MECHANICS.md`) only when the scan scope includes those systems.

### 3. Repository inventory (fast, wide)

Use **Glob** in parallel:

| Pattern | Purpose |
|---------|---------|
| `Assets/Scripts/**/*.cs` | Runtime surface area |
| `Assets/Scripts/**/*.asmdef` | Assembly graph, naming, test references |
| `Assets/Tests/**/*.cs` | Test gaps vs modules |
| `Assets/**/*.unity` | Scene set vs documented scenes |
| `Packages/manifest.json` | Package versions, duplicate risk |

### 4. Risk-oriented semantic / grep passes (parallel)

Launch **multiple** `SemanticSearch` or `Grep` queries tuned to this repo’s standards, for example:

- `Update` / `FixedUpdate` / `LateUpdate` + allocations (`new`, string concat, LINQ, `GetComponent` in loop)
- `OnDestroy` / `OnDisable` — missing unsubscription, stopped coroutines, disposed handles
- `static` / `Singleton` / `Instance` — anything beyond allowed `GameManager` pattern (`CLAUDE.md`)
- `async` / `Task` / `void` async — fire-and-forget, missing cancellation
- Threading: `Thread`, `Task.Run`, locks — cross-check **voice** producer/consumer rules if touching `Voice/`
- `Resources.Load` / `FindObjectOfType` / stringly scene loads — perf and fragility
- `TODO`, `FIXME`, `HACK`, commented-out blocks — debt and half-finished paths
- Empty `catch`, swallowed exceptions — correctness

Adjust queries to the user’s **scope** (whole repo vs `Assets/Scripts/Battle/` only).

---

## Scan Dimensions (Check Each Relevant to Scope)

Group findings under these headings in the final report. Use **P0–P3** severity (P0 = ship blocker / data loss / thread safety violation).

### A. Architecture and maintainability

- MonoBehaviour thinness: logic bloated in behaviours vs plain C# classes (`GAME_PLAN` / `CLAUDE.md`)
- Forbidden singletons; static mutable state
- Hardcoded gameplay data that should live in ScriptableObjects / `Assets/Data/`
- UI location rules (`Battle/UI/`, `Platformer/UI/` — no orphan `Assets/Scripts/UI/`)

### B. Correctness and lifecycle

- Nullability: missing guards, order of guard clauses (early exits before irrelevant null checks)
- Unity order: `Awake`/`Start`/`OnEnable` assumptions across scenes and prefabs
- Events: subscribe in `OnEnable`, unsubscribe in `OnDisable` / `OnDestroy` as appropriate
- Scene load / `DontDestroyOnLoad` duplicates and orphaned objects

### C. Performance (URP 2D + desktop targets)

- Per-frame work: physics queries, raycasts, pathfinding, audio, LINQ in hot paths
- Allocations: boxing, closures capturing heavy state, `foreach` over non-struct enumerators where it matters
- Asset loading: synchronous loads in gameplay paths, uncompressed audio misuse (flag when evidence exists)
- Build/player settings only when the user scope includes ProjectSettings (otherwise note “not scanned”)

### D. Voice / threading (if `Assets/Scripts/Voice/` in scope)

Verify against `CLAUDE.md`:

- Vosk / waveform work **not** on main thread except documented handoff
- Queues and shutdown: no use-after-dispose, clean teardown on scene exit

### E. Tests and assemblies

- Edit Mode vs Play Mode placement vs `CLAUDE.md` / existing `Assets/Tests/**` layout
- `.asmdef` references: missing refs → compile issues; wrong `optionalUnityReferences` for tests
- Public API surface with **no** tests for non-trivial branches (call out as **debt**, not always P0)

### F. Version control hygiene (optional section)

This project uses **UVCS**, not git, for check-ins. If the audit includes “how to land fixes,” reference `docs/VERSION_CONTROL.md` and list `.meta` siblings for every touched asset path — same discipline as implementation plans.

---

## Output Format (Mandatory)

Deliver a single markdown report (chat or `docs/audits/<date>-unity-scan.md` if the user asked for a file):

```markdown
# Unity codebase scan — [scope] — [date]

## Executive summary
[3–6 sentences: worst risks, rough counts by severity]

## Scan parameters
- Scope: [paths / assemblies / scenes included]
- Excluded: [e.g. ThirdParty, generated, not requested]
- Evidence basis: [static analysis only | + Test Runner | + manual repro notes]

## Findings

### P0 — [title]
- **Where:** `path/to/File.cs` (approx line or symbol)
- **Evidence:** [code citation or grep pattern result]
- **Risk:** [user-visible failure / thread hazard / corruption]
- **Recommendation:** [one concrete next step]

### P1 — ...
...

## Optimizations (non-blocking)
- [Ordered by impact vs effort]

## False positives / needs human confirmation
- [Items that need Play Mode, device, or design intent]

## Suggested follow-ups
- [ ] Repro or UTR test for finding X
- [ ] Profile with Unity Profiler for hot path Y
```

Use **code citations** (`startLine:endLine:path`) for every non-trivial finding.

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Auditing from memory | Re-`Read` files when citing; grep hits are not proof of a bug without context |
| Declaring “clean” after shallow grep | Run targeted `Read` on hot files and cross-check `GAME_PLAN.md` rules |
| Ignoring `.meta` / asmdef graph | Broken references are P0 compile or CI failures |
| Mixing style nits with P0 correctness | Keep severity honest; move nits to “Optimizations” or omit |
| Suggesting `git commit` for this repo | Use UVCS per `docs/VERSION_CONTROL.md` when giving check-in steps |
| Full-repo audit without parallel explore subagents | Dispatch ≥2 `Task` explore subagents in one message unless the narrow-scope exception applies |

---

## Phase 1: Post-Scan Verification (Mandatory Before Finalizing)

1. **Re-read** every file you cited in P0/P1 findings — confirm line numbers, symbol names, and that the issue still applies on current branch.
2. **Downgrade or remove** findings that are dead code intentionally kept for UVCS history vs active paths (respect dead-code policy: prefer “remove” recommendation over “unused” noise).
3. **Deduplicate** multiple grep hits that map to one root cause (one finding, multiple references).
4. If the user needs **runtime proof**, state explicitly what to run in **Unity Test Runner** or Play Mode; do not invent profiler screenshots.
5. Run any **project verification** the user’s rules require (e.g. tests, linters) if available — if Unity has no CLI in this repo, say so and scope verification to static evidence.

---

## Additional resources

- Implementation planning (feature work): [writing-unity-game-dev-plans](../writing-unity-game-dev-plans/SKILL.md)
- Systematic investigation of a single failure: `superpowers:systematic-debugging`
- Evidence before “done”: `superpowers:verification-before-completion`
