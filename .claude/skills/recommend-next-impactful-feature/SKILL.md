---
name: recommend-next-impactful-feature
description: Delivers exactly one highest-impact “what to build next” recommendation by reconciling the game plan, GDD/mechanics references, active implementation plans, and code reality (what exists vs stubbed). Use when the user asks for the best next feature, smartest next step, most compelling or innovative addition, roadmap priority, a single focus, or what to build next.
---

# Recommend Next Impactful Feature

## Non-negotiable output

- Recommend **one** addition or focus area unless the user explicitly asks for a ranked list.
- Name it clearly in one line, then justify it with evidence from **at least three** of: game plan, mechanics docs, plans folder, codebase.
- If the user’s wording mixes “innovative” with “impactful,” optimize for **player-visible impact and design promise fulfilled**, not novelty for its own sake.

## Before reading everything

1. If the repo has `CLAUDE.md`, read it first for authoritative paths, current phase, architecture rules, and “not yet implemented” callouts.
2. If `CLAUDE.md` disagrees with a design doc, **trust `CLAUDE.md` for repo state** and note the doc drift.

## Source map (this project)

Adjust only if `CLAUDE.md` or folder layout differs.

| Lens | Primary locations |
| ---- | ----------------- |
| Game plan / phases | `docs/GAME_PLAN.md` (master), phase exit criteria |
| GDD / ticketing scope | `docs/GAME_DESIGN_DOCUMENT.md` |
| Deep mechanics | `docs/game-mechanics/` (e.g. chemistry combat), `docs/LORE_AND_MECHANICS.md` |
| Active / historical plans | `docs/superpowers/plans/`, specs under `docs/superpowers/specs/` |
| Ad-hoc backlog | `docs/dev-notes/` |
| Quality signals | `docs/audits/` (most recent first) |

## Analysis workflow

### 1. Anchor on phase and exit criteria

Read `docs/GAME_PLAN.md` (or equivalent) and identify:

- Declared **current phase** and **exit criteria**
- Dependencies: what is explicitly blocked until something else ships
- Cut/deferred items (do not recommend reviving unless the user asks)

### 2. Reconcile GDD and mechanics with reality

Skim `docs/GAME_DESIGN_DOCUMENT.md` and targeted mechanics docs **only** if the candidate feature touches spells, combat conditions, voice, progression, or narrative systems.

Extract:

- Features that are **promised to players** but not yet credible in-game
- Systems that need **vertical slice** proof (one spell, one enemy, one loop) before expanding content

### 3. Scan plans for momentum and duplication

In `docs/superpowers/plans/`:

- Prefer **recent** dated plans and any plan the user or ticket title references in-chat
- Cluster themes (e.g. bridge, persistence, voice robustness, content roster)
- Discard recommendations that duplicate an **already detailed plan** unless the recommendation is “execute plan X next”

### 4. Ground-truth in code (lightweight, high signal)

Without a full audit, confirm **existence and wiring** of the systems your recommendation depends on:

- Entry scenes, managers, and cross-scene state mentioned in `CLAUDE.md`
- Recognizers, controllers, and data-driven assets paths (`Assets/Scripts/`, `Assets/Data/`)
- Search selectively: `TODO`, `FIXME`, `NotImplemented`, `throw new`, obvious stubs, empty handlers, scenes listed but unused

If Unity Test Runner is mentioned in project docs, cite **whether tests exist** for the risky area; do not claim tests pass without running them.

### 5. Score candidates, then collapse to one

Score each serious candidate 1–5 (mental math is fine) on:

| Criterion | Question |
| --------- | -------- |
| **Unlock** | Does it unblock the next phase or multiple downstream features? |
| **Player value** | Is the payoff visible in a play session soon? |
| **Risk** | Technical, UX, or dependency risk vs reward? |
| **Cohesion** | Does it strengthen the core fantasy (e.g. voice-spell identity) vs peripheral polish? |
| **Doc alignment** | Does it close the biggest plan-vs-code gap?

Pick the top composite score. If two tie, prefer **phase exit criteria** and **dependency unlock** over raw novelty.

## Response template

Use this structure in the chat reply:

```markdown
## Single recommendation
**Build next:** [One concrete feature or initiative name]

## Why this wins
- **Game plan / phase:** [cite section or criterion]
- **Mechanics / GDD:** [cite doc + what player expectation it satisfies]
- **Code / repo reality:** [what is missing, stubbed, or fragile — cite files or symbols when known]
- **Plans / momentum:** [relevant plan filenames or themes]

## What “done” looks like
- [3–5 verifiable outcomes, including edge cases if critical]

## Intentionally not recommending (brief)
- [1–3 alternatives and one-line reason each: wrong phase, duplicate plan, low unlock, etc.]
```

## Guardrails

- Do not recommend large content drops (full rosters, many levels) when **core loop or bridge systems** fail exit criteria for the current phase.
- Respect documented **non-negotiables** (e.g. architecture constraints in `CLAUDE.md`).
- If information is missing, state the **smallest read or grep** that would resolve uncertainty; still commit to one recommendation with labeled assumptions.

## Optional deep dive

For chemistry combat, spells, or enemies, read the authoritative mechanics doc referenced from `CLAUDE.md` before arguing for combat-adjacent work.
