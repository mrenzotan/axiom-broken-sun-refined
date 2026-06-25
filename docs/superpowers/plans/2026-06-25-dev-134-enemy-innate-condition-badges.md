# DEV-134 Enemy Innate Condition Badges Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Surface an enemy's innate material condition(s) (e.g. always-`Liquid`, always-`Vapor`) as permanent, counter-less badges in the enemy `ConditionBadgeUI` row, so players can see what an enemy is "made of" without guessing.

**Architecture:** Extract the badge-selection decision (which conditions to show, dedup, innate-vs-timed) into a new pure C# class `ConditionBadgeLogic` (mirrors the existing `SpellListPanelLogic` / `SpellInputUILogic` pattern) and EditMode-test it. `ConditionBadgeUI` (MonoBehaviour) keeps only rendering and is given the innate list to display per refresh. For multi-form enemies, `BattleController` computes the **current form's** innate conditions from live chemistry state (`GetFormIndexForConditions` → `GetInnateConditionsForForm`) and passes that to the HUD, so a temporarily-transformed form (e.g. Liquid frozen → Solid) shows the transformed material as a time-limited badge and hides the suppressed innate underneath.

**Tech Stack:** Unity 6.0.4 LTS, C#, Unity Test Framework (NUnit, EditMode), TextMeshPro, Unity UI.

## Global Constraints

- **MonoBehaviour separation (non-negotiable):** `ConditionBadgeUI` stays a thin renderer; all selection logic lives in the plain C# `ConditionBadgeLogic`. Test the plain class in EditMode.
- **No data-model changes:** Do NOT make `CharacterStats.InnateConditions` mutable, and do NOT alter the chemistry resolver or innate-condition data model (out of scope per DEV-134). `InnateConditions` remains the immutable restore-source set once at `Initialize()`.
- **Namespace:** Runtime classes in `Axiom.Battle`. New EditMode tests in `Axiom.Battle.Tests` with `[TestFixture]` (match `SpellListPanelLogicTests`).
- **Text badges only:** No icons, artwork, tooltips, or hover text (out of scope).
- **Version control:** Unity Version Control (UVCS) only — never `git`. Commit format: `<type>(DEV-134): <desc>`.
- **Editor vs code:** Claude writes all `.cs` files. The user runs Unity Test Runner and performs any Inspector wiring. No new Inspector references are required by this plan (the badge prefab/container are already wired from DEV-28/DEV-75).

---

## Design Decisions (read before starting)

These resolve the ambiguity in the DEV-134 acceptance criteria against the actual code:

1. **`CharacterStats.InnateConditions` never changes at runtime.** It is set once in `Initialize()` from `EnemyData.GetInnateConditionsForForm(0)` and is the restore-source for expiring material transformations. "Form" is **purely visual** — `BattleController._currentEnemyForm` follows `ActiveMaterialConditions` via `GetFormIndexForConditions`; it does not rewrite innate conditions.

