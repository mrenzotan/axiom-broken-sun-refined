# DEV-89 — Battle Scene Enemy Visual Prefab Swap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Pair with the `executing-unity-game-dev-plans` skill for the Unity Editor handoffs and UVCS check-in cadence. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Battle scene's enemy GameObject (sprite, Animator, hit-frame events, attack offsets) match the `EnemyData` carried by the triggering `ExplorationEnemyCombatTrigger`, so non-Ice-Slime engagements no longer render Ice Slime visuals.

**Architecture:** Each `EnemyData` ScriptableObject gains a `battleVisualPrefab` field. A new pure-C# `EnemyVisualSpawner` instantiates that prefab under an `EnemySpawnAnchor` Transform in the Battle scene and returns its `EnemyBattleAnimator`. `BattleController.Start()` calls the spawner and reassigns its `_enemyAnimator` reference **before** `InitializeFromTransition()` runs, so the animator-event wiring inside `Initialize()` (`OnHitFrame`, `OnAttackSequenceComplete`, `OnSpellFireFrame`, `OnSpellChargeAborted`) hooks the live spawned instance. If `EnemyData`, `battleVisualPrefab`, or the anchor is null, the spawner logs a warning and returns the Inspector-assigned `_enemyAnimator` unchanged so standalone Battle scene play-from-scene still works.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, ScriptableObject data layer, Unity Test Framework (Edit Mode, NUnit).

**Related tickets:**
- **DEV-80** — Dynamic Battle Background (the architectural twin of this work — same `Start()` insertion point pattern, same plain-C#-service architecture, but for the background `SpriteRenderer` instead of the enemy GameObject).
- **DEV-46** — Level 1 Snow Mountain (parent feature; blocks completion until correct enemy visuals render for FrostMeltspawn/FrostbiteCreeper/FrostMeltSentinel encounters).

**Prerequisites:** None. The runtime hook is greenfield. Battle prefabs already exist on disk for Ice Slime, FrostMeltspawn, and VoidWraith.

---

## File Structure

### New runtime scripts

| File | Assembly | Responsibility |
|---|---|---|
| `Assets/Scripts/Battle/EnemyVisualSpawner.cs` | `Axiom.Battle` | Plain C# (no MonoBehaviour). Instantiates `EnemyData.battleVisualPrefab` under a `Transform` anchor and returns the spawned `EnemyBattleAnimator`. Returns the supplied fallback animator unchanged when any input is null or the prefab has no `EnemyBattleAnimator`. |

### New tests

| File | Assembly |
|---|---|
| `Assets/Tests/Editor/Battle/EnemyVisualSpawnerTests.cs` | `BattleTests` (existing — already references `Axiom.Battle`, `Axiom.Core`, `Axiom.Data`) |
| `Assets/Tests/Editor/Data/EnemyDataTests.cs` | `DataTests` (existing — already references `Axiom.Battle`, `Axiom.Data`) |

### Modified runtime scripts

| File | Change |
|---|---|
| `Assets/Scripts/Data/EnemyData.cs` | Add `public GameObject battleVisualPrefab;` field with tooltip. |
| `Assets/Scripts/Battle/BattleController.cs` | Add `[SerializeField] private Transform _enemySpawnAnchor;` field; add `private EnemyVisualSpawner _visualSpawner;` private field; in `Start()` after the existing battle-music block and before the `SceneTransition?.IsTransitioning` check, instantiate the spawner and reassign `_enemyAnimator` from its return value. |

### Unity Editor tasks (user)

| Task |
|---|
| In `Assets/Scenes/Battle.unity`: add an empty `EnemySpawnAnchor` GameObject at the existing `Ice Slime (Battle)` Transform position; delete the static `Ice Slime (Battle)` GameObject; assign the new anchor to `BattleController._enemySpawnAnchor`; verify `BattleController._enemyAnimator` is now empty (None). |
| For each `Assets/Data/Enemies/ED_*.asset` whose battle prefab exists on disk, drag the matching prefab into the new `Battle Visual Prefab` Inspector field. Currently this covers `ED_IceSlime`, `ED_FrostMeltspawn`, `ED_VoidWraith`. |
| Leave `battleVisualPrefab` empty for `ED_FrostbiteCreeper` and `ED_FrostMeltSentinel` — both are missing battle prefabs on disk. Track as DEV-89 follow-ups (Task 7). The runtime warning path will fire for them until the prefabs are authored. |
| Manual end-to-end playtest from `Platformer.unity` against each Snow Mountain enemy type. |

### Folder/asset structure on disk (reference)

```
Assets/Data/Enemies/
├── ED_IceSlime.asset           ← wire battleVisualPrefab → Ice Slime (Battle).prefab
├── ED_FrostMeltspawn.asset     ← wire battleVisualPrefab → FrostMeltSpawnBattle.prefab
├── ED_VoidWraith.asset         ← wire battleVisualPrefab → VoidWraithBattle.prefab
├── ED_FrostbiteCreeper.asset   ← LEAVE NULL (prefab missing — follow-up)
├── ED_FrostMeltSentinel.asset  ← LEAVE NULL (prefab missing — follow-up)
└── (8 other ED_*.asset files for higher zones — leave null until those zones ship)

Assets/Prefabs/Enemies/Level 1/
├── Ice Slime (Battle).prefab        (existing — wire to ED_IceSlime)
├── FrostMeltSpawnBattle.prefab      (existing — wire to ED_FrostMeltspawn)
├── VoidWraithBattle.prefab          (existing — wire to ED_VoidWraith)
└── (no battle prefabs for FrostbiteCreeper / FrostMeltSentinel yet)
```

---

## Task 1: Add `battleVisualPrefab` field to `EnemyData`

**Files:**
- Modify: `Assets/Scripts/Data/EnemyData.cs`
- Create: `Assets/Tests/Editor/Data/EnemyDataTests.cs`
- Create: `Assets/Tests/Editor/Data/EnemyDataTests.cs.meta` (auto-generated by Unity)

- [ ] **Step 1: Write failing test — `EnemyDataTests.cs`**

Create `Assets/Tests/Editor/Data/EnemyDataTests.cs` with:

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Data.Tests
{
    public class EnemyDataTests
    {
        [Test]
        public void BattleVisualPrefab_Default_IsNull()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            Assert.IsNull(data.battleVisualPrefab,
                "battleVisualPrefab should default to null so unconfigured EnemyData " +
                "falls through to the BattleController fallback path.");
            Object.DestroyImmediate(data);
        }

        [Test]
        public void BattleVisualPrefab_Field_HasTooltip()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "battleVisualPrefab",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "EnemyData.battleVisualPrefab field is missing.");

            var tooltips = field.GetCustomAttributes(typeof(TooltipAttribute), false);
            Assert.IsNotEmpty(tooltips,
                "battleVisualPrefab must have a [Tooltip] explaining the required prefab shape.");
        }

        [Test]
        public void BattleVisualPrefab_Field_IsGameObjectType()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "battleVisualPrefab",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(GameObject), field.FieldType,
                "battleVisualPrefab must be a GameObject (the prefab root) so the spawner " +
                "can Instantiate it and resolve EnemyBattleAnimator via GetComponentInChildren.");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Open Unity Editor → Window → General → Test Runner → EditMode → DataTests.
