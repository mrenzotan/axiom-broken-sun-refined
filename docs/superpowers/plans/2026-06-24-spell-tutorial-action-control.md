# Spell Tutorial Action Control (DEV-121) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. For this Unity project also use `executing-unity-game-dev-plans` (batched tasks, UVCS check-ins, Editor handoffs). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** During the `Level_1-1` SpellTutorial, funnel the player to exactly one action on the Freeze turn — only `Freeze` is castable and only the Spell button is clickable — then restore full access afterward.

**Architecture:** Add a pure-C# `TutorialSpellGate` carried inside the existing `BattleTutorialAction` and stored on `BattleController`. The Freeze turn's tutorial action (a) disables Attack/Item/Flee, leaving only Spell, and (b) sets the gate to `{freeze}`. `BattleController.OnSpellCast` consults the gate **before spending MP**; a disallowed spell is soft-rejected via the existing `OnSpellCastRejected` path (no MP spent, turn not consumed). The next turn clears the gate and restores buttons. No Vosk grammar rebuild.

**Tech Stack:** Unity 6.0.4 LTS, C#, NUnit EditMode tests (Unity Test Framework), UVCS for version control.

## Global Constraints

- MonoBehaviours handle Unity lifecycle only; all logic in plain C# classes (GAME_PLAN architecture standard). `TutorialSpellGate` is plain C#.
- No new static singletons (only `GameManager`). The gate is an instance field on `BattleController`.
- Spell identity is the lowercase `SpellData.spellName` ("freeze", "combust", "neutralize"). Gate matching is case-insensitive.
- Version control is **UVCS only** — never `git add`/`git commit`. Check in via Unity Version Control → Pending Changes.
- Commit message format: `<type>(DEV-121): <short description>` (no Co-Authored-By).
- EditMode tests live in `Assets/Tests/Editor/Battle/` under the existing `BattleTests` asmdef (already references `Axiom.Battle`, `Axiom.Core`, `Axiom.Data`). No new asmdef needed.
- Coaching/reject copy, verbatim: `The tutorial needs Freeze — say 'Freeze' aloud.`
- Tests are run by the user in Unity Editor → Window → General → Test Runner → EditMode (no test CLI in this project).

---

### Task 1: `TutorialSpellGate` (pure C# restriction holder)

**Files:**
- Create: `Assets/Scripts/Battle/TutorialSpellGate.cs`
- Test: `Assets/Tests/Editor/Battle/TutorialSpellGateTests.cs`

**Interfaces:**
- Produces:
  - `class TutorialSpellGate` in namespace `Axiom.Battle`
  - `TutorialSpellGate(IEnumerable<string> allowedSpellNames, string rejectionMessage)`
  - `static readonly TutorialSpellGate TutorialSpellGate.Unrestricted`
  - `bool IsAllowed(string spellName)`
  - `string RejectionMessage { get; }`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/TutorialSpellGateTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Battle;

namespace Axiom.Battle.Tests
{
    public class TutorialSpellGateTests
    {
        [Test]
        public void Unrestricted_AllowsAnySpell()
        {
            TutorialSpellGate gate = TutorialSpellGate.Unrestricted;
            Assert.IsTrue(gate.IsAllowed("combust"));
            Assert.IsTrue(gate.IsAllowed("neutralize"));
        }

        [Test]
        public void Unrestricted_AllowsNullName_BecauseRestrictionIsAbsent()
        {
            // An unrestricted gate does not care about the parameter — the empty
            // allow-set short-circuits before the null check.
            Assert.IsTrue(TutorialSpellGate.Unrestricted.IsAllowed(null));
        }

        [Test]
        public void EmptyAllowList_IsTreatedAsUnrestricted()
        {
            var gate = new TutorialSpellGate(new List<string>(), "msg");
            Assert.IsTrue(gate.IsAllowed("combust"));
        }

