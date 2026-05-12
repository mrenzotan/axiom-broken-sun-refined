# DEV-86 Per-Enemy SpellVFX Offset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for the Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Let each enemy nudge the SpellVFX spawn position so Damage-spell visuals land on the enemy sprite's visual center instead of its transform origin. Default offset `(0, 0)` keeps current behavior unchanged for every existing enemy until an artist tunes it.

**Architecture:** Add a `Vector2 spellVfxOffset` field to `EnemyData` (ScriptableObject). In `BattleController.FireSpellVisuals()`, add the offset (cast to `Vector3`, Z preserved) to the enemy transform position when computing `vfxPosition` for Damage spells. Heal/Shield path is untouched — player-side offset is explicitly out of scope per the Jira ticket. No new files outside the test, no new asmdef, no MonoBehaviour added.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, Mono scripting backend, Unity Test Framework (Edit Mode, NUnit), Unity Version Control (UVCS) for check-ins.

**Jira:** [DEV-86](https://axiombrokensunrefined.atlassian.net/browse/DEV-86) — "Add per-enemy X/Y offset for SpellVFX spawn position"

**Project conventions to honor:**

- **MonoBehaviours own Unity lifecycle only.** `BattleController` is the existing wiring layer; this plan adds one expression to one method, no new logic class.
- **No new singletons / no premature abstraction.** No helper class, no event channel — direct field read on the existing `_enemyData` reference.
- **`_enemyData` is optional.** `BattleController.cs:58–59` documents that `_enemyData` may be null in standalone testing. The new offset read MUST null-guard before dereferencing.
- **UVCS commit format:** `<type>(DEV-86): <short description>` — no `Co-Authored-By` footer.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Data/EnemyData.cs` | **Modified** | Add `public Vector2 spellVfxOffset` field with `[Tooltip]` and default `Vector2.zero`. |
| `Assets/Scripts/Battle/BattleController.cs` | **Modified (one method body)** | In `FireSpellVisuals()` (around lines 575–581), add the null-guarded enemy offset to the Damage-spell `vfxPosition`. |
| `Assets/Tests/Editor/Data/EnemyDataTests.cs` | **Modified (append tests)** | Edit-Mode NUnit fixture already exists with `battleVisualPrefab` reflection tests. Append four `spellVfxOffset` tests: default `Vector2.zero`, round-trip, `[Tooltip]` present, field type is `Vector2`. |

No new folders, no new `.asmdef`, no new MonoBehaviours, **no new test file**. The existing `Assets/Tests/Editor/Data/DataTests.asmdef` already references both `Axiom.Data` and `Axiom.Battle`. `EnemyDataTests.cs` already exists on disk (it covers `battleVisualPrefab` via reflection) — the new tests append to that fixture rather than replace it.

---

## Task 1: Append failing `spellVfxOffset` tests to EnemyDataTests

**Why TDD here:** The field is one line, but the AC explicitly says "default `(0,0)` produces current behavior — existing enemies unaffected until tuned." Locking that contract in a test prevents a future refactor from silently changing the default and breaking already-tuned `.asset` files.

**Files:**
- Modify (append): `Assets/Tests/Editor/Data/EnemyDataTests.cs`

> **Note:** This file already exists on disk and contains three reflection-based tests for `battleVisualPrefab`. **Do not overwrite it.** Append the new tests inside the existing `Axiom.Data.Tests.EnemyDataTests` class. The existing file uses `using System.Reflection;`, `using NUnit.Framework;`, `using UnityEngine;` and namespace `Axiom.Data.Tests` — no new using directives are required.

- [ ] **Step 1: Append four new `spellVfxOffset` tests to the existing class**

Open `Assets/Tests/Editor/Data/EnemyDataTests.cs`. After the last existing `[Test]` (currently `BattleVisualPrefab_Field_IsGameObjectType`) and before the closing `}` of the `EnemyDataTests` class, append:

```csharp
        [Test]
        public void DefaultSpellVfxOffset_IsZero()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            Assert.AreEqual(Vector2.zero, data.spellVfxOffset);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void SpellVfxOffset_RoundTripsAssignedValue()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.spellVfxOffset = new Vector2(0.5f, -0.25f);
            Assert.AreEqual(new Vector2(0.5f, -0.25f), data.spellVfxOffset);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void SpellVfxOffset_Field_HasTooltip()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "spellVfxOffset",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "EnemyData.spellVfxOffset field is missing.");

            var tooltips = field.GetCustomAttributes(typeof(TooltipAttribute), false);
            Assert.IsNotEmpty(tooltips,
                "spellVfxOffset must have a [Tooltip] explaining the per-enemy nudge semantics.");
        }

        [Test]
        public void SpellVfxOffset_Field_IsVector2Type()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "spellVfxOffset",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(Vector2), field.FieldType,
                "spellVfxOffset must be a Vector2 so BattleController can cast to Vector3 " +
                "while preserving the enemy transform's Z.");
        }