Expected: 3 new tests visible, all FAIL with "EnemyData.battleVisualPrefab field is missing." or compile error on `data.battleVisualPrefab`.

- [ ] **Step 3: Add the field to `EnemyData.cs`**

Modify `Assets/Scripts/Data/EnemyData.cs`. Add the new field directly above `loot` (so visual config sits near the top of the asset Inspector):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// ScriptableObject holding all data for one enemy type.
    /// innateConditions defines the enemy's material composition — what it is made of.
    /// These are copied into CharacterStats.InnateConditions on battle init.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Axiom/Data/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Tooltip("Display name shown in the Battle UI.")]
        public string enemyName;

        public int maxHP;
        public int maxMP;
        public int atk;
        public int def;
        public int spd;

        [Tooltip("XP awarded to the player on defeat.")]
        [Min(0)] public int xpReward;

        [Tooltip("1–2 material conditions the enemy starts every combat with. Defines what the enemy is made of — determines physical immunity, reaction targets, and other combat interactions.")]
        public List<ChemicalCondition> innateConditions = new List<ChemicalCondition>();

        [Tooltip("Battle scene prefab instantiated by BattleController on scene load. " +
                 "Must contain (or have a child with) SpriteRenderer + Animator + EnemyBattleAnimator. " +
                 "Leave null only for enemies whose battle prefab has not been authored yet — " +
                 "BattleController will warn and fall back to the Inspector-assigned animator.")]
        public GameObject battleVisualPrefab;

        [Tooltip("Possible item drops. Each entry rolls independently against its dropChance.")]
        public List<LootEntry> loot = new List<LootEntry>();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Editor → Test Runner → EditMode → DataTests → run the 3 new tests.