2. **Multi-form behavior (AC #3), chosen interpretation:** badges reflect the **current form's** innate conditions. Because the only way to reach a non-zero form is via a temporary material transformation (which already renders as a time-limited badge), the current-form innate is computed live from `ActiveMaterialConditions` and then deduped against the time-limited badges. Net effect for the canonical Liquid↔Solid enemy:
   - At start / after thaw (form 0): shows `Liquid` (innate, no counter).
   - While frozen (form 1, `Solid` transform active for N turns): shows `Solid (N)` only — the suppressed `Liquid` innate is hidden. (`Solid` innate-for-form is deduped by the time-limited `Solid (N)`.)
   The current form is computed from live conditions (`GetFormIndexForConditions(ActiveMaterialConditions)`), NOT from the mutable `_currentEnemyForm` field, to avoid coupling to that field's update timing.

3. **Dedup rule (AC #4):** a condition shown as a time-limited badge is never also shown as an innate badge — time-limited takes precedence.

4. **Player panel unchanged (AC #5):** the player's `InnateConditions` is empty, so passing it through produces zero innate badges. No special-casing needed.

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Battle/UI/ConditionBadgeLogic.cs` (new) | Pure C#: the `ConditionBadge` value struct + `ConditionBadgeLogic.BuildBadges(stats, innateConditions)` selection/dedup/order logic |
| `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs` (modify) | Renderer only — calls `ConditionBadgeLogic.BuildBadges`, draws badges with/without turn counter; updated `Refresh` signature; updated class comment |
| `Assets/Scripts/Battle/BattleController.cs` (modify) | New read-only `CurrentEnemyFormInnateConditions` property computing the live current-form innate list |
| `Assets/Scripts/Battle/UI/BattleHUD.cs` (modify) | Passes the correct innate list to each panel; adds initial battle-start refresh |
| `Assets/Tests/Editor/Battle/ConditionBadgeLogicTests.cs` (new) | EditMode tests for `BuildBadges` (innate display, dedup, ordering, no-counter, null safety) |
| `Assets/Tests/Editor/Battle/EnemyDataFormConditionsTests.cs` (new) | EditMode test that the form-resolution composition returns the transformed form's innate list |

---

## Task 1: `ConditionBadgeLogic` selection logic (TDD)

**Files:**
- Create: `Assets/Scripts/Battle/UI/ConditionBadgeLogic.cs`
- Test: `Assets/Tests/Editor/Battle/ConditionBadgeLogicTests.cs`

**Interfaces:**
- Consumes: `Axiom.Battle.CharacterStats` (existing — exposes `ActiveStatusConditions` [each entry has `.Condition`, `.TurnsRemaining`, `.AppliedOrder`], `ActiveMaterialConditions`, `GetMaterialTransformTurns(ChemicalCondition)`, `GetMaterialTransformOrder(ChemicalCondition)`, `InnateConditions`); `Axiom.Data.ChemicalCondition` (existing enum).
- Produces:
  - `readonly struct Axiom.Battle.ConditionBadge` with public fields `ChemicalCondition Condition`, `int TurnsRemaining`, `bool IsInnate`, and constructor `ConditionBadge(ChemicalCondition condition, int turnsRemaining, bool isInnate)`.
  - `static List<ConditionBadge> Axiom.Battle.ConditionBadgeLogic.BuildBadges(CharacterStats stats, IReadOnlyList<ChemicalCondition> innateConditions)`.

Behavior contract for `BuildBadges`:
- `stats == null` → empty list.
- Time-limited badges = all `ActiveStatusConditions` + every `ActiveMaterialConditions` entry whose `GetMaterialTransformTurns > 0`, each carrying `(condition, turns, appliedOrder)`, sorted ascending by `appliedOrder`. Each becomes `ConditionBadge(condition, turns, isInnate: false)`.
- Innate badges = each condition in `innateConditions` (skip `null` list) that is **not** already present among the time-limited badges (dedup, time-limited wins) and not already added as an innate badge (de-duplicate the innate list itself). Each becomes `ConditionBadge(condition, turnsRemaining: 0, isInnate: true)`.
- **Order:** innate badges first (stable identity, leftmost), then time-limited badges in `appliedOrder` order. Relative order of time-limited badges is preserved (AC #6).

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/ConditionBadgeLogicTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Battle.Tests
{
    [TestFixture]
    public class ConditionBadgeLogicTests
    {
        private static CharacterStats MakeStats()
            => new CharacterStats { MaxHP = 100, MaxMP = 30, ATK = 10, DEF = 5, SPD = 8 };

        [Test]
        public void BuildBadges_NullStats_ReturnsEmpty()
        {
            var badges = ConditionBadgeLogic.BuildBadges(
                null, new List<ChemicalCondition> { ChemicalCondition.Liquid });
            Assert.That(badges, Is.Empty);
        }

        [Test]
        public void BuildBadges_InnateOnly_ShowsInnateBadgeWithNoCounter()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });

            var badges = ConditionBadgeLogic.BuildBadges(stats, stats.InnateConditions);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
            Assert.That(badges[0].IsInnate, Is.True);
            Assert.That(badges[0].TurnsRemaining, Is.EqualTo(0));
        }

        [Test]
        public void BuildBadges_NullInnateList_ShowsNoInnateBadges()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });

            var badges = ConditionBadgeLogic.BuildBadges(stats, null);

            Assert.That(badges, Is.Empty);
        }

        [Test]
        public void BuildBadges_InnatePlusStatus_InnateFirstThenTimedWithCounter()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
            stats.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5, duration: 2);

            var badges = ConditionBadgeLogic.BuildBadges(stats, stats.InnateConditions);

            Assert.That(badges.Count, Is.EqualTo(2));
            // Innate first
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
            Assert.That(badges[0].IsInnate, Is.True);
            // Then time-limited with its turn count
            Assert.That(badges[1].Condition, Is.EqualTo(ChemicalCondition.Burning));
            Assert.That(badges[1].IsInnate, Is.False);
            Assert.That(badges[1].TurnsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void BuildBadges_FrozenLiquid_HidesSuppressedInnate_ShowsOnlyTransform()
        {
            // Liquid enemy frozen into Solid: the suppressed Liquid is consumed and Solid
            // added as a temporary transformation. The current-form innate (computed by the
            // caller) is [Solid], which must be deduped by the time-limited Solid badge.
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
            stats.ConsumeCondition(ChemicalCondition.Liquid);
            stats.ApplyMaterialTransformation(
                transformsTo: ChemicalCondition.Solid,
                suppressedCondition: ChemicalCondition.Liquid,
                duration: 2);

            var currentFormInnate = new List<ChemicalCondition> { ChemicalCondition.Solid };
            var badges = ConditionBadgeLogic.BuildBadges(stats, currentFormInnate);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Solid));
            Assert.That(badges[0].IsInnate, Is.False);          // time-limited wins
            Assert.That(badges[0].TurnsRemaining, Is.EqualTo(2));
            Assert.That(badges.Any(b => b.Condition == ChemicalCondition.Liquid), Is.False);
        }

        [Test]
        public void BuildBadges_TwoInnateConditions_ShowsBoth()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition>
                { ChemicalCondition.Liquid, ChemicalCondition.Vapor });

            var badges = ConditionBadgeLogic.BuildBadges(stats, stats.InnateConditions);

            Assert.That(badges.Select(b => b.Condition),
                Is.EquivalentTo(new[] { ChemicalCondition.Liquid, ChemicalCondition.Vapor }));
            Assert.That(badges.All(b => b.IsInnate && b.TurnsRemaining == 0), Is.True);
        }

        [Test]
        public void BuildBadges_DuplicateInnateEntries_RenderedOnce()
        {
            var stats = MakeStats();
            stats.Initialize();

            var innate = new List<ChemicalCondition>
                { ChemicalCondition.Liquid, ChemicalCondition.Liquid };
            var badges = ConditionBadgeLogic.BuildBadges(stats, innate);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

> **Unity Editor task (user):** Open Window → General → Test Runner → EditMode and run the `ConditionBadgeLogicTests` fixture.
Expected: FAIL — `ConditionBadgeLogic` / `ConditionBadge` do not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Battle/UI/ConditionBadgeLogic.cs`:

```csharp
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// One badge to render: a chemical condition, its remaining turns, and whether it is
    /// a permanent innate condition. Innate badges use TurnsRemaining == 0 and render with
    /// no counter; time-limited badges carry a positive turn count.
    /// </summary>
    public readonly struct ConditionBadge
    {
        public readonly ChemicalCondition Condition;
        public readonly int TurnsRemaining;
        public readonly bool IsInnate;

        public ConditionBadge(ChemicalCondition condition, int turnsRemaining, bool isInnate)
        {
            Condition      = condition;
            TurnsRemaining = turnsRemaining;
            IsInnate       = isInnate;
        }
    }

    /// <summary>
    /// Pure selection/ordering/dedup logic for the condition badge row, extracted so it can
    /// be EditMode-tested without a scene (mirrors SpellListPanelLogic / SpellInputUILogic).
    /// </summary>
    public static class ConditionBadgeLogic
    {
        /// <summary>
        /// Builds the ordered badge list for a character.
        /// Innate badges (no turn counter) come first, then time-limited badges sorted by the
        /// order in which they were applied. A condition shown as a time-limited badge is never
        /// also shown as an innate badge (time-limited takes precedence).
        /// Pass innateConditions == null (or empty) for characters with no innate conditions
        /// (e.g. the player) to render time-limited badges only.
        /// </summary>
        public static List<ConditionBadge> BuildBadges(
            CharacterStats stats, IReadOnlyList<ChemicalCondition> innateConditions)
        {
            var result = new List<ConditionBadge>();
            if (stats == null) return result;

            // Time-limited entries: status conditions + active material transformations (turns > 0).
            var timed = new List<(ChemicalCondition condition, int turns, int order)>();

            foreach (var entry in stats.ActiveStatusConditions)
                timed.Add((entry.Condition, entry.TurnsRemaining, entry.AppliedOrder));

            foreach (var condition in stats.ActiveMaterialConditions)
            {
                int turns = stats.GetMaterialTransformTurns(condition);
                if (turns > 0)
                    timed.Add((condition, turns, stats.GetMaterialTransformOrder(condition)));
            }

            timed.Sort((a, b) => a.order.CompareTo(b.order));

            // Innate badges first, skipping any condition already represented by a time-limited
            // badge (dedup) and any innate condition already added.
            var shown = new HashSet<ChemicalCondition>();
            if (innateConditions != null)
            {
                foreach (var condition in innateConditions)
                {
                    if (TimedContains(timed, condition)) continue;
                    if (!shown.Add(condition)) continue;
                    result.Add(new ConditionBadge(condition, 0, isInnate: true));
                }
            }

            // Then the time-limited badges in applied order.
            foreach (var t in timed)
                result.Add(new ConditionBadge(t.condition, t.turns, isInnate: false));

            return result;
        }

        private static bool TimedContains(
            List<(ChemicalCondition condition, int turns, int order)> timed, ChemicalCondition condition)
        {
            foreach (var t in timed)
                if (t.condition == condition) return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

> **Unity Editor task (user):** Re-run the `ConditionBadgeLogicTests` fixture in EditMode.
Expected: PASS — all 7 tests green.

- [ ] **Step 5: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `test(DEV-134): add ConditionBadgeLogic with innate/dedup selection tests`
  - `Assets/Scripts/Battle/UI/ConditionBadgeLogic.cs`
  - `Assets/Scripts/Battle/UI/ConditionBadgeLogic.cs.meta`
  - `Assets/Tests/Editor/Battle/ConditionBadgeLogicTests.cs`
  - `Assets/Tests/Editor/Battle/ConditionBadgeLogicTests.cs.meta`

---

## Task 2: Render innate badges in `ConditionBadgeUI`

**Files:**
- Modify: `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs`

**Interfaces:**
- Consumes: `ConditionBadgeLogic.BuildBadges(CharacterStats, IReadOnlyList<ChemicalCondition>)` and the `ConditionBadge` struct from Task 1.
- Produces: new public method signature `void Refresh(CharacterStats stats, IReadOnlyList<ChemicalCondition> innateConditions)` (replaces the old `void Refresh(CharacterStats stats)`). Task 3 depends on this signature.

- [ ] **Step 1: Update the class comment and `using` directives**

In `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs`, replace the class XML-doc summary (currently lines 10–25) so it no longer claims innate conditions are hidden. Replace:

```csharp
    /// <summary>
    /// Renders a wrapping flow of colored pill badges for a character's active
    /// time-limited conditions (status conditions + temporary material transformations).
    ///
    /// Call Refresh() whenever the character's condition list may have changed.
    /// Permanent innate material conditions (e.g. always-Liquid) are not shown —
    /// only conditions with a turn countdown appear.
    ///
    /// Inspector setup required:
```

with:

```csharp
    /// <summary>
    /// Renders a wrapping flow of colored pill badges for a character's conditions:
    /// permanent innate material conditions (e.g. always-Liquid, shown with no counter)
    /// followed by time-limited conditions (status conditions + temporary material
    /// transformations, shown as "Name (turnsRemaining)").
    ///
    /// Call Refresh() whenever the character's condition list may have changed, passing the
    /// innate conditions to display (the enemy's current-form innate list, or an empty list
    /// for the player). Badge selection/dedup/ordering lives in ConditionBadgeLogic.
    ///
    /// Inspector setup required:
```

The existing `using` block already imports `System.Collections.Generic` and `Axiom.Data`; add nothing. Remove `using System.Linq;` only if it becomes unused after Step 2 (it does — see Step 2). Replace line 2 `using System.Linq;` by deleting it.

- [ ] **Step 2: Rewrite `Refresh` to use the logic and accept the innate list**

Replace the entire `Refresh` method (currently lines 41–86) with:

```csharp
        /// <summary>
        /// Clears and rebuilds the badge row from the character's current condition state.
        /// Safe to call with a null stats argument (clears the row).
        /// Pass innateConditions to render permanent innate badges (no counter); pass null or
        /// an empty list to render time-limited badges only (e.g. the player panel).
        /// Reuses pooled badge GameObjects to avoid per-refresh allocation churn.
        /// </summary>
        public void Refresh(CharacterStats stats, IReadOnlyList<ChemicalCondition> innateConditions)
        {
            if (_container == null || _badgePrefab == null)
            {
                Debug.LogError("[ConditionBadgeUI] _container or _badgePrefab is not assigned.", this);
                return;
            }

            // Deactivate all pooled badges before reuse.
            for (int i = 0; i < _badgePool.Count; i++)
            {
                if (_badgePool[i] != null)
                    _badgePool[i].SetActive(false);
            }

            if (stats == null) return;

            var badgeData = ConditionBadgeLogic.BuildBadges(stats, innateConditions);

            var badges = new List<RectTransform>(badgeData.Count);
            foreach (var data in badgeData)
                badges.Add(AcquireBadge(data));

            // Force each badge's ContentSizeFitter to compute its size before layout
            foreach (var badge in badges)
                LayoutRebuilder.ForceRebuildLayoutImmediate(badge);

            LayoutBadges(badges);
        }
```

- [ ] **Step 3: Update `AcquireBadge` / `UpdateBadge` to honor the innate (no-counter) case**

Replace `AcquireBadge` (currently lines 122–145) and `UpdateBadge` (currently lines 147–159) with:

```csharp
        /// <summary>
        /// Acquires an active badge for the given badge data, either by reusing a pooled
        /// inactive badge or instantiating a new one.
        /// </summary>
        private RectTransform AcquireBadge(ConditionBadge data)
        {
            // Search for an inactive pooled badge.
            for (int i = 0; i < _badgePool.Count; i++)
            {
                GameObject pooled = _badgePool[i];
                if (pooled != null && !pooled.activeSelf)
                {
                    pooled.SetActive(true);
                    UpdateBadge(pooled, data);
                    return pooled.GetComponent<RectTransform>();
                }
            }

            // No inactive badge available — instantiate a new one.
            GameObject badge = Instantiate(_badgePrefab, _container);
            _badgePool.Add(badge);
            UpdateBadge(badge, data);
            return badge.GetComponent<RectTransform>();
        }

        private static void UpdateBadge(GameObject badge, ConditionBadge data)
        {
            TMP_Text label = badge.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                // Innate badges show the name with no turn counter; time-limited badges
                // append the remaining turns, e.g. "Frozen (2)".
                label.text = data.IsInnate
                    ? LabelFor(data.Condition)
                    : $"{LabelFor(data.Condition)} ({data.TurnsRemaining})";
                label.ForceMeshUpdate(); // ensure TMP computes size before ForceRebuildLayoutImmediate
            }

            Image bg = badge.GetComponent<Image>();
            if (bg != null)
                bg.color = ColorFor(data.Condition);
        }
```

`LabelFor`, `ColorFor`, and `LayoutBadges` are unchanged.

- [ ] **Step 4: Verify it compiles**

> **Unity Editor task (user):** Return to the Unity Editor and let it recompile. Confirm the Console shows **no compile errors**. (`ConditionBadgeUI.Refresh` now has a 2-arg signature; the only caller is `BattleHUD`, updated in Task 3 — expect a temporary compile error there until Task 3 is done. If running Task 2 standalone, the project will not compile until Task 3 completes; proceed to Task 3 before re-running tests.)

- [ ] **Step 5: Check in via UVCS** (combine with Task 3 if executing together)
  Unity Version Control → Pending Changes → stage the file below → Check in with message: `feat(DEV-134): render innate condition badges via ConditionBadgeLogic`
  - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs`
  - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs.meta`

> Note: because Task 2 changes `Refresh`'s signature and the only caller is updated in Task 3, the project compiles cleanly only after Task 3. When executing inline, do Tasks 2 and 3 together and use a single combined check-in (see Task 3 Step 4).

---

## Task 3: Plumb the current-form innate list and add the battle-start refresh

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`

**Interfaces:**
- Consumes: `ConditionBadgeUI.Refresh(CharacterStats, IReadOnlyList<ChemicalCondition>)` from Task 2; existing `EnemyData.GetFormIndexForConditions(List<ChemicalCondition>)` and `EnemyData.GetInnateConditionsForForm(int)`.
- Produces: `BattleController.CurrentEnemyFormInnateConditions` (read-only `IReadOnlyList<ChemicalCondition>`), consumed by `BattleHUD`.

- [ ] **Step 1: Add `CurrentEnemyFormInnateConditions` to `BattleController`**

In `Assets/Scripts/Battle/BattleController.cs`, add this property near the other condition-related members (e.g. just after the `OnConditionsChanged` event declaration around line 220). It computes the current visual form from live chemistry (same source of truth the enemy sprite uses) and returns that form's innate conditions:

```csharp
        /// <summary>
        /// The innate material conditions of the enemy's CURRENT visual form, for the HUD badge
        /// row. The form is derived live from ActiveMaterialConditions (the same source the enemy
        /// sprite's form follows), so a Liquid enemy frozen into Solid reports the Solid form's
        /// innate set — which the badge UI then dedups against the active Solid transformation,
        /// hiding the suppressed Liquid while frozen. Falls back to the stats' fixed innate list
        /// when there is no EnemyData (standalone test setups).
        /// </summary>
        public IReadOnlyList<ChemicalCondition> CurrentEnemyFormInnateConditions
        {
            get
            {
                if (_enemyData == null) return _enemyStats.InnateConditions;
                int form = _enemyData.GetFormIndexForConditions(_enemyStats.ActiveMaterialConditions);
                return _enemyData.GetInnateConditionsForForm(form);
            }
        }
```

If `BattleController.cs` does not already have `using System.Collections.Generic;` and `using Axiom.Data;` at the top, add them. (Verify before adding — it references `ChemicalCondition` and `List<>` elsewhere, so both are almost certainly already imported.)

- [ ] **Step 2: Update `BattleHUD.HandleConditionsChanged` to pass the right innate list**

In `Assets/Scripts/Battle/UI/BattleHUD.cs`, replace `HandleConditionsChanged` (currently lines 266–272):

```csharp
        private void HandleConditionsChanged(CharacterStats target)
        {
            if (target == _playerStats)
                _playerConditionBadges?.Refresh(target);
            else if (target == _enemyStats)
                _enemyConditionBadges?.Refresh(target);
        }
```

with:

```csharp
        private void HandleConditionsChanged(CharacterStats target)
        {
            if (target == _playerStats)
                _playerConditionBadges?.Refresh(target, target.InnateConditions);
            else if (target == _enemyStats)
                _enemyConditionBadges?.Refresh(target, _battleController.CurrentEnemyFormInnateConditions);
        }
```

(The player's `InnateConditions` is empty, so the player panel renders time-limited badges only — unchanged from current behavior.)

- [ ] **Step 3: Add the battle-start refresh in `BattleHUD.Setup`**

`Setup` does not currently populate the badge rows, and no `OnConditionsChanged` fires at battle start — so innate badges would not appear until the first condition change. Add an initial refresh so they show from turn 0 (AC #1). In `Assets/Scripts/Battle/UI/BattleHUD.cs`, at the end of `Setup` (immediately after `_actionMenuUI.SetInteractable(false);`, currently line 100), add:

```csharp
            // Populate condition badges immediately so innate conditions are visible at battle
            // start (no OnConditionsChanged fires during init).
            _playerConditionBadges?.Refresh(playerStats, playerStats.InnateConditions);
            _enemyConditionBadges?.Refresh(enemyStats, _battleController.CurrentEnemyFormInnateConditions);
```

- [ ] **Step 4: Verify compile + run the full EditMode suite**

> **Unity Editor task (user):** Return to the Unity Editor, let it recompile, and confirm **no compile errors**. Then open Test Runner → EditMode and run the **full** suite.
Expected: all tests PASS, including `ConditionBadgeLogicTests`. Confirm no regressions in `CharacterStatsTests`, `BattleControllerSpellPhaseTests`, etc.

- [ ] **Step 5: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-134): surface enemy innate conditions in battle HUD badge row`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`
  - `Assets/Scripts/Battle/UI/BattleHUD.cs`
  - `Assets/Scripts/Battle/UI/BattleHUD.cs.meta`
  - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs` (if not already checked in via Task 2)
  - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs.meta`

---

## Task 4: Lock in the multi-form resolution with an EditMode test (TDD)

This verifies AC #3's form-resolution composition (`GetFormIndexForConditions` → `GetInnateConditionsForForm`) that `CurrentEnemyFormInnateConditions` relies on, using the real `EnemyData` methods.

**Files:**
- Test: `Assets/Tests/Editor/Battle/EnemyDataFormConditionsTests.cs`

**Interfaces:**
- Consumes: existing `Axiom.Data.EnemyData`, `EnemyFormData`, `ChemicalCondition`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Battle/EnemyDataFormConditionsTests.cs`:

```csharp
using System.Collections.Generic;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Battle.Tests
{
    [TestFixture]
    public class EnemyDataFormConditionsTests
    {
        // A Liquid (form 0) / Solid (form 1) enemy, mirroring the canonical frozen-Liquid case.
        private static EnemyData MakeTwoFormEnemy()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.innateConditions = new List<ChemicalCondition> { ChemicalCondition.Liquid };
            enemy.formDefinitions = new List<EnemyFormData>
            {
                new EnemyFormData { formIndex = 0, formName = "Liquid",
                    innateConditions = new List<ChemicalCondition> { ChemicalCondition.Liquid } },
                new EnemyFormData { formIndex = 1, formName = "Ice",
                    innateConditions = new List<ChemicalCondition> { ChemicalCondition.Solid } },
            };
            return enemy;
        }

        [Test]
        public void FormResolution_LiquidActive_ReturnsForm0InnateLiquid()
        {
            var enemy = MakeTwoFormEnemy();
            var active = new List<ChemicalCondition> { ChemicalCondition.Liquid };

            int form = enemy.GetFormIndexForConditions(active);
            var innate = enemy.GetInnateConditionsForForm(form);

            Assert.That(form, Is.EqualTo(0));
            Assert.That(innate, Is.EqualTo(new[] { ChemicalCondition.Liquid }));
        }

        [Test]
        public void FormResolution_SolidActive_ReturnsForm1InnateSolid()
        {
            var enemy = MakeTwoFormEnemy();
            var active = new List<ChemicalCondition> { ChemicalCondition.Solid };

            int form = enemy.GetFormIndexForConditions(active);
            var innate = enemy.GetInnateConditionsForForm(form);

            Assert.That(form, Is.EqualTo(1));
            Assert.That(innate, Is.EqualTo(new[] { ChemicalCondition.Solid }));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they pass**

> **Unity Editor task (user):** Run the `EnemyDataFormConditionsTests` fixture in EditMode.
Expected: PASS — these exercise existing `EnemyData` methods, so they should pass immediately (this test guards against future regressions in the form-resolution path that `CurrentEnemyFormInnateConditions` depends on). If either fails, the form data model changed — stop and reconcile before proceeding.

- [ ] **Step 3: Check in via UVCS**
  Unity Version Control → Pending Changes → stage the files below → Check in with message: `test(DEV-134): cover multi-form innate condition resolution`
  - `Assets/Tests/Editor/Battle/EnemyDataFormConditionsTests.cs`
  - `Assets/Tests/Editor/Battle/EnemyDataFormConditionsTests.cs.meta`

---

## Task 5: Play-Mode verification (user)

No automated Play-Mode test is included (rendering/layout is visual and the decision logic is fully covered in EditMode). Verify in the Battle scene instead.

- [ ] **Step 1: Verify innate badge at battle start**

> **Unity Editor task (user):** Open `Assets/Scenes/Battle.unity`, assign an enemy whose `EnemyData.innateConditions` contains a material condition (e.g. `Liquid`), enter Play Mode.
Expected: the enemy badge row shows a `Liquid` badge with **no** turn counter from the first frame, and it persists for the whole fight.

- [ ] **Step 2: Verify dedup + transform on a multi-form enemy**

> **Unity Editor task (user):** Use a Liquid/Solid multi-form enemy. Freeze it (apply the Solid transformation).
Expected while frozen: the row shows `Solid (N)` only (counter counting down); the `Liquid` badge is hidden. After the transformation expires: `Liquid` (no counter) returns.

- [ ] **Step 3: Verify the player panel is unchanged**

> **Unity Editor task (user):** Observe the player condition badge row across a fight (apply a status like Burning to the player).
Expected: only time-limited badges appear (e.g. `Burning (2)`); no innate badges — identical to pre-DEV-134 behavior.

- [ ] **Step 4: Verify ordering and pooling**

> **Unity Editor task (user):** On an enemy with an innate condition, apply two timed conditions.
Expected: innate badge appears leftmost, then the timed badges in the order applied; badges wrap correctly and reuse from the pool (no flicker/leak) across refreshes.

---

## Acceptance Criteria → Task Map

| DEV-134 Acceptance Criterion | Covered by |
|---|---|
| Innate conditions shown at battle start, persist whole fight | Task 1 (`BuildBadges` innate), Task 3 Step 3 (battle-start refresh), Task 5 Step 1 |
| Innate badges visually distinct — name, no counter, never `(0)` | Task 1 (`isInnate`/`TurnsRemaining: 0`), Task 2 Step 3 (`UpdateBadge`), Task 1 tests |
| Multi-form: reflect current form's innate, update on form change | Task 3 Step 1 (`CurrentEnemyFormInnateConditions`), Task 4 tests, Task 5 Step 2 |
| No duplicate badge — time-limited takes precedence | Task 1 (dedup), `BuildBadges_FrozenLiquid_...` test, Task 5 Step 2 |
| Player-side panel unchanged | Task 3 Step 2 (empty innate list), Task 5 Step 3 |
| Existing time-limited behavior, ordering, pool reuse preserved | Task 1 (order by `AppliedOrder`), Task 2 (pool unchanged), Task 5 Step 4 |
| Update `ConditionBadgeUI` class comment | Task 2 Step 1 |
| Out of scope: icons, tooltips, resolver/data-model changes | Not implemented (Global Constraints) |

---

## Self-Review Notes

- **Spec coverage:** every AC maps to a task (table above). The "out of scope" items are explicitly excluded.
- **Signature consistency:** `Refresh(CharacterStats, IReadOnlyList<ChemicalCondition>)`, `BuildBadges(CharacterStats, IReadOnlyList<ChemicalCondition>)`, `ConditionBadge(ChemicalCondition, int, bool)`, and `CurrentEnemyFormInnateConditions` are spelled identically everywhere they appear.
- **Guard ordering:** `BuildBadges` returns early on `stats == null` before touching `innateConditions`; `Refresh` checks `_container`/`_badgePrefab` then `stats == null` before building. `CurrentEnemyFormInnateConditions` checks `_enemyData == null` before dereferencing it.
- **UVCS file audit:** each created/modified `.cs` is paired with its `.meta` in a check-in step. No new folders or `.asmdef` files are created (all files land in existing `Assets/Scripts/Battle[/UI]` and `Assets/Tests/Editor/Battle`, which already have asmdefs).