```

The reflection-based tooltip / field-type tests mirror the style of the existing `BattleVisualPrefab_Field_HasTooltip` and `BattleVisualPrefab_Field_IsGameObjectType` tests for consistency.

- [ ] **Step 2: Confirm the new tests FAIL to compile**

> **Unity Editor task (user):** Save the file → return to Unity Editor → wait for the recompile to finish → open the **Console** window. There must be a compile error (red icon) referencing `EnemyData.spellVfxOffset` at `Assets/Tests/Editor/Data/EnemyDataTests.cs`. Because the entire `DataTests` assembly fails to load, **Window → General → Test Runner → EditMode** will not list `Axiom.Data.Tests.EnemyDataTests` (and other DataTests fixtures will also be unavailable until Task 2 fixes the field). That is the expected red state — proceed to Task 2.

If the Console shows no compile error, stop and re-check that the new test methods were saved into the existing `Axiom.Data.Tests.EnemyDataTests` class (not a duplicate class declaration).

---

## Task 2: Add `spellVfxOffset` to EnemyData

**Why this task:** Make the failing test from Task 1 compile and pass by adding the public field with the correct default. No other change to `EnemyData`.

**Files:**
- Modify: `Assets/Scripts/Data/EnemyData.cs`

- [ ] **Step 1: Add the `spellVfxOffset` field to EnemyData**

Open `Assets/Scripts/Data/EnemyData.cs` and add the field shown below directly **after** the existing `loot` field (the last field in the class). Do not change any other field, attribute, or namespace.

```csharp
        [Tooltip("Pixel-aligned world-space offset (in Battle scene units) added to the enemy's transform position when spawning SpellVFX for Damage spells. Default (0, 0) preserves current behavior. Tune per-enemy so the VFX lands on the sprite's visual center.")]
        public Vector2 spellVfxOffset = Vector2.zero;
```

After the edit, the field block of `EnemyData` should read in this order: `enemyName`, `maxHP`, `maxMP`, `atk`, `def`, `spd`, `xpReward`, `innateConditions`, `battleVisualPrefab`, `loot`, `spellVfxOffset`.

- [ ] **Step 2: Run the EnemyData tests in the Unity Editor and confirm they PASS**

> **Unity Editor task (user):** Save `EnemyData.cs` → return to Unity → wait for the recompile → in **Test Runner → EditMode**, run **Axiom.Data.Tests.EnemyDataTests**. All four new tests (`DefaultSpellVfxOffset_IsZero`, `SpellVfxOffset_RoundTripsAssignedValue`, `SpellVfxOffset_Field_HasTooltip`, `SpellVfxOffset_Field_IsVector2Type`) plus the three existing `BattleVisualPrefab_*` tests should be green.

If either test still fails, recheck: (a) the field is `public`, (b) the type is `Vector2`, (c) the field name is exactly `spellVfxOffset` (camelCase), (d) the default `= Vector2.zero` is present.

- [ ] **Step 3: Confirm existing enemy assets still deserialize cleanly**

> **Unity Editor task (user):** In the **Project** window, click each `.asset` under `Assets/Data/Enemies/` (`ED_AcidPool`, `ED_AcidSlug`, `ED_CorrosionQueen`, `ED_FrostbiteCreeper`, `ED_FrostMeltSentinel`, `ED_FrostMeltspawn`, `ED_Gasbloater`, `ED_IceSlime`, `ED_LivingFurnace`, `ED_NullKing`, `ED_Sparksprite`, `ED_VoidWraith`, `ED_VolatileResidue`). The Inspector should now show a new **Spell Vfx Offset** Vector2 field reading `X 0  Y 0` on every enemy. No yellow "missing script" warnings should appear in the Console.

If any asset fails to load or shows a missing-script warning, stop and report — that means the field added in Step 1 has a deserialization mismatch and must be fixed before continuing.

- [ ] **Step 4: Check in Task 1 + Task 2 changes via UVCS**

> **Unity Editor task (user):** Unity Version Control → **Pending Changes** → stage **only** the files listed below → **Check in** with message: `feat(DEV-86): add spellVfxOffset field to EnemyData`
>   - `Assets/Scripts/Data/EnemyData.cs`
>   - `Assets/Tests/Editor/Data/EnemyDataTests.cs`

Both `.cs.meta` files already exist and are not modified by this work, so they will not appear in Pending Changes. This is a modify-only check-in (no new files).

---

## Task 3: Apply the offset in BattleController.FireSpellVisuals()

**Why this task:** This is the only behavior change visible in the Battle scene. The change is one expression: read `_enemyData.spellVfxOffset` (null-guarded), cast to `Vector3` (preserves Z from the source position), add to the enemy transform position. Heal/Shield branch is untouched.

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs` (method `FireSpellVisuals`, lines ~575–581)