Expected: 3 PASS. All other DataTests still PASS.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-89): add battleVisualPrefab field to EnemyData`
- `Assets/Scripts/Data/EnemyData.cs`
- `Assets/Scripts/Data/EnemyData.cs.meta` _(unchanged but include if Unity touched it; otherwise omit)_
- `Assets/Tests/Editor/Data/EnemyDataTests.cs`
- `Assets/Tests/Editor/Data/EnemyDataTests.cs.meta`

> Note: existing `ED_*.asset` files do **not** need to be re-saved by this code change. Unity's serializer treats a missing `battleVisualPrefab` field on disk as the default null value at load time. The Inspector wiring in Task 4 is what causes them to be re-saved.

---

## Task 2: `EnemyVisualSpawner` plain C# class

**Files:**
- Create: `Assets/Scripts/Battle/EnemyVisualSpawner.cs`
- Create: `Assets/Scripts/Battle/EnemyVisualSpawner.cs.meta` (auto-generated by Unity)
- Create: `Assets/Tests/Editor/Battle/EnemyVisualSpawnerTests.cs`
- Create: `Assets/Tests/Editor/Battle/EnemyVisualSpawnerTests.cs.meta` (auto-generated by Unity)

- [ ] **Step 1: Write failing tests — `EnemyVisualSpawnerTests.cs`**

Create `Assets/Tests/Editor/Battle/EnemyVisualSpawnerTests.cs` with:

```csharp
using NUnit.Framework;
using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class EnemyVisualSpawnerTests
    {
        private EnemyVisualSpawner _spawner;
        private GameObject _anchorGo;
        private Transform _anchor;
        private GameObject _fakePrefab;
        private GameObject _fallbackGo;
        private EnemyBattleAnimator _fallback;
        private EnemyData _data;

        [SetUp]
        public void SetUp()
        {
            _spawner = new EnemyVisualSpawner();

            _anchorGo = new GameObject("EnemySpawnAnchor");
            _anchor = _anchorGo.transform;

            // "Prefab template" — a deactivated GameObject with EnemyBattleAnimator
            // attached. Object.Instantiate clones runtime GameObjects in EditMode tests.
            _fakePrefab = new GameObject("FakeBattleVisualPrefab");
            _fakePrefab.AddComponent<EnemyBattleAnimator>();
            _fakePrefab.SetActive(false);

            _fallbackGo = new GameObject("FallbackEnemyAnimator");
            _fallback = _fallbackGo.AddComponent<EnemyBattleAnimator>();

            _data = ScriptableObject.CreateInstance<EnemyData>();
            _data.enemyName = "TestEnemy";
            _data.battleVisualPrefab = _fakePrefab;
        }

        [TearDown]
        public void TearDown()
        {
            if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
            if (_fakePrefab != null) Object.DestroyImmediate(_fakePrefab);
            if (_fallbackGo != null) Object.DestroyImmediate(_fallbackGo);
            if (_data != null) Object.DestroyImmediate(_data);
        }

        [Test]
        public void Spawn_DataNull_ReturnsFallback()
        {
            EnemyBattleAnimator result = _spawner.Spawn(null, _anchor, _fallback);
            Assert.AreSame(_fallback, result);
            Assert.AreEqual(0, _anchor.childCount,
                "Nothing should be instantiated when EnemyData is null.");
        }

        [Test]
        public void Spawn_PrefabNull_ReturnsFallback()
        {
            _data.battleVisualPrefab = null;
            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);
            Assert.AreSame(_fallback, result);
            Assert.AreEqual(0, _anchor.childCount);
        }

        [Test]
        public void Spawn_AnchorNull_ReturnsFallback()
        {
            EnemyBattleAnimator result = _spawner.Spawn(_data, null, _fallback);
            Assert.AreSame(_fallback, result);
        }

        [Test]
        public void Spawn_PrefabHasNoEnemyBattleAnimator_ReturnsFallback()
        {
            var noAnimatorPrefab = new GameObject("NoAnimatorPrefab");
            noAnimatorPrefab.SetActive(false);
            _data.battleVisualPrefab = noAnimatorPrefab;

            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);

            Assert.AreSame(_fallback, result,
                "Spawner must return the fallback when the prefab lacks an EnemyBattleAnimator.");

            Object.DestroyImmediate(noAnimatorPrefab);
        }

        [Test]
        public void Spawn_Valid_ParentsInstanceUnderAnchor()
        {
            _spawner.Spawn(_data, _anchor, _fallback);
            Assert.AreEqual(1, _anchor.childCount,
                "Spawned visual prefab should be parented directly under the anchor.");
        }

        [Test]
        public void Spawn_Valid_ReturnsSpawnedAnimator_NotFallback()
        {
            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);
            Assert.IsNotNull(result);
            Assert.AreNotSame(_fallback, result,
                "Should return the spawned instance's animator, not the fallback.");
            Assert.IsTrue(result.transform.IsChildOf(_anchor),
                "Spawned animator should live inside the anchor's hierarchy.");
        }

        [Test]
        public void Spawn_Valid_ResetsLocalPositionToZero()
        {
            // Even if the prefab template has a non-zero local position, the spawned
            // instance must sit exactly at the anchor.
            _fakePrefab.transform.localPosition = new Vector3(5f, 6f, 7f);

            _spawner.Spawn(_data, _anchor, _fallback);

            Transform spawned = _anchor.GetChild(0);
            Assert.AreEqual(Vector3.zero, spawned.localPosition,
                "Spawned instance localPosition should be (0,0,0) so the anchor defines spawn position.");
        }

        [Test]
        public void Spawn_AnimatorOnChildOfPrefab_StillResolved()
        {
            // Mirrors the project's Enemy → Visual (child) sprite-flipping pattern from
            // GAME_PLAN.md §6: the EnemyBattleAnimator lives on a child, not the root.
            var rootPrefab = new GameObject("RootOnlyPrefab");
            rootPrefab.SetActive(false);
            var visualChild = new GameObject("Visual");
            visualChild.transform.SetParent(rootPrefab.transform, worldPositionStays: false);
            visualChild.AddComponent<EnemyBattleAnimator>();

            _data.battleVisualPrefab = rootPrefab;

            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);

            Assert.IsNotNull(result);
            Assert.AreNotSame(_fallback, result);
            Assert.IsTrue(result.transform.IsChildOf(_anchor));

            Object.DestroyImmediate(rootPrefab);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Editor → Test Runner → EditMode → BattleTests.