        [Test]
        public void RestrictedToFreeze_AllowsFreeze_CaseInsensitive()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "msg");
            Assert.IsTrue(gate.IsAllowed("freeze"));
            Assert.IsTrue(gate.IsAllowed("Freeze"));
            Assert.IsTrue(gate.IsAllowed("FREEZE"));
        }

        [Test]
        public void RestrictedToFreeze_RejectsOtherSpells()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "msg");
            Assert.IsFalse(gate.IsAllowed("combust"));
            Assert.IsFalse(gate.IsAllowed("neutralize"));
        }

        [Test]
        public void RestrictedGate_RejectsNullOrEmptyName()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "msg");
            Assert.IsFalse(gate.IsAllowed(null));
            Assert.IsFalse(gate.IsAllowed(""));
        }

        [Test]
        public void RejectionMessage_IsExposed()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "say Freeze");
            Assert.AreEqual("say Freeze", gate.RejectionMessage);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Unity Editor → Test Runner → EditMode → run `TutorialSpellGateTests`.
Expected: compile error / all FAIL — `TutorialSpellGate` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Battle/TutorialSpellGate.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Axiom.Battle
{
    /// <summary>
    /// Restricts which spells the player may cast during a scripted tutorial step.
    /// Carried inside <see cref="BattleTutorialAction"/> and stored on BattleController,
    /// which consults it in OnSpellCast before spending MP. An empty allow-set means
    /// "no restriction". Pure C# so it is EditMode-testable per the MonoBehaviour-separation
    /// standard (CLAUDE.md).
    /// </summary>
    public sealed class TutorialSpellGate
    {
        private readonly HashSet<string> _allowed;

        /// <summary>Shared "anything goes" gate. Use it to clear a restriction.</summary>
        public static readonly TutorialSpellGate Unrestricted = new TutorialSpellGate(null, null);

        public TutorialSpellGate(IEnumerable<string> allowedSpellNames, string rejectionMessage)
        {
            _allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (allowedSpellNames != null)
            {
                foreach (string name in allowedSpellNames)
                    if (!string.IsNullOrEmpty(name)) _allowed.Add(name);
            }
            RejectionMessage = rejectionMessage;
        }

        public string RejectionMessage { get; }

        /// <summary>
        /// True when unrestricted (empty allow-set), or when <paramref name="spellName"/> is
        /// in the allow-set (case-insensitive). The empty-set check comes first so an
        /// unrestricted gate never rejects, regardless of the argument.
        /// </summary>
        public bool IsAllowed(string spellName)
        {
            if (_allowed.Count == 0) return true;
            if (string.IsNullOrEmpty(spellName)) return false;
            return _allowed.Contains(spellName);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Unity Editor → Test Runner → EditMode → run `TutorialSpellGateTests`.
Expected: all 7 tests PASS.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-121): add TutorialSpellGate spell restriction`
- `Assets/Scripts/Battle/TutorialSpellGate.cs`
- `Assets/Scripts/Battle/TutorialSpellGate.cs.meta`
- `Assets/Tests/Editor/Battle/TutorialSpellGateTests.cs`
- `Assets/Tests/Editor/Battle/TutorialSpellGateTests.cs.meta`

---

### Task 2: Carry the gate in `BattleTutorialAction` and set/clear it in `BattleTutorialFlow`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleTutorialAction.cs`
- Modify: `Assets/Scripts/Battle/BattleTutorialFlow.cs`
- Test: `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs` (modify)

**Interfaces:**
- Consumes: `TutorialSpellGate` (Task 1).
- Produces:
  - `BattleTutorialAction.SpellGate` (`TutorialSpellGate`, null = no change).
  - New ctor param `TutorialSpellGate spellGate = null` (appended last).
  - `BattleTutorialFlow.OnPlayerTurnStarted()` turn 2 → Attack/Item/Flee disabled, Spell enabled, `SpellGate = {freeze}`; turn 3+ → all-but-Item/Flee restored, `SpellGate = Unrestricted`.

- [ ] **Step 1: Write/adjust the failing tests**

In `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs`:

(a) **Update** the existing `SpellTutorial_PlayerTurn2_UnlocksSpellAndPromptsCast` test — Attack must now be **disabled** on turn 2. Replace its body assertions with:

```csharp
        [Test]
        public void SpellTutorial_PlayerTurn2_UnlocksSpellAndPromptsCast()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();              // turn 1
            flow.OnPlayerAttackImmune();             // attack bounced
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 2
            StringAssert.Contains("freeze", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.SpellInteractable, "Spell button must unlock at turn 2.");
            Assert.IsFalse(a.AttackInteractable, "Attack must be disabled so the player cannot deviate from casting Freeze.");
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }
```

(b) **Add** these tests:

```csharp
        [Test]
        public void SpellTutorial_PlayerTurn2_RestrictsCastingToFreezeOnly()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 2
            Assert.IsNotNull(a.SpellGate, "Turn 2 must install a spell restriction.");
            Assert.IsTrue(a.SpellGate.IsAllowed("freeze"));
            Assert.IsFalse(a.SpellGate.IsAllowed("combust"), "Combust must be blocked on the Freeze turn.");
            Assert.IsFalse(a.SpellGate.IsAllowed("neutralize"), "Neutralize must be blocked on the Freeze turn.");
        }

        [Test]
        public void SpellTutorial_PlayerTurn3_ClearsSpellRestrictionAndRestoresAttack()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();          // turn 2
            flow.OnSpellCast(spellName: "freeze");
            flow.OnConditionsChanged();
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 3
            Assert.IsNotNull(a.SpellGate, "Turn 3 must explicitly clear the restriction.");
            Assert.IsTrue(a.SpellGate.IsAllowed("combust"), "Full spell access must be restored after the Freeze turn.");
            Assert.IsTrue(a.AttackInteractable, "Attack must be re-enabled to 'Strike while it's Solid'.");
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Unity Editor → Test Runner → EditMode → run `BattleTutorialFlowTests`.
Expected: the updated `...UnlocksSpellAndPromptsCast` and the two new tests FAIL — `SpellGate` member does not exist; turn 2 still returns `attackInteractable: true`.

- [ ] **Step 3a: Add the `SpellGate` field to `BattleTutorialAction`**

In `Assets/Scripts/Battle/BattleTutorialAction.cs`, add the property and ctor param (appended last so existing positional usage is unaffected):

```csharp
    public readonly struct BattleTutorialAction
    {
        public string PromptText        { get; }
        public bool? AttackInteractable { get; }
        public bool? SpellInteractable  { get; }
        public bool? ItemInteractable   { get; }
        public bool? FleeInteractable   { get; }
        public bool MarkComplete        { get; }
        public TutorialSpellGate SpellGate { get; }

        public BattleTutorialAction(
            string promptText = null,
            bool? attackInteractable = null,
            bool? spellInteractable = null,
            bool? itemInteractable = null,
            bool? fleeInteractable = null,
            bool markComplete = false,
            TutorialSpellGate spellGate = null)
        {
            PromptText = promptText;
            AttackInteractable = attackInteractable;
            SpellInteractable  = spellInteractable;
            ItemInteractable   = itemInteractable;
            FleeInteractable   = fleeInteractable;
            MarkComplete       = markComplete;
            SpellGate          = spellGate;
        }

        public static readonly BattleTutorialAction NoChange = new BattleTutorialAction();
    }
```

Also update the struct's XML doc comment to add one line: `SpellGate: null = no change; an Unrestricted gate clears any active restriction.`

- [ ] **Step 3b: Set/clear the gate and disable Attack on the Freeze turn in `BattleTutorialFlow`**

In `Assets/Scripts/Battle/BattleTutorialFlow.cs`:

Add a prompt/reject constant alongside the other SpellTutorial constants (after line 27):

```csharp
        private const string SpellTutorial_FreezeOnlyReject = "The tutorial needs Freeze — say 'Freeze' aloud.";
```

Replace the turn-2 and turn-3 branches in `OnPlayerTurnStarted` (currently lines 105–122) with:

```csharp
                    if (_spell_playerTurnsObserved == 2)
                    {
                        // Turn 2: unlock Spell, lock every other action, and restrict casting
                        // to Freeze only. Disabling Attack/Item/Flee is what stops the player
                        // deviating into the nonsensical turn-3 "Strike while Solid" state.
                        return new BattleTutorialAction(
                            promptText: SpellTutorial_PressSpellFreeze,
                            attackInteractable: false,
                            spellInteractable: true,
                            itemInteractable: false,
                            fleeInteractable: false,
                            spellGate: new TutorialSpellGate(new[] { "freeze" }, SpellTutorial_FreezeOnlyReject));
                    }
                    // Turn 3+: post-Freeze world. Strike while Solid. Restore Attack and clear
                    // the spell restriction so normal access resumes.
                    _spell_postFreezeTurnReached = true;
                    return new BattleTutorialAction(
                        promptText: SpellTutorial_StrikeWhileSolid,
                        attackInteractable: true,
                        spellInteractable: true,
                        itemInteractable: false,
                        fleeInteractable: false,
                        spellGate: TutorialSpellGate.Unrestricted);
```

- [ ] **Step 4: Run the tests to verify they pass**

Unity Editor → Test Runner → EditMode → run `BattleTutorialFlowTests`.
Expected: all tests PASS (including the updated turn-2 test and the two new tests).

> Note: the existing `SpellTutorial_AttackOnTurn2_RefiresLiquidBlocksPrompt` test still passes — it calls `OnPlayerAttackImmune()` directly and does not depend on button state. It now documents that the flow stays correct even if that method is ever reached defensively. Leave it unchanged (surgical scope).

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-121): restrict SpellTutorial turn 2 to Freeze and lock other actions`
- `Assets/Scripts/Battle/BattleTutorialAction.cs`
- `Assets/Scripts/Battle/BattleTutorialFlow.cs`
- `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs`

(No `.meta` entries: these files already exist and their `.meta` is unchanged.)

---

### Task 3: Enforce the gate in `BattleController` and wire it from `BattleTutorialController`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs` (add field near line 241; add guard in `OnSpellCast` after line 577 / before line 579; add public setter)
- Modify: `Assets/Scripts/Battle/BattleTutorialController.cs` (extend `Apply`, ~line 164–170)
- Test: `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs` (add a test)

**Interfaces:**
- Consumes: `TutorialSpellGate` (Task 1), `BattleTutorialAction.SpellGate` (Task 2).
- Produces:
  - `BattleController.SetTutorialSpellGate(TutorialSpellGate gate)` — null clears to `Unrestricted`.
  - `BattleController.OnSpellCast` rejects a disallowed spell via `OnSpellCastRejected(gate.RejectionMessage)` before spending MP.

- [ ] **Step 1: Write the failing test**

In `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs`, add (the file already has `SetField`/`GetField` reflection helpers and the `SetUp` that creates `_controller`):

```csharp
        [Test]
        public void OnSpellCast_TutorialRestrictsToFreeze_RejectsOtherSpellWithoutSpendingTurn()
        {
            var bm = new BattleManager();
            bm.StartBattle(CombatStartState.Advantaged); // → PlayerTurn

            SetField(_controller, "_battleManager", bm);
            SetField(_controller, "_isAwaitingVoiceSpell", true);
            SetField(_controller, "_isProcessingAction", true);

            _controller.SetTutorialSpellGate(
                new TutorialSpellGate(new[] { "freeze" }, "The tutorial needs Freeze — say 'Freeze' aloud."));

            string rejected = null;
            _controller.OnSpellCastRejected += msg => rejected = msg;

            var combust = ScriptableObject.CreateInstance<SpellData>();
            combust.spellName = "combust";
            combust.mpCost = 6;

            _controller.OnSpellCast(combust);

            Assert.AreEqual("The tutorial needs Freeze — say 'Freeze' aloud.", rejected,
                "Casting a non-Freeze spell during the restricted tutorial turn must be rejected with the coaching message.");
            Assert.IsFalse((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
                "Rejection must exit the voice spell phase so the player returns to the action menu and can retry.");

            Object.DestroyImmediate(combust);
        }
```

(The allowed/no-restriction path is the pre-existing `OnSpellCast` behavior — unchanged by this task and already covered by `TutorialSpellGateTests` — so it is verified in Play Mode (Task 4), not duplicated here.)

- [ ] **Step 2: Run the test to verify it fails**

Unity Editor → Test Runner → EditMode → run `BattleControllerSpellPhaseTests`.
Expected: `OnSpellCast_TutorialRestrictsToFreeze_...` FAILS to compile — `SetTutorialSpellGate` does not exist.

- [ ] **Step 3a: Add the gate field and setter to `BattleController`**

In `Assets/Scripts/Battle/BattleController.cs`, near the other private state fields (next to `private bool _isAwaitingVoiceSpell;` at line 241), add:

```csharp
        private TutorialSpellGate _tutorialSpellGate = TutorialSpellGate.Unrestricted;
```

Add this public method (place it just above `public void OnSpellCast(SpellData spell)` at line 574):

```csharp
        /// <summary>
        /// Sets the active tutorial spell restriction. BattleTutorialController calls this when a
        /// tutorial step restricts (or clears) which spells the player may cast. Null clears it.
        /// Outside a tutorial the gate stays Unrestricted, so OnSpellCast behaves normally.
        /// </summary>
        public void SetTutorialSpellGate(TutorialSpellGate gate)
        {
            _tutorialSpellGate = gate ?? TutorialSpellGate.Unrestricted;
        }
```

- [ ] **Step 3b: Add the gate guard in `OnSpellCast`**

In `OnSpellCast`, insert the guard immediately after the `_isAwaitingVoiceSpell` guard (line 577) and **before** the `SpendMP` call (line 579), so no MP is spent on a disallowed spell:

```csharp
        public void OnSpellCast(SpellData spell)
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (!_isAwaitingVoiceSpell) return;

            if (!_tutorialSpellGate.IsAllowed(spell.spellName))
            {
                // Mirrors the insufficient-MP path: exit the spell phase, do NOT spend MP,
                // do NOT advance the turn. The player drops back to the action menu (where
                // only Spell is enabled during the tutorial) and can retry.
                SetAwaitingVoiceSpell(false);
                _isProcessingAction = false;
                OnSpellPhaseExited?.Invoke();
                OnSpellCastRejected?.Invoke(_tutorialSpellGate.RejectionMessage);
                OnSpellChargeAborted?.Invoke();
                Debug.Log($"[Battle] Spell rejected by tutorial — {spell.spellName} not allowed this step.");
                return;
            }

            if (!_playerStats.SpendMP(spell.mpCost))
            {
                // ... existing insufficient-MP block unchanged ...
```

(Leave the rest of `OnSpellCast` exactly as-is.)

- [ ] **Step 3c: Push the gate from `BattleTutorialController.Apply`**

In `Assets/Scripts/Battle/BattleTutorialController.cs`, inside `Apply` (after the `_actionMenu` button block, before the `MarkComplete` block — i.e. after line 170), add:

```csharp
            if (action.SpellGate != null && _battleController != null)
                _battleController.SetTutorialSpellGate(action.SpellGate);
```

- [ ] **Step 4: Run the tests to verify they pass**

Unity Editor → Test Runner → EditMode → run `BattleControllerSpellPhaseTests` and the full Battle suite.
Expected: the new gate tests PASS; all pre-existing Battle tests still PASS.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-121): enforce tutorial spell gate in BattleController`
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/BattleTutorialController.cs`
- `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs`

(No `.meta` entries: these files already exist and their `.meta` is unchanged.)

---

### Task 4: Manual Play Mode verification in `Level_1-1`

No code. The C# scripts contain no new serialized fields, so **no Inspector rewiring is required** — `BattleTutorialController` already holds the `_battleController` reference it needs.

- [ ] **Step 1: Reset tutorial flags so the SpellTutorial will run**

> **Unity Editor task (user):** Ensure `PlayerState.HasCompletedSpellTutorialBattle` is false for this playthrough (fresh save, or clear the persisted flag) so `BattleTutorialController.ResolveMode` activates `SpellTutorial`.

- [ ] **Step 2: Play the SpellTutorial battle and verify each AC**

> **Unity Editor task (user):** Enter Play Mode, trigger the `Level_1-1` Meltspawn spell tutorial, and confirm:
> 1. **Turn 1:** Spell is locked; attacking shows the "Liquid blocks physical" prompt.
> 2. **Turn 2:** Only the **Spell** button is clickable — Attack, Item, Flee are greyed out. Prompt reads "Press Spell, then say 'Freeze' aloud."
> 3. **Turn 2 wrong spell:** Press Spell, say "Combust" (and "Neutralize"). Each is rejected with "The tutorial needs Freeze — say 'Freeze' aloud.", **no MP is lost**, and the turn does **not** advance.
> 4. **Turn 2 reopen:** Cancel/reopen the spell prompt, delay, then say a wrong spell again — still blocked (restriction is not bypassed by reopening).
> 5. **Turn 2 correct:** Say "Freeze" — it casts, enemy becomes Frozen/Solid, turn advances.
> 6. **Turn 3:** All actions restored; "Strike while it's Solid!" is shown and Attack works; casting Combust/Neutralize is now accepted (restriction cleared).

- [ ] **Step 3: Update Jira**

> **Unity Editor task (user):** If all checks pass, move DEV-121 to Done (or your review column) and note the verification result in a comment.

---

## Self-Review

**1. Spec coverage** — every spec requirement maps to a task:

| Spec requirement | Task |
| --- | --- |
| Turn 2 restricted to `Freeze` (voice) | Task 2 gate `{freeze}` + Task 3 `OnSpellCast` guard |
| `Combust`/`Neutralize` blocked | Task 2 + Task 3 (soft-reject) |
| Cannot deviate via wrong button | Task 2 (Attack/Item/Flee disabled on turn 2) |
| Clearly communicate intended action | Existing turn-2 prompt + Task 2 coaching reject message |
| Restore normal access after restriction | Task 2 turn-3 `Unrestricted` gate + buttons restored |
| Robust to delayed input / reopening spell UI | Gate scoped to turn state, enforced in `OnSpellCast` (Task 3) |
| No MP loss / turn loss on wrong cast | Task 3 guard placed before `SpendMP`, mirrors MP-reject path |

**2. Placeholder scan** — no TBD/TODO/"handle edge cases"; every code step has complete code.

**3. Type consistency** — `TutorialSpellGate(IEnumerable<string>, string)`, `IsAllowed(string)`, `RejectionMessage`, `Unrestricted`, `BattleTutorialAction.SpellGate`, and `BattleController.SetTutorialSpellGate(TutorialSpellGate)` are spelled identically across Tasks 1–3. Ctor param `spellGate` appended last preserves existing positional `BattleTutorialAction` call sites.

**4. Guard ordering** — `IsAllowed` checks the empty allow-set (unrestricted) before the null-name check, so an unrestricted gate never rejects. The `OnSpellCast` gate guard sits after the PlayerTurn/awaiting guards and before `SpendMP`.

**5. UVCS staged-file audit** — new files (`TutorialSpellGate.cs`, `TutorialSpellGateTests.cs`) list both `.cs` and `.cs.meta`; modified files list only the `.cs` (their `.meta` is unchanged and will not appear in Pending Changes). No new folders or asmdefs created.

> **Note on external docs:** No Context7/Exa research was needed — this feature introduces no new Unity API surface. It reuses existing patterns already in the codebase: NUnit `[Test]`, `ScriptableObject.CreateInstance<SpellData>()`, reflection field injection (`SetField`/`GetField` in `BattleControllerSpellPhaseTests`), and the established `BattleTutorialAction`/event conventions.
