# DEV-121 — Improve Spell Tutorial Action Control

**Date:** 2026-06-24
**Jira:** [DEV-121](https://axiombrokensunrefined.atlassian.net/browse/DEV-121) (Story, Epic DEV-52 Phase 7: Polish & Release Prep)
**Status:** Design approved — pending implementation plan

## Problem

During the `SpellTutorial` mode in `Level_1-1`, turn 2 is meant to teach the player to
cast `Freeze`. The current tutorial unlocks the Spell button and shows the prompt
"Press Spell, then say 'Freeze' aloud" — but it does **not** actually constrain the
player to that action. Two deviations are currently possible and both break the flow:

1. **Wrong spell by voice.** Combat casting is voice-only (Vosk push-to-talk). Any
   *unlocked* spell the recognizer hears (`Combust`, `Neutralize`) is accepted and cast,
   spending MP and consuming the turn.
2. **Wrong button.** Attack/Item/Flee remain interactable on turn 2. Clicking **Attack**
   consumes the turn and advances to turn 3, whose prompt "Strike while it's Solid!" is
   nonsensical because the enemy was never Frozen.

Either deviation pushes the tutorial into an inconsistent state. The ticket requires the
player be funneled to exactly one action — cast `Freeze` — and that normal access be
restored afterward.

### Key constraint discovered during exploration

Combat casting is **voice-only**. There are no per-spell cast buttons to "hide" or
"disable" — the Spell button merely enters the voice phase, and `SpellListPanelUI` is a
read-only reference list. So the ticket's "disable/hide Combust & Neutralize" is enforced
on the **voice-acceptance path**, not on per-spell UI controls.

## Approach

**Soft-reject + coach** for voice input, and **disable non-Freeze action buttons** for the
restricted turn.

- Soft-reject (chosen over hard grammar rebuild): the threaded Vosk recognizer and its
  grammar are left untouched. The restriction is enforced after recognition, before MP is
  spent, reusing the existing `OnSpellCastRejected` feedback path. No async recognizer
  rebuild, no grammar to restore.
- Disable Attack/Item/Flee on the restricted turn (chosen over coaching after a wrong
  click): prevention beats correction. A coaching message after an Attack is too late —
  the turn is already consumed. This reuses the tutorial's existing per-button lock pattern
  (already used on turn 1, where Spell is locked to funnel the player to Attack).

Rejected alternatives:
- **Hard-block grammar** (rebuild Vosk grammar to `["freeze"]` and restore after): purer
  but adds async recognizer rebuild/restore on the background thread, latency, and
  restore-on-edge-case complexity for no UX gain over soft-reject.
- **Spell List panel grey-out**: the panel is read-only reference and does not cast;
  greying it out adds UI rewiring without enforcing anything. Out of scope.

## Components

All new logic lives in plain C# classes per the project's "MonoBehaviours = lifecycle only"
standard.

### 1. `TutorialSpellGate` (new, plain C#)
Holds the active restriction and answers whether a cast is permitted.
- State: a case-insensitive set of allowed spell names + a coaching message. An **empty
  set means unrestricted**.
- `bool IsAllowed(string spellName)` — true when unrestricted or when `spellName` is in the
  allowed set.
- `string RejectionMessage` — the coaching text shown when a cast is rejected.
- Logic-only, so it is EditMode-testable.

### 2. `BattleTutorialAction` (extend existing struct)
Add a nullable `SpellRestriction` field carrying allowed names + coaching message.
- `null` → no change this step (consistent with the struct's existing nullable-field
  convention for button states).
- non-null with names → activate that restriction.
- non-null **empty** → clear the restriction (restore normal access).

### 3. `BattleTutorialFlow.OnPlayerTurnStarted` (edit existing turn cases)
- **Turn 2:** `spellInteractable: true`, `attackInteractable: false`,
  `itemInteractable: false`, `fleeInteractable: false`, and
  `SpellRestriction = { allowed: ["freeze"], message: "The tutorial needs Freeze — say 'Freeze' aloud." }`.
- **Turn 3:** restore all buttons (Spell already restored today) and
  `SpellRestriction = empty` (clear).

### 4. `BattleTutorialController.Apply` (extend)
When `action.SpellRestriction` is non-null, push it into `BattleController` (which owns the
gate). Follows the existing pattern where `Apply` maps action fields onto UI/controller
calls.

### 5. `BattleController.OnSpellCast` (add one guard)
After a spell is recognized but **before MP is deducted**: if the gate is active and the
recognized spell is not allowed, fire the existing `OnSpellCastRejected(gate.RejectionMessage)`
and return early.
- No MP spent, turn not consumed.
- Player returns to the action menu where only Spell is enabled, and can retry.
- Because the restriction is scoped to the tutorial turn state (not a one-shot UI event),
  this is robust to delayed input and to reopening the spell selection UI.

## Flow after change

1. Turn 2 begins → only Spell enabled; gate = `{freeze}`.
2. Player says "Combust" → recognized → gate rejects → coaching message shown, no MP spent,
   still on turn 2.
3. Player says "Freeze" → allowed → cast resolves → enemy Frozen/Solid → turn advances.
4. Turn 3 → gate cleared, all buttons restored → "Strike while it's Solid!" is now valid.

## Acceptance criteria mapping

| AC | Satisfied by |
| --- | --- |
| Player restricted to intended action when prompted | Turn-2 button locks + gate |
| Turn 2 only allows `Freeze` | Gate = `{freeze}` + `OnSpellCast` guard |
| `Combust`/`Neutralize` disabled/blocked | Soft-reject via gate (voice-only path) |
| Tutorial clearly communicates the action | Existing turn-2 prompt + coaching reject message |
| Player cannot deviate via wrong spell or wrong button | Gate (voice) + Attack/Item/Flee disabled |
| Normal availability restored after restriction | Turn-3 action clears gate + restores buttons |
| Robust to delayed input / reopening spell UI | Restriction scoped to turn state, not a UI event |

## Testing (EditMode, pure C#)

- `TutorialSpellGate`: allows `freeze`; rejects `combust`/`neutralize`; empty set allows
  all; case-insensitive match.
- `BattleTutorialFlow`: turn-2 action restricts to `freeze` **and** disables
  attack/item/flee; turn-3 action clears the restriction **and** restores all buttons.
  Tests encode *why*: deviation must be impossible mid-tutorial and access must be restored
  after.

## Out of scope (YAGNI)

- Vosk grammar rebuild / restore.
- `SpellListPanelUI` grey-out of restricted spells.
- Generalized multi-step tutorial restriction DSL — only the turn-2 case this ticket needs.

## Manual verification (Unity Editor)

Play `Level_1-1` in `SpellTutorial` mode and confirm: turn 2 allows only Spell; saying a
non-Freeze spell is rejected with the coaching message and no MP loss; Attack/Item/Flee are
not clickable; casting Freeze advances correctly; turn 3 restores all actions.