Expected: 8 new tests, all FAIL with compile error "The type or namespace name 'EnemyVisualSpawner' could not be found".

- [ ] **Step 3: Implement `EnemyVisualSpawner.cs`**

Create `Assets/Scripts/Battle/EnemyVisualSpawner.cs` with:

```csharp
using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Instantiates the battle visual prefab from an EnemyData under a spawn anchor and
    /// returns its EnemyBattleAnimator. Returns the supplied fallback animator unchanged
    /// when EnemyData, battleVisualPrefab, or anchor is null, or when the spawned prefab
    /// has no EnemyBattleAnimator — preserving standalone Battle scene play-from-scene.
    /// Pure C# — zero Unity lifecycle. Call from BattleController.Start() before Initialize.
    /// </summary>
    public sealed class EnemyVisualSpawner
    {
        public EnemyBattleAnimator Spawn(
            EnemyData data,
            Transform anchor,
            EnemyBattleAnimator fallback)
        {
            if (data == null) return fallback;

            if (data.battleVisualPrefab == null)
            {
                Debug.LogWarning(
                    $"[Battle] EnemyData '{data.enemyName}' has no battleVisualPrefab assigned — " +
                    "using Inspector-assigned _enemyAnimator. Assign a battle prefab on the " +
                    "EnemyData asset to swap the enemy GameObject per battle.");
                return fallback;
            }

            if (anchor == null)
            {
                Debug.LogWarning(
                    $"[Battle] EnemyVisualSpawner.Spawn called with null anchor for " +
                    $"'{data.enemyName}' — using Inspector-assigned _enemyAnimator. " +
                    "Assign _enemySpawnAnchor on BattleController in the Battle scene.");
                return fallback;
            }

            GameObject instance = Object.Instantiate(data.battleVisualPrefab, anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.SetActive(true);

            EnemyBattleAnimator spawned = instance.GetComponentInChildren<EnemyBattleAnimator>(
                includeInactive: true);
            if (spawned == null)
            {
                Debug.LogWarning(
                    $"[Battle] Spawned battleVisualPrefab for '{data.enemyName}' has no " +
                    "EnemyBattleAnimator component on the root or any child — using fallback. " +
                    "Add an EnemyBattleAnimator to the prefab.");
                DestroySafely(instance);
                return fallback;
            }

            return spawned;
        }

        // Object.Destroy is illegal outside Play Mode and triggers a
        // "Destroy may not be called from edit mode" error in EditMode tests,
        // which Unity Test Framework treats as a failure. Fall back to
        // DestroyImmediate when the application is not playing.
        private static void DestroySafely(Object obj)
        {
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Editor → Test Runner → EditMode → BattleTests → run the 8 new tests.
Expected: 8 PASS. All other BattleTests still PASS.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-89): add EnemyVisualSpawner`
- `Assets/Scripts/Battle/EnemyVisualSpawner.cs`
- `Assets/Scripts/Battle/EnemyVisualSpawner.cs.meta`
- `Assets/Tests/Editor/Battle/EnemyVisualSpawnerTests.cs`
- `Assets/Tests/Editor/Battle/EnemyVisualSpawnerTests.cs.meta`

---

## Task 3: Wire `EnemyVisualSpawner` into `BattleController.Start()`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Step 1: Add the `_enemySpawnAnchor` SerializeField**

In `Assets/Scripts/Battle/BattleController.cs`, add the new field directly **after** the existing `_enemyData` SerializeField block (around line 59). The grouping keeps enemy-related serialized fields contiguous in the Inspector:

```csharp
        [SerializeField]
        [Tooltip("Empty Transform marking where the enemy battle visual is spawned. " +
                 "BattleController instantiates EnemyData.battleVisualPrefab as a child of this anchor " +
                 "in Start() and reassigns _enemyAnimator to the spawned instance.")]
        private Transform _enemySpawnAnchor;
```

- [ ] **Step 2: Add the `_visualSpawner` private field**

In the private fields block (around line 227, alongside `private BattleEnvironmentService _environmentService;`), add:

```csharp
        private EnemyVisualSpawner _visualSpawner;
```

- [ ] **Step 3: Modify `Start()` to call the spawner before `InitializeFromTransition()`**

Current `Start()` body (lines 235–265):