- [ ] **Step 1: Apply the offset to the Damage-spell `vfxPosition` calculation**

Open `Assets/Scripts/Battle/BattleController.cs`. Inside `FireSpellVisuals()`, locate the existing block (currently around lines 575–581):

```csharp
            if (_spellVfxController != null)
            {
                Vector3 vfxPosition = spell.effectType == SpellEffectType.Damage
                    ? (_enemyAnimator  != null ? _enemyAnimator.transform.position  : Vector3.zero)
                    : (_playerAnimator != null ? _playerAnimator.transform.position : Vector3.zero);
                _spellVfxController.Play(spell, vfxPosition);
            }
```

Replace it with:

```csharp
            if (_spellVfxController != null)
            {
                // DEV-86: enemy-side SpellVFX nudge. _enemyData is optional in standalone testing.
                Vector2 enemyOffset = _enemyData != null ? _enemyData.spellVfxOffset : Vector2.zero;
                Vector3 vfxPosition = spell.effectType == SpellEffectType.Damage
                    ? (_enemyAnimator  != null ? _enemyAnimator.transform.position + (Vector3)enemyOffset : Vector3.zero)
                    : (_playerAnimator != null ? _playerAnimator.transform.position : Vector3.zero);
                _spellVfxController.Play(spell, vfxPosition);
            }
```

Constraints:

- Do **not** apply `enemyOffset` to the Heal/Shield branch — DEV-86 is explicitly enemy-side only ("Out of scope / follow-up: Player-side offset for Heal/Shield VFX").
- Do **not** introduce a new SerializeField, helper method, or static utility. The change is local to this method.
- Do **not** change the `_enemyAnimator != null` guard — when the animator is missing the `Vector3.zero` fallback must remain.

- [ ] **Step 2: Verify the project compiles cleanly**

> **Unity Editor task (user):** Save `BattleController.cs` → return to Unity → confirm the **Console** shows no compile errors after the recompile finishes.

- [ ] **Step 3: Re-run all Edit-Mode tests and confirm none regressed**

> **Unity Editor task (user):** In **Test Runner → EditMode**, click **Run All**. All previously-green tests must remain green, including:
>   - `Axiom.Data.Tests.EnemyDataTests` (added in Task 1)
>   - `Axiom.Tests.Battle.SpellDataVFXTests` (existing — proves the SpellVFX path still null-safe)
>   - All other Battle / Data / UI / Platformer / Core fixtures.

If anything regresses, the most likely cause is a typo in the replaced block — re-diff against the snippet in Step 1. Do **not** continue to Step 4 until everything is green.

- [ ] **Step 4: Manual smoke test in the Battle scene with default offset (0, 0)**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Select the BattleController GameObject in the Hierarchy and confirm an `EnemyData` asset is wired into the `_enemyData` SerializeField (any of the existing `ED_*.asset` files works — e.g., `ED_IceSlime`).
> 3. Confirm the assigned EnemyData asset still shows `Spell Vfx Offset = (0, 0)` in the Inspector.
> 4. Press **Play** → take a player turn → cast a Damage-type spell.
> 5. Observe: the SpellVFX sprite must spawn at the same world position it spawned at before this change — i.e., the enemy's transform origin. **No visible change** is the success criterion for default offset.
> 6. Stop Play mode.

If the VFX shifts, the offset is being applied when it should not be — recheck that the Inspector value is exactly `(0, 0)` and that the code change does not add the offset unconditionally.

- [ ] **Step 5: Manual smoke test with a tuned offset**

> **Unity Editor task (user):**
> 1. With the same EnemyData asset selected, set **Spell Vfx Offset** to a clearly visible value such as `(1, 0.5)` for the duration of this test.
> 2. Press **Play** and cast a Damage spell.
> 3. Observe: the SpellVFX must now spawn 1 world-unit right and 0.5 world-units above the previous spawn position. The shift should be obvious by eye.
> 4. Cast a Heal or Shield spell on the same turn (if MP allows). Observe: the player-side VFX position is unchanged — the offset must NOT apply to player-targeted spells.
> 5. Stop Play mode and **revert** Spell Vfx Offset back to `(0, 0)` on this asset before checking in. Per AC3, existing enemies remain unaffected until intentionally tuned in a separate task.