```csharp
        private void Start()
        {
            var pending = GameManager.Instance?.PendingBattle;
            if (pending != null)
            {
                _startState    = pending.StartState;
                _enemyData     = pending.EnemyData;
                _battleEnemyId = pending.EnemyId;
                _enemyStartHp  = pending.EnemyCurrentHp;
                GameManager.Instance.ClearPendingBattle();
                if (_tutorialController != null)
                    _tutorialController.Setup(pending.TutorialMode);
            }

            // Apply dynamic battle background from the engagement origin (DEV-80).
            _environmentService = new BattleEnvironmentService();
            _environmentService.Apply(pending?.EnvironmentData, _backgroundRenderer);

            // Play battle music from the per-enemy BattleEnvironmentData.
            AudioClip battleMusic = pending?.EnvironmentData?.BattleMusic;
            if (battleMusic != null)
            {
                GameManager gm = GameManager.Instance;
                gm?.AudioManager?.PlayBgm(battleMusic, 1f);
            }

            if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
                GameManager.Instance.OnSceneReady += InitializeFromTransition;
            else
                InitializeFromTransition();
        }
```

Insert two new lines (a blank-line-separated block) **after** the music block and **before** the `SceneTransition?.IsTransitioning` check. The full `Start()` after the edit reads:

```csharp
        private void Start()
        {
            var pending = GameManager.Instance?.PendingBattle;
            if (pending != null)
            {
                _startState    = pending.StartState;
                _enemyData     = pending.EnemyData;
                _battleEnemyId = pending.EnemyId;
                _enemyStartHp  = pending.EnemyCurrentHp;
                GameManager.Instance.ClearPendingBattle();
                if (_tutorialController != null)
                    _tutorialController.Setup(pending.TutorialMode);
            }

            // Apply dynamic battle background from the engagement origin (DEV-80).
            _environmentService = new BattleEnvironmentService();
            _environmentService.Apply(pending?.EnvironmentData, _backgroundRenderer);

            // Play battle music from the per-enemy BattleEnvironmentData.
            AudioClip battleMusic = pending?.EnvironmentData?.BattleMusic;
            if (battleMusic != null)
            {
                GameManager gm = GameManager.Instance;
                gm?.AudioManager?.PlayBgm(battleMusic, 1f);
            }

            // DEV-89: swap the enemy visual GameObject to match the triggering EnemyData.
            // Must run before InitializeFromTransition() so animator-event wiring inside
            // Initialize() hooks the live spawned instance, not the deleted scene placeholder.
            _visualSpawner = new EnemyVisualSpawner();
            _enemyAnimator = _visualSpawner.Spawn(_enemyData, _enemySpawnAnchor, _enemyAnimator);

            if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
                GameManager.Instance.OnSceneReady += InitializeFromTransition;
            else
                InitializeFromTransition();
        }
```

- [ ] **Step 4: Verify compilation**

Switch to the Unity Editor. Wait for script compilation to complete. Console must be free of new errors. The Inspector for `BattleController` now shows an `Enemy Spawn Anchor` slot.

- [ ] **Step 5: Run all Edit Mode tests**

Unity Editor → Test Runner → EditMode → Run All.
Expected: every existing test still passes; `EnemyVisualSpawnerTests` (8) and `EnemyDataTests` (3) pass.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-89): instantiate enemy visual prefab in BattleController`
- `Assets/Scripts/Battle/BattleController.cs`

---

## Task 4: Wire `battleVisualPrefab` on existing `ED_*.asset` files

> **Unity Editor task (user):** All steps in this task happen in the Unity Editor. No code edits.

- [ ] **Step 1: Wire `ED_IceSlime`**

Project window → `Assets/Data/Enemies/ED_IceSlime.asset` → Inspector → drag `Assets/Prefabs/Enemies/Level 1/Ice Slime (Battle).prefab` into the new **Battle Visual Prefab** field.

- [ ] **Step 2: Wire `ED_FrostMeltspawn`**

Project window → `Assets/Data/Enemies/ED_FrostMeltspawn.asset` → Inspector → drag `Assets/Prefabs/Enemies/Level 1/FrostMeltSpawnBattle.prefab` into **Battle Visual Prefab**.

- [ ] **Step 3: Wire `ED_VoidWraith`**

Project window → `Assets/Data/Enemies/ED_VoidWraith.asset` → Inspector → drag `Assets/Prefabs/Enemies/Level 1/VoidWraithBattle.prefab` into **Battle Visual Prefab**.

- [ ] **Step 4: Confirm the rest stay null (intentional)**

For these assets, leave **Battle Visual Prefab** as `None`. Do **not** wire them — the runtime warning fallback path is the documented behavior until each prefab is authored:
- `ED_FrostbiteCreeper.asset` (battle prefab missing — Task 7 follow-up)
- `ED_FrostMeltSentinel.asset` (battle prefab missing — Task 7 follow-up)
- `ED_AcidPool.asset`, `ED_AcidSlug.asset`, `ED_CorrosionQueen.asset`, `ED_Gasbloater.asset`, `ED_LivingFurnace.asset`, `ED_NullKing.asset`, `ED_Sparksprite.asset`, `ED_VolatileResidue.asset` (higher zones — out of DEV-89 scope)

- [ ] **Step 5: Save the project**

`Ctrl/Cmd-S` in the Unity Editor (or `File → Save Project`) to flush the asset writes.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-89): wire battleVisualPrefab on Level 1 EnemyData assets`
- `Assets/Data/Enemies/ED_IceSlime.asset`
- `Assets/Data/Enemies/ED_IceSlime.asset.meta`
- `Assets/Data/Enemies/ED_FrostMeltspawn.asset`
- `Assets/Data/Enemies/ED_FrostMeltspawn.asset.meta`
- `Assets/Data/Enemies/ED_VoidWraith.asset`
- `Assets/Data/Enemies/ED_VoidWraith.asset.meta`

> If UVCS reports `.meta` files as unchanged, omit them — only the `.asset` files were modified by the Inspector edits. UVCS shows exactly what is dirty.

---

## Task 5: Battle scene editor changes (anchor + delete static enemy)

> **Unity Editor task (user):** All steps in this task happen in the Unity Editor. No code edits.

- [ ] **Step 1: Open the Battle scene**

`Assets/Scenes/Battle.unity` → double-click in Project window.

- [ ] **Step 2: Note the existing Ice Slime Transform**

Hierarchy → find the `Ice Slime (Battle)` GameObject (current static enemy instance). Inspector → record its Position (X, Y, Z) and rotation. The new anchor must sit at the same world position so existing camera framing and offset values remain valid.

- [ ] **Step 3: Create the `EnemySpawnAnchor` GameObject**

Hierarchy → right-click → `Create Empty`. Rename it `EnemySpawnAnchor`. Set its Transform Position to the values recorded in Step 2 (rotation = Quaternion.identity, scale = (1,1,1)).

> Place `EnemySpawnAnchor` at the **scene root**, not as a child of any existing GameObject. This keeps the anchor's world position stable regardless of any parent's transform.

- [ ] **Step 4: Delete the static `Ice Slime (Battle)` instance**

Hierarchy → right-click `Ice Slime (Battle)` → `Delete`. Confirm.

> If the scene has direct references to `Ice Slime (Battle)` other than `BattleController._enemyAnimator` (e.g. an animator subscription on another component), Unity will surface them as Missing References after deletion — fix any that appear by either re-pointing them at the anchor or removing them. Phase 4 architecture should not have any such references; this note is defensive.

- [ ] **Step 5: Wire `_enemySpawnAnchor` on the BattleController**

Hierarchy → select the GameObject that owns `BattleController` (commonly `BattleManager` or similar). Inspector → drag `EnemySpawnAnchor` from the Hierarchy into the new **Enemy Spawn Anchor** field.

- [ ] **Step 6: Confirm `_enemyAnimator` is now empty**

Same Inspector view → the **Enemy Animator** field should now read `None (Enemy Battle Animator)` since the GameObject it pointed at was deleted in Step 4. Leave it empty — the spawner overwrites this reference at runtime. (For optional standalone-without-data testing, you may temporarily reassign it; production data flow ignores this slot.)

- [ ] **Step 7: Save the scene**

`File → Save` (`Ctrl/Cmd-S`).

- [ ] **Step 8: Smoke-test standalone Battle scene**

Set Play Mode entry scene to `Battle.unity` (or open it directly and press Play with `BattleController._enemyData` Inspector-assigned to `ED_IceSlime`). Expected:
1. The Ice Slime battle prefab is instantiated under `EnemySpawnAnchor` at scene start.
2. Battle UI shows Ice Slime stats.
3. Pressing Attack plays the Ice Slime hurt animation; the enemy attacks correctly.
4. No `NullReferenceException` related to `_enemyAnimator` in the Console.

If any step fails, **stop** — diagnose before moving on. Common failure: `_enemySpawnAnchor` left unassigned → spawner returns the (now-null) fallback → `Initialize()` errors when wiring `_enemyAnimator.OnHitFrame`. Fix by reassigning the anchor.