If the Heal/Shield VFX also shifts, the offset has been wired into the wrong branch — re-diff Step 1.

- [ ] **Step 6: Check in Task 3 changes via UVCS**

> **Unity Editor task (user):** Unity Version Control → **Pending Changes** → stage **only** the file listed below → **Check in** with message: `feat(DEV-86): apply per-enemy spellVfxOffset to Damage spell VFX spawn`
>   - `Assets/Scripts/Battle/BattleController.cs`

`BattleController.cs.meta` is untouched and will not appear in Pending Changes. **Do not** check in any modified `.asset` files from `Assets/Data/Enemies/` — Step 5 instructed reverting the test value to `(0, 0)`.

---

## Task 4 (Follow-up, optional): Tune per-enemy offsets

Per AC3, existing enemies remain at `(0, 0)` until intentionally tuned. Tuning is a pure Editor activity with no code change and is **not required for DEV-86 to be considered Done** — DEV-86's Definition of Done is met after Task 3 ships.

> **Unity Editor task (user, optional follow-up):** For each enemy whose SpellVFX visibly mis-aligns with the sprite's center of mass during playtest, open the corresponding `Assets/Data/Enemies/ED_<Name>.asset`, set **Spell Vfx Offset** to the world-unit nudge that lands the VFX on-center, and check in each tuned asset individually with message `chore(DEV-86): tune spellVfxOffset for <EnemyName>` and the asset's `.asset` + `.asset.meta` paired together.

If a follow-up code change is desired (e.g., extending the offset to player-side Heal/Shield spells), file a separate Jira story — it is explicitly out of scope here.

---

## Self-Review Checklist (run before declaring the plan ready)

After saving this file, the plan author re-reads it and confirms:

- [ ] **Spec coverage.** Every Acceptance Criterion in DEV-86 maps to a task:
  - AC1 ("`EnemyData` inspector exposes a `Spell VFX Offset` Vector2 field") → Task 2 Step 1 (field added with `[Tooltip]`, public, `Vector2`).
  - AC2 ("Setting the offset on an enemy's `.asset` repositions the SpellVFX spawn during a Damage cast") → Task 3 Step 5 (manual smoke test with `(1, 0.5)`).
  - AC3 ("Default `(0, 0)` produces current behavior — existing enemies unaffected until tuned") → Task 1 (Edit-Mode unit test) + Task 3 Step 4 (manual zero-offset confirmation) + Task 4 Editor callout.
- [ ] **No placeholders.** No "TODO", no "implement later", no abstract "add appropriate handling" in any code block.
- [ ] **Type consistency.** `Vector2 spellVfxOffset` in `EnemyData` is referenced as `_enemyData.spellVfxOffset` in `BattleController` — names match exactly.
- [ ] **Null-guard correctness.** `_enemyData != null ? _enemyData.spellVfxOffset : Vector2.zero` precedes the existing `_enemyAnimator != null` guard, so when `_enemyData` is missing the offset is `Vector2.zero` and the original `Vector3.zero` fallback is still reachable when `_enemyAnimator` is also missing.
- [ ] **UVCS staged-file audit.**
  - Task 2 check-in: `EnemyData.cs` + `EnemyDataTests.cs` (2 files; modify-only — both `.cs.meta` files already exist and are untouched).
  - Task 3 check-in: `BattleController.cs` (1 file; modify-only — `.cs.meta` untouched).
- [ ] **Editor / code separation.** Every `> **Unity Editor task (user):**` callout is its own checkbox and does not include code edits. Every code edit is its own checkbox and does not include Editor steps.
- [ ] **No git commands.** Every check-in step uses Unity Version Control → Pending Changes. No `git add`, no `git commit` anywhere in the plan.
- [ ] **Out-of-scope discipline.** Player-side Heal/Shield offset is mentioned only as future work in Task 4 and the architecture summary — it is never wired into a code step.

---

## Execution

After this plan is read and approved, execution choice:

**1. Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration. Use `superpowers:subagent-driven-development`.

**2. Inline Execution** — execute tasks in this session using `superpowers:executing-plans` with batch checkpoints.

For Unity-specific UVCS handoffs and Editor task gating, pair either approach with the `executing-unity-game-dev-plans` skill.