- [ ] **Step 9: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-89): replace static Ice Slime in Battle scene with EnemySpawnAnchor`
- `Assets/Scenes/Battle.unity`
- `Assets/Scenes/Battle.unity.meta` _(only if UVCS shows it as dirty)_

---

## Task 6: End-to-end manual verification (playtest)

> **Unity Editor task (user):** All steps in this task happen in the Unity Editor (Play Mode in `Platformer.unity`). No code edits.

This task validates every bullet of the bug's Acceptance Criteria.

- [ ] **Step 1: Test wired enemy — Ice Slime**

1. Open a platformer scene that contains an Ice Slime `ExplorationEnemyCombatTrigger` (e.g. `Assets/Scenes/Level_1-1.unity`).
2. Press Play. Walk into / attack the Ice Slime.
3. Wait for Battle scene to load.
4. **Verify:** Ice Slime sprite, idle animation, attack animation, and hit-frame timing match the Ice Slime asset. BattleHUD shows `Ice Slime`.
5. Win the battle. Return to platformer.

- [ ] **Step 2: Test wired enemy — FrostMeltspawn**

1. Trigger an encounter against `FrostMeltspawn` in a platformer scene (Snow Mountain area from DEV-46).
2. **Verify:** Battle scene shows the FrostMeltSpawn sprite + animator (NOT Ice Slime). Hit-frame timing comes from `FrostMeltSpawnBattle.prefab`'s `EnemyBattleAnimator` settings, not Ice Slime's.

- [ ] **Step 3: Test fallback — FrostbiteCreeper (no battle prefab)**

1. Trigger an encounter against `FrostbiteCreeper`.
2. **Verify:** Console shows the warning: `[Battle] EnemyData 'FrostbiteCreeper' has no battleVisualPrefab assigned — using Inspector-assigned _enemyAnimator.`
3. **Verify:** Battle still loads without `NullReferenceException`. Stats are correct (FrostbiteCreeper Name/HP/ATK/DEF/SPD), even though the visual is the inspector fallback (likely missing/empty after Task 5 — that's the documented fallback behavior).

> If this case shows a hard error rather than a warning + degraded-but-functional battle, regress: re-check the spawner's null guards.

- [ ] **Step 4: Standalone Battle scene play-from-scene**

1. Open `Assets/Scenes/Battle.unity` directly. With no `GameManager` / `PendingBattle`, the controller uses `_enemyData` from the Inspector.
2. Set `BattleController._enemyData` to `ED_IceSlime`. Press Play.
3. **Verify:** Ice Slime visuals spawn correctly under `EnemySpawnAnchor`. Battle plays through.
4. Reset: clear `_enemyData` slot if you want true standalone (no enemy data) testing — battle should still load with the default `_enemyStats` placeholder values; no spawner activity since `data == null` returns the (empty) fallback.

- [ ] **Step 5: Tutorial flow — `BattleTutorialMode.FirstBattle` on Ice Slime**

1. From a fresh save, trigger the first Ice Slime encounter that should fire `BattleTutorialMode.FirstBattle`.
2. **Verify:** Tutorial overlay appears. Ice Slime visual renders correctly. Tutorial gates UI as before — the prefab swap does not interfere.

- [ ] **Step 6: Tutorial flow — `SpellTutorial` on FrostMeltspawn**

1. Reach the `SpellTutorial` encounter (FrostMeltspawn).
2. **Verify:** FrostMeltspawn visual renders. Spell tutorial flow proceeds normally.

- [ ] **Step 7: Run all Edit Mode tests one more time**

Unity Editor → Test Runner → EditMode → Run All.
Expected: every test in the suite PASSES.

- [ ] **Step 8: Acceptance Criteria check-off**

Re-read the Jira ticket DEV-89 Acceptance Criteria. Tick each box:

| AC bullet | How verified |
|---|---|
| `EnemyData` exposes `battleVisualPrefab` | Task 1 step 3 + EnemyDataTests |
| `BattleController` instantiates prefab and reassigns `_enemyAnimator` before `Initialize()` | Task 3 step 3 + Step 4 of this task |
| `Battle.unity` no longer contains static `Ice Slime (Battle)` | Task 5 steps 4 + 7 |
| Level-1 `ED_*.asset` have `battleVisualPrefab` (or tracked) | Task 4 + Task 7 |
| Missing prefabs tracked | Task 7 |
| Each enemy type renders its own visuals | Steps 1-3 of this task |
| Standalone Battle play-from-scene works | Step 4 of this task |
| Tutorial flows still work | Steps 5-6 of this task |
| Edit Mode tests pass | Step 7 of this task |

If any AC fails, **do not proceed to merge** — re-open the failing task or open a new bug.

---

## Task 7: Track missing battle prefabs (follow-up Jira tickets)

> **Atlassian/Jira task (user):** Two new bug tickets, plus links from DEV-89.

- [ ] **Step 1: Create `FrostbiteCreeper` follow-up bug ticket**

Title: `Build FrostbiteCreeper battle prefab + wire to ED_FrostbiteCreeper`
Type: Bug (or Task — author's call)
Labels: `phase-4-bridge`, `bug`, `unity`, `content`
Description should reference DEV-89 and DEV-46. Acceptance:
- [ ] `FrostbiteCreeperBattle.prefab` authored under `Assets/Prefabs/Enemies/Level 1/` with SpriteRenderer + Animator + EnemyBattleAnimator (attack offsets tuned to feel right against player).
- [ ] `ED_FrostbiteCreeper.battleVisualPrefab` field wired.
- [ ] In-game playtest: triggering a FrostbiteCreeper encounter renders the FrostbiteCreeper visual without falling back to the warning path.

- [ ] **Step 2: Create `FrostMeltSentinel` follow-up bug ticket**

Title: `Build FrostMeltSentinel battle prefab + wire to ED_FrostMeltSentinel`
Same fields as Step 1, swapping the enemy name.

- [ ] **Step 3: Link the new tickets to DEV-89 and DEV-46**

In Jira → DEV-89 → Add link → "is blocked by" the two new tickets (DEV-46 cannot be fully closed until they ship).
Also add "relates to" links between DEV-89 and DEV-46.

- [ ] **Step 4: Comment on DEV-89 with the follow-up ticket numbers**

Add a comment to DEV-89 listing the two new ticket IDs so reviewers can see the follow-up scope without leaving the ticket.

- [ ] **Step 5: Transition DEV-89 to Done**

Once Tasks 1–6 are checked in and AC is satisfied, transition DEV-89 to `Done` in Jira.

---

## Architecture Notes

### Why a plain-C# `EnemyVisualSpawner` instead of inlining the logic in `BattleController`

GAME_PLAN.md §10 mandates: *"MonoBehaviours handle Unity lifecycle only. All logic lives in plain C# classes/services injected into them."* Inlining `Object.Instantiate` + null-guard logic in `BattleController.Start()` would violate that. The spawner mirrors `BattleEnvironmentService` (DEV-80) and is unit-testable in Edit Mode without scene loading.

### Why instantiate before `InitializeFromTransition()` instead of inside `Initialize()`

`Initialize()` wires animator events: `_enemyAnimator.OnHitFrame += FireEnemyDamageVisuals;`, `_enemyAnimator.OnAttackSequenceComplete += OnEnemySequenceComplete;`, `_playerAnimator.OnSpellFireFrame += FireSpellVisuals;`. These subscriptions must hook the live runtime instance — if we instantiate **after** `Initialize()`, every event subscription points at the deleted scene placeholder (or null) and combat visuals never fire.

`Start()` runs once per scene load on every BattleController in the scene. The spawn happens unconditionally there, then `Initialize()` (called either immediately or via `OnSceneReady`) sees the freshly-assigned `_enemyAnimator`.

### Why the spawner has a `DestroySafely` helper

The failed-spawn cleanup branch (prefab missing `EnemyBattleAnimator`) needs to destroy the half-instantiated GameObject so it doesn't leak into the scene. At runtime `Object.Destroy` is the correct choice (Unity defers the actual destruction to end-of-frame), but in Edit Mode tests `Object.Destroy` logs `"Destroy may not be called from edit mode! Use Object.DestroyImmediate instead."` — and Unity Test Framework fails any test that surfaces an unexpected `LogType.Error`. The `DestroySafely` helper branches on `Application.isPlaying` so the same code path is correct in both contexts. Tests can call into this branch via the no-animator fake prefab without the test failing on a Unity-emitted error.

### Why no separate `BattleControllerTests`

`BattleController` is a MonoBehaviour wrapper — its only DEV-89 logic is two new lines that delegate to `EnemyVisualSpawner.Spawn`. The spawner has full Edit Mode coverage (Task 2). Wrapping the wrapper in a Play Mode test would require scene loading, animator stub objects, and Unity lifecycle — disproportionate for two lines of dependency injection. Manual playtest (Task 6) covers the end-to-end behavior. This matches the testing pattern established by DEV-80 (`BattleEnvironmentService` is unit-tested; the controller wiring is playtested).

### Why leave the Inspector `_enemyAnimator` slot empty after Task 5

After deleting the static `Ice Slime (Battle)` from the scene, the Inspector reference becomes `None`. The runtime path always overwrites `_enemyAnimator` from the spawner's return value (either the spawned animator or the original `fallback` which **is** `_enemyAnimator`). Leaving it empty makes the data flow obvious — the live animator comes from `EnemyData.battleVisualPrefab`, never from a hand-placed scene object. A future test scene could optionally assign a stub animator here for non-data-driven testing; production scenes do not need it.

---

## Acceptance Criteria Verification Matrix

| AC bullet (from Jira DEV-89) | Plan task |
|---|---|
| `EnemyData` exposes a `battleVisualPrefab` field | Task 1 |
| `BattleController` instantiates `_enemyData.battleVisualPrefab` under `_enemySpawnAnchor` in `Start()` and reassigns `_enemyAnimator` before `Initialize()` runs | Task 3 |
| `Assets/Scenes/Battle.unity` no longer contains a static `Ice Slime (Battle)` instance — only the `EnemySpawnAnchor` Transform | Task 5 |
| All Level 1 `ED_*.asset` have `battleVisualPrefab` assigned (or tracked) | Tasks 4 + 7 |
| Missing battle prefabs for `FrostbiteCreeper` and `FrostMeltSentinel` tracked as follow-up | Task 7 |
| Triggering a battle against each enemy type produces matching visuals | Task 6 steps 1-3 |
| Standalone Battle scene testing still works | Task 6 step 4 |
| Tutorial flows continue to work | Task 6 steps 5-6 |
| Edit Mode tests still pass | Task 6 step 7 |
