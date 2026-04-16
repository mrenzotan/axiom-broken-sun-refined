# DEV-38: Spell Unlock System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player gain new castable spells through level-up thresholds and direct story-trigger calls, and have each unlock rebuild the Vosk grammar so the new spell is recognizable in the current session.

**Architecture:** `SpellData` already has a `SpellUnlockCondition unlockCondition` (shipped in DEV-37) with `requiredLevel` (int) and `prerequisiteSpell` (SpellData ref). DEV-38 extends this: `requiredLevel = 0` means story-only (never auto-granted by level); `requiredLevel >= 1` means auto-granted when the player reaches that level, provided any `prerequisiteSpell` is already unlocked. A new `SpellCatalog` ScriptableObject holds every `SpellData` and exposes lookup by `spellName`. `SpellUnlockService` (plain C# in `Axiom.Core`) owns the runtime unlocked set, de-duplicates, and fires `OnSpellUnlocked`. `GameManager` owns the service instance and bridges it to `PlayerState.UnlockedSpellIds` for save/load. `BattleVoiceBootstrap` subscribes to `OnSpellUnlocked` and hot-swaps the `VoskRecognizer` via `SpellVocabularyManager.RebuildRecognizerAsync` so new spells become speakable mid-battle.

**Tech Stack:** Unity 6 LTS, C# (.NET Standard 2.1), ScriptableObject, NUnit via Unity Test Framework (Edit Mode only — service and catalog are plain C#), Vosk C# bindings.

---

## Project context (read before coding)

| Source | What applies to this ticket |
|--------|-----------------------------|
| Jira DEV-38 AC | Plain-C# `SpellUnlockService`, event on unlock, level-triggered grants via `SpellData` threshold, story-trigger `Unlock(SpellData)`, grammar rebuild, duplicate calls silently ignored |
| `CLAUDE.md` — Non-Negotiable Code Standards | MonoBehaviours = lifecycle only; no static singletons except `GameManager`; ScriptableObject-driven data; no premature abstraction |
| `CLAUDE.md` — Voice Architecture | Grammar restricted to currently unlocked spells; grammar rebuild uses `SpellVocabularyManager.RebuildRecognizerAsync` (background thread) |
| `docs/GAME_PLAN.md` §Phase 5 | Phase-5 data/progression work; level progression drives new-spell grants |
| `docs/VERSION_CONTROL.md` | UVCS is source of truth; commit format `<type>(DEV-##): <desc>` |
| `Assets/Scripts/Data/SpellData.cs` | Already has `unlockCondition` (`SpellUnlockCondition`) — DEV-37 shipped this; no field additions needed |
| `Assets/Scripts/Data/SpellUnlockCondition.cs` | Change `[Min(1)]` to `[Min(0)]` so `requiredLevel = 0` can represent story-only spells |
| `Assets/Scripts/Voice/SpellVocabularyManager.cs` | Static helpers — call `RebuildRecognizerAsync(model, sampleRate, spells)` for hot-swap |
| `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` | Currently Inspector-assigns `_unlockedSpells`; will switch to reading from `GameManager` at Play Mode startup and rebuild on unlock events |
| `Assets/Scripts/Core/GameManager.cs` | Owns `PlayerState`, already serializes `UnlockedSpellIds`; will own `SpellUnlockService` |
| `Assets/Scripts/Core/PlayerState.cs` | `Level`, `UnlockedSpellIds` (List&lt;string&gt;) already exist |

---

## Current state (repository)

**Already implemented (by DEV-37):**

- `SpellData` SO with `spellName`, MP cost, effect type, `ChemistryConcept concept`, `SpellUnlockCondition unlockCondition`, `List<ReactionEntry> reactions`, chemistry condition fields, VFX/SFX fields.
- `SpellUnlockCondition` (`[Serializable]` class) with `requiredLevel` (int, `[Min(1)]`), `prerequisiteSpell` (SpellData ref), and `IsUnlockedFor(int playerLevel, IReadOnlyCollection<string> unlockedSpellNames)` method.
- `ReactionEntry` (`[Serializable]` class) — `reactsWith`, `reactionBonusDamage`, `transformsTo`, `transformationDuration`.
- `SpellEffectResolver` already iterates `spell.reactions` (first-match-wins).
- `ChemistryConcept` enum — UI classification tag on SpellData.
- `ItemData`, `CharacterData`, `LootEntry`, `ItemType`, `ItemEffectType` ScriptableObjects / enums.
- `EnemyData` extended with `xpReward` and `List<LootEntry> loot`.
- Three sample spells under `Assets/Data/Spells/` (`SD_Freeze`, `SD_Combust`, `SD_Neutralize`).
- `SpellVocabularyManager.BuildGrammarJson` / `RebuildRecognizerAsync` — stateless static helpers; grammar rebuild off main thread.
- `BattleVoiceBootstrap.Start()` — loads model, builds recognizer once from inspector-assigned `SpellData[]`.
- `PlayerState.Level`, `PlayerState.UnlockedSpellIds`, `SaveData.unlockedSpellIds` (persistence round-trips string IDs).
- `GameManager` singleton owns `PlayerState`; `ApplyProgression(level, xp)` exists.

**Missing (scope of DEV-38):**

- `SpellUnlockCondition.requiredLevel` `[Min(1)]` → `[Min(0)]` change — needed so `requiredLevel = 0` can represent story-only spells (never auto-granted by level).
- `SpellCatalog` ScriptableObject (global registry — resolves spell IDs ↔ `SpellData`).
- `SpellUnlockService` (plain C#, event-emitting, dedupe-by-spellName, uses `IsUnlockedFor()` to respect prerequisite chains).
- `GameManager` hook that constructs + exposes the service and drives level-based unlocks.
- Live grammar rebuild subscription in `BattleVoiceBootstrap`.

**Dependency:** DEV-37 must be fully committed before Task 1 lands, since `SpellUnlockCondition.cs` is a DEV-37 file being modified here.

**Out of scope:** XP gain (DEV-40 / covered elsewhere — this plan only reacts to a level number); main-menu UI; Phase 6 content spells. The plan treats "reached level N" as an external notification into `SpellUnlockService.NotifyPlayerLevel(N)`.

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Data/SpellUnlockCondition.cs` | Change `[Min(1)]` to `[Min(0)]` so `requiredLevel = 0` can represent story-only spells |
| Create | `Assets/Scripts/Data/SpellCatalog.cs` | ScriptableObject wrapping `SpellData[]`; lookup by spellName; filter by `unlockCondition.requiredLevel` threshold |
| Create | `Assets/Scripts/Core/SpellUnlockService.cs` | Plain C# service — owns unlocked set, fires `OnSpellUnlocked`, uses `IsUnlockedFor()` to respect prerequisite chains |
| Modify | `Assets/Scripts/Core/GameManager.cs` | Instantiate `SpellUnlockService`, expose via property, drive `PlayerState.UnlockedSpellIds`, call `NotifyPlayerLevel` from `ApplyProgression` |
| Modify | `Assets/Scripts/Voice/Axiom.Voice.asmdef` | Add `Axiom.Core` reference (so `BattleVoiceBootstrap` can see the service) |
| Modify | `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` | Pull initial spell list from `GameManager`, subscribe to `OnSpellUnlocked`, hot-swap recognizer |
| Create | `Assets/Tests/Editor/Core/SpellUnlockServiceTests.cs` | Edit Mode tests — starter state, duplicate unlock no-op, event fires once per new spell, level grants, prerequisite chains |
| Create | `Assets/Tests/Editor/Core/SpellCatalogTests.cs` | Edit Mode tests — lookup, level filter, null guards |
| Modify | `Assets/Tests/Editor/Core/CoreTests.asmdef` | Ensure references cover `Axiom.Data` (likely already does — verify in Task 7) |

**No new asmdefs.** `SpellUnlockService` and its tests piggyback on the existing `Axiom.Core` / `CoreTests` assemblies.

---

## Task 1: Allow `requiredLevel = 0` on `SpellUnlockCondition` for story-only spells

DEV-37 shipped `SpellUnlockCondition` with `[Min(1)] public int requiredLevel = 1`, meaning every spell must be at least level 1. DEV-38 needs `requiredLevel = 0` to mean "story-only — never auto-granted by level, only unlocked via `SpellUnlockService.Unlock(SpellData)` calls from story triggers."

**Files:**
- Modify: `Assets/Scripts/Data/SpellUnlockCondition.cs`

**Dependency:** DEV-37 must be fully committed before this task lands.

- [ ] **Step 1: Change `[Min(1)]` to `[Min(0)]` on `requiredLevel`**

Open `Assets/Scripts/Data/SpellUnlockCondition.cs`. Change:

```csharp
        [Min(1)] public int requiredLevel = 1;
```

to:

```csharp
        [Tooltip("Minimum player level required to unlock this spell. 0 = story-only (never auto-granted by level). 1 = starter spell (granted at level 1 or above).")]
        [Min(0)] public int requiredLevel = 1;
```

Note: the default remains `1` (starter spell) — only spells explicitly set to `0` in the Inspector are story-only.

- [ ] **Step 2: Verify Unity compiles**

> **Unity Editor task (user):** Switch to Unity. Wait for the scripts to reload. Open **Window → General → Console**. Confirm zero compile errors. The existing spell assets' `Unlock Condition → Required Level` should still show `1`.

- [ ] **Step 3: Verify existing spell assets are starter spells**

> **Unity Editor task (user):** In the Project window, select each of the three existing spell assets and confirm `Unlock Condition → Required Level = 1` (default from DEV-37):
> - `Assets/Data/Spells/SD_Freeze.asset` → Required Level = 1
> - `Assets/Data/Spells/SD_Combust.asset` → Required Level = 1
> - `Assets/Data/Spells/SD_Neutralize.asset` → Required Level = 1
>
> No changes needed — they're already starter spells.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-38): allow requiredLevel 0 on SpellUnlockCondition for story-only spells`
- `Assets/Scripts/Data/SpellUnlockCondition.cs`

---

## Task 2: Create `SpellCatalog` ScriptableObject + tests

**Files:**
- Create: `Assets/Scripts/Data/SpellCatalog.cs`
- Create: `Assets/Tests/Editor/Core/SpellCatalogTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/SpellCatalogTests.cs`:

```csharp
using System.Linq;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Core
{
    public class SpellCatalogTests
    {
        private static SpellData MakeSpell(string name, int requiredLevel)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            spell.unlockCondition = new SpellUnlockCondition { requiredLevel = requiredLevel };
            return spell;
        }

        private static SpellCatalog MakeCatalog(params SpellData[] spells)
        {
            SpellCatalog catalog = ScriptableObject.CreateInstance<SpellCatalog>();
            catalog.SetSpellsForTests(spells);
            return catalog;
        }

        [Test]
        public void TryGetByName_ReturnsMatchingSpell()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellCatalog catalog = MakeCatalog(freeze);

            bool found = catalog.TryGetByName("freeze", out SpellData result);

            Assert.IsTrue(found);
            Assert.AreSame(freeze, result);
        }

        [Test]
        public void TryGetByName_ReturnsFalseForMissingSpell()
        {
            SpellCatalog catalog = MakeCatalog(MakeSpell("freeze", 1));

            bool found = catalog.TryGetByName("combust", out SpellData result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetByName_NullOrEmptyReturnsFalse()
        {
            SpellCatalog catalog = MakeCatalog(MakeSpell("freeze", 1));

            Assert.IsFalse(catalog.TryGetByName(null, out _));
            Assert.IsFalse(catalog.TryGetByName(string.Empty, out _));
        }

        [Test]
        public void GetUnlocksAtOrBelowLevel_IncludesMatchingLevels()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData midgame = MakeSpell("combust", 3);
            SpellData lategame = MakeSpell("crystal spike", 5);
            SpellCatalog catalog = MakeCatalog(starter, midgame, lategame);

            SpellData[] result = catalog.GetUnlocksAtOrBelowLevel(3).ToArray();

            CollectionAssert.AreEquivalent(new[] { starter, midgame }, result);
        }

        [Test]
        public void GetUnlocksAtOrBelowLevel_ExcludesStoryOnlySpells()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData storyOnly = MakeSpell("ancient burn", 0);
            SpellCatalog catalog = MakeCatalog(starter, storyOnly);

            SpellData[] result = catalog.GetUnlocksAtOrBelowLevel(10).ToArray();

            CollectionAssert.AreEquivalent(new[] { starter }, result);
        }

        [Test]
        public void GetUnlocksAtOrBelowLevel_ZeroLevelReturnsEmpty()
        {
            SpellCatalog catalog = MakeCatalog(MakeSpell("freeze", 1));

            SpellData[] result = catalog.GetUnlocksAtOrBelowLevel(0).ToArray();

            CollectionAssert.IsEmpty(result);
        }
    }
}
```

- [ ] **Step 2: Run the tests — they must fail at compile time**

> **Unity Editor task (user):** Open **Window → General → Test Runner** → Edit Mode tab → Run All. Expected: compile error referencing `SpellCatalog` not found. Keep the Test Runner open.

- [ ] **Step 3: Implement `SpellCatalog`**

Create `Assets/Scripts/Data/SpellCatalog.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Registry of every <see cref="SpellData"/> in the game. Resolves string IDs
    /// (spellName) to <see cref="SpellData"/> assets for save/load round-tripping,
    /// and filters spells by <see cref="SpellUnlockCondition.requiredLevel"/> for level-up grants.
    ///
    /// One asset per project, referenced by <c>GameManager</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellCatalog", menuName = "Axiom/Data/Spell Catalog")]
    public class SpellCatalog : ScriptableObject
    {
        [Tooltip("All SpellData assets in the game. Order irrelevant. Spell names must be unique.")]
        [SerializeField] private SpellData[] _spells = System.Array.Empty<SpellData>();

        public IReadOnlyList<SpellData> AllSpells => _spells;

        /// <summary>
        /// Looks up a spell by its <see cref="SpellData.spellName"/> (lowercase).
        /// Returns false and null when <paramref name="spellName"/> is null, empty,
        /// or not present in the catalog.
        /// </summary>
        public bool TryGetByName(string spellName, out SpellData spell)
        {
            spell = null;
            if (string.IsNullOrWhiteSpace(spellName)) return false;
            if (_spells == null) return false;

            for (int i = 0; i < _spells.Length; i++)
            {
                SpellData candidate = _spells[i];
                if (candidate == null) continue;
                if (candidate.spellName == spellName)
                {
                    spell = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns every spell with <c>unlockCondition.requiredLevel &gt; 0</c> and
        /// <c>unlockCondition.requiredLevel &lt;= level</c>.
        /// Story-only spells (<c>requiredLevel == 0</c>) are excluded — they must be granted via
        /// <c>SpellUnlockService.Unlock(SpellData)</c>.
        /// Note: this filters by level only. Prerequisite checks are handled by
        /// <c>SpellUnlockService.NotifyPlayerLevel</c> using <c>IsUnlockedFor()</c>.
        /// </summary>
        public IEnumerable<SpellData> GetUnlocksAtOrBelowLevel(int level)
        {
            if (_spells == null) yield break;
            for (int i = 0; i < _spells.Length; i++)
            {
                SpellData candidate = _spells[i];
                if (candidate == null) continue;
                int reqLevel = candidate.unlockCondition != null
                    ? candidate.unlockCondition.requiredLevel
                    : 1;
                if (reqLevel <= 0) continue;
                if (reqLevel > level) continue;
                yield return candidate;
            }
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Test-only hook — lets Edit Mode tests populate the catalog without asset serialization.
        /// </summary>
        internal void SetSpellsForTests(SpellData[] spells) => _spells = spells ?? System.Array.Empty<SpellData>();
#endif
    }
}
```

- [ ] **Step 4: Confirm `CoreTests.asmdef` references `Axiom.Data`**

Read `Assets/Tests/Editor/Core/CoreTests.asmdef`. The `references` array must include `"Axiom.Data"`. If it does not, add it so the final file looks like (preserve all existing entries — only ADD `"Axiom.Data"` if missing):

```json
{
    "name": "CoreTests",
    "references": [
        "Axiom.Core",
        "Axiom.Data"
    ],
    "testReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

If other fields already exist (e.g. `"nunit.framework.dll"` under precompiledReferences), preserve them exactly — only the `references` array is being modified.

- [ ] **Step 5: Run the tests — they must pass**

> **Unity Editor task (user):** In the Test Runner → Edit Mode tab → Run All. Expected: all six `SpellCatalogTests` pass.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-38): add SpellCatalog scriptable object`
- `Assets/Scripts/Data/SpellCatalog.cs`
- `Assets/Scripts/Data/SpellCatalog.cs.meta`
- `Assets/Tests/Editor/Core/SpellCatalogTests.cs`
- `Assets/Tests/Editor/Core/SpellCatalogTests.cs.meta`
- `Assets/Tests/Editor/Core/CoreTests.asmdef` *(only if modified in Step 4)*

---

## Task 3: Create `SpellUnlockService` + tests

**Files:**
- Create: `Assets/Scripts/Core/SpellUnlockService.cs`
- Create: `Assets/Tests/Editor/Core/SpellUnlockServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/SpellUnlockServiceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Core
{
    public class SpellUnlockServiceTests
    {
        private static SpellData MakeSpell(string name, int requiredLevel = 0, SpellData prerequisite = null)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            spell.unlockCondition = new SpellUnlockCondition
            {
                requiredLevel = requiredLevel,
                prerequisiteSpell = prerequisite
            };
            return spell;
        }

        private static SpellCatalog MakeCatalog(params SpellData[] spells)
        {
            SpellCatalog catalog = ScriptableObject.CreateInstance<SpellCatalog>();
            catalog.SetSpellsForTests(spells);
            return catalog;
        }

        [Test]
        public void NewService_HasEmptyUnlockedList()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog());

            Assert.AreEqual(0, service.UnlockedSpells.Count);
        }

        [Test]
        public void Unlock_AddsSpell_AndFiresEvent()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(freeze));

            var fired = new List<SpellData>();
            service.OnSpellUnlocked += fired.Add;

            bool result = service.Unlock(freeze);

            Assert.IsTrue(result);
            CollectionAssert.Contains(service.UnlockedSpells, freeze);
            CollectionAssert.AreEqual(new[] { freeze }, fired);
        }

        [Test]
        public void Unlock_DuplicateCall_IsSilentlyIgnored()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(freeze));

            int fireCount = 0;
            service.OnSpellUnlocked += _ => fireCount++;

            service.Unlock(freeze);
            bool secondResult = service.Unlock(freeze);

            Assert.IsFalse(secondResult);
            Assert.AreEqual(1, service.UnlockedSpells.Count);
            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void Unlock_NullSpell_ThrowsArgumentNullException()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog());

            Assert.Throws<ArgumentNullException>(() => service.Unlock(null));
        }

        [Test]
        public void Contains_ReturnsTrueForUnlockedSpell()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(freeze));
            service.Unlock(freeze);

            Assert.IsTrue(service.Contains(freeze));
        }

        [Test]
        public void Contains_NullReturnsFalse()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog());

            Assert.IsFalse(service.Contains(null));
        }

        [Test]
        public void NotifyPlayerLevel_GrantsEligibleSpells_AndFiresEventPerNewSpell()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData midgame = MakeSpell("combust", 3);
            SpellData lategame = MakeSpell("crystal spike", 5);
            SpellData storyOnly = MakeSpell("ancient burn", 0);

            SpellUnlockService service = new SpellUnlockService(
                MakeCatalog(starter, midgame, lategame, storyOnly));

            var fired = new List<SpellData>();
            service.OnSpellUnlocked += fired.Add;

            service.NotifyPlayerLevel(3);

            CollectionAssert.AreEquivalent(new[] { starter, midgame }, service.UnlockedSpells);
            CollectionAssert.AreEquivalent(new[] { starter, midgame }, fired);
        }

        [Test]
        public void NotifyPlayerLevel_DoesNotRefireForAlreadyUnlockedSpells()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData midgame = MakeSpell("combust", 3);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(starter, midgame));

            service.NotifyPlayerLevel(1); // grants starter

            var firedAfterL1 = new List<SpellData>();
            service.OnSpellUnlocked += firedAfterL1.Add;

            service.NotifyPlayerLevel(3); // should only grant midgame

            CollectionAssert.AreEqual(new[] { midgame }, firedAfterL1);
            CollectionAssert.AreEquivalent(new[] { starter, midgame }, service.UnlockedSpells);
        }

        [Test]
        public void NotifyPlayerLevel_RespectsPrerequisiteChains()
        {
            // Spell A (level 3, no prereq) → Spell B (level 3, requires A)
            // Both become level-eligible at L3, but B can only unlock after A.
            // A single NotifyPlayerLevel(3) call should grant both in order.
            SpellData spellA = MakeSpell("freeze", 3);
            SpellData spellB = MakeSpell("shatter", 3, prerequisite: spellA);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(spellA, spellB));

            var fired = new List<SpellData>();
            service.OnSpellUnlocked += fired.Add;

            service.NotifyPlayerLevel(3);

            CollectionAssert.AreEquivalent(new[] { spellA, spellB }, service.UnlockedSpells);
            CollectionAssert.AreEquivalent(new[] { spellA, spellB }, fired);
        }

        [Test]
        public void NotifyPlayerLevel_DoesNotGrantSpellWhenPrerequisiteNotMet()
        {
            SpellData spellA = MakeSpell("freeze", 5); // higher level than B
            SpellData spellB = MakeSpell("shatter", 3, prerequisite: spellA);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(spellA, spellB));

            service.NotifyPlayerLevel(3);

            // B's level is met but prereq (A, level 5) is not unlocked yet
            CollectionAssert.IsEmpty(service.UnlockedSpells);
        }

        [Test]
        public void Constructor_NullCatalog_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SpellUnlockService(null));
        }

        [Test]
        public void RestoreFromIds_PopulatesUnlockedSpellsWithoutFiringEvent()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(starter));

            int fireCount = 0;
            service.OnSpellUnlocked += _ => fireCount++;

            service.RestoreFromIds(new[] { "freeze" });

            CollectionAssert.AreEqual(new[] { starter }, service.UnlockedSpells);
            Assert.AreEqual(0, fireCount, "Save-load restore must not fire unlock events.");
        }

        [Test]
        public void RestoreFromIds_IgnoresUnknownIds()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(MakeSpell("freeze", 1)));

            service.RestoreFromIds(new[] { "freeze", "nonexistent", null, string.Empty });

            Assert.AreEqual(1, service.UnlockedSpells.Count);
        }
    }
}
```

- [ ] **Step 2: Run the tests — they must fail at compile time**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Expected: compile error referencing `SpellUnlockService` not found.

- [ ] **Step 3: Implement `SpellUnlockService`**

Create `Assets/Scripts/Core/SpellUnlockService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# service that owns the player's unlocked-spell set at runtime and
    /// fires <see cref="OnSpellUnlocked"/> each time a new spell is granted.
    ///
    /// Ownership:  <c>GameManager</c> constructs and holds the singleton instance.
    /// Threading:  all methods run on the Unity main thread. The event is invoked synchronously.
    /// De-dupe:    duplicate <see cref="Unlock"/> calls are silently ignored — the event
    ///             only fires the first time a given spell is added.
    /// Persistence: <see cref="UnlockedSpellNames"/> returns the ordered list of spell names
    ///             for <c>SaveData.unlockedSpellIds</c>. <see cref="RestoreFromIds"/> repopulates
    ///             state on load WITHOUT firing <see cref="OnSpellUnlocked"/>.
    /// </summary>
    public sealed class SpellUnlockService
    {
        private readonly SpellCatalog _catalog;
        private readonly List<SpellData> _unlocked = new List<SpellData>();
        private readonly HashSet<string> _unlockedNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Fires synchronously on each new unlock, receiving the spell just granted.
        /// Does NOT fire for duplicate <see cref="Unlock"/> calls or during <see cref="RestoreFromIds"/>.
        /// </summary>
        public event Action<SpellData> OnSpellUnlocked;

        public SpellUnlockService(SpellCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>Read-only view of the unlocked spells in the order they were granted.</summary>
        public IReadOnlyList<SpellData> UnlockedSpells => _unlocked;

        /// <summary>Ordered list of spellNames — for SaveData.unlockedSpellIds.</summary>
        public IReadOnlyList<string> UnlockedSpellNames
        {
            get
            {
                string[] names = new string[_unlocked.Count];
                for (int i = 0; i < _unlocked.Count; i++) names[i] = _unlocked[i].spellName;
                return names;
            }
        }

        /// <summary>
        /// Grants the spell and fires <see cref="OnSpellUnlocked"/>.
        /// Returns true on first unlock, false if already unlocked.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="spell"/> is null.</exception>
        public bool Unlock(SpellData spell)
        {
            if (spell == null) throw new ArgumentNullException(nameof(spell));
            return UnlockInternal(spell, fireEvent: true);
        }

        /// <summary>Returns true if the spell is already unlocked. Null returns false.</summary>
        public bool Contains(SpellData spell)
        {
            if (spell == null) return false;
            return _unlockedNames.Contains(spell.spellName);
        }

        /// <summary>
        /// Auto-grants every catalog spell whose <c>unlockCondition.IsUnlockedFor()</c> returns
        /// true given the player's level and the current unlocked set. Story-only spells
        /// (<c>requiredLevel == 0</c>) are excluded — they must be granted via <see cref="Unlock"/>.
        ///
        /// Loops until stable to resolve prerequisite chains: if spell A (level 3, no prereq)
        /// unlocks, spell B (level 3, requires A) becomes eligible on the next pass.
        /// Bounded by catalog size — at most N passes for N spells.
        /// </summary>
        public void NotifyPlayerLevel(int playerLevel)
        {
            bool grantedAny;
            do
            {
                grantedAny = false;
                foreach (SpellData candidate in _catalog.GetUnlocksAtOrBelowLevel(playerLevel))
                {
                    if (_unlockedNames.Contains(candidate.spellName)) continue;
                    if (candidate.unlockCondition != null
                        && !candidate.unlockCondition.IsUnlockedFor(playerLevel, _unlockedNames))
                        continue;

                    if (UnlockInternal(candidate, fireEvent: true))
                        grantedAny = true;
                }
            } while (grantedAny);
        }

        /// <summary>
        /// Rebuilds the unlocked set from a list of persisted spell names
        /// (e.g. <c>SaveData.unlockedSpellIds</c>). Does NOT fire <see cref="OnSpellUnlocked"/>.
        /// Unknown IDs (not present in the catalog) and null/whitespace entries are silently skipped.
        /// Replaces the current set entirely.
        /// </summary>
        public void RestoreFromIds(IEnumerable<string> spellIds)
        {
            _unlocked.Clear();
            _unlockedNames.Clear();

            if (spellIds == null) return;

            foreach (string id in spellIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!_catalog.TryGetByName(id, out SpellData spell)) continue;
                UnlockInternal(spell, fireEvent: false);
            }
        }

        private bool UnlockInternal(SpellData spell, bool fireEvent)
        {
            if (!_unlockedNames.Add(spell.spellName)) return false;
            _unlocked.Add(spell);
            if (fireEvent) OnSpellUnlocked?.Invoke(spell);
            return true;
        }
    }
}
```

- [ ] **Step 4: Run the tests — they must all pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Expected: all thirteen `SpellUnlockServiceTests` + prior `SpellCatalogTests` pass. Zero failures.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-38): add SpellUnlockService`
- `Assets/Scripts/Core/SpellUnlockService.cs`
- `Assets/Scripts/Core/SpellUnlockService.cs.meta`
- `Assets/Tests/Editor/Core/SpellUnlockServiceTests.cs`
- `Assets/Tests/Editor/Core/SpellUnlockServiceTests.cs.meta`

---

## Task 4: Wire `SpellUnlockService` into `GameManager`

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

- [ ] **Step 1: Add `SpellCatalog` serialized field and service property**

Open `Assets/Scripts/Core/GameManager.cs`. Add a `using UnityEngine;` import if not present (already present in file). At the class level, directly below the `public static GameManager Instance { get; private set; }` line, insert:

```csharp
        [SerializeField]
        [Tooltip("Master catalog of every SpellData in the game. Required for level-up spell grants and save-load ID resolution.")]
        private SpellCatalog _spellCatalog;

        private SpellUnlockService _spellUnlockService;

        /// <summary>
        /// Runtime-owned spell unlock service. Lazily constructed on first access so
        /// Edit Mode tests work without Awake running.
        /// Subscribes: BattleVoiceBootstrap listens to OnSpellUnlocked for live grammar rebuild.
        /// </summary>
        public SpellUnlockService SpellUnlockService
        {
            get
            {
                EnsureSpellUnlockService();
                return _spellUnlockService;
            }
        }
```

Add the `using Axiom.Data;` import at the top — actually the file already imports `Axiom.Data`. Verify by reading the top of the file; no import change needed.

- [ ] **Step 2: Add the `EnsureSpellUnlockService` helper**

Directly below the existing `EnsureSaveService()` method, add:

```csharp
        private void EnsureSpellUnlockService()
        {
            if (_spellUnlockService != null) return;

            if (_spellCatalog == null)
            {
                Debug.LogError(
                    "[GameManager] SpellCatalog is not assigned — SpellUnlockService cannot be constructed. " +
                    "Assign it in the Inspector on the GameManager prefab.", this);
                return;
            }

            _spellUnlockService = new SpellUnlockService(_spellCatalog);
            _spellUnlockService.OnSpellUnlocked += HandleSpellUnlocked;
        }

        private void HandleSpellUnlocked(SpellData _)
        {
            // Mirror the unlocked set into PlayerState so SaveData round-trips correctly.
            EnsurePlayerState();
            PlayerState.SetUnlockedSpellIds(_spellUnlockService.UnlockedSpellNames);
        }
```

- [ ] **Step 3: Call `NotifyPlayerLevel` from `ApplyProgression`-related flows**

Still in `GameManager.cs`, locate the `ApplySaveData` method. Directly after the existing line `PlayerState.SetUnlockedSpellIds(data.unlockedSpellIds ?? Array.Empty<string>());`, insert a call to restore the service from the loaded IDs and then re-apply level grants:

```csharp
            EnsureSpellUnlockService();
            _spellUnlockService?.RestoreFromIds(data.unlockedSpellIds ?? Array.Empty<string>());
            _spellUnlockService?.NotifyPlayerLevel(PlayerState.Level);
```

The full block now reads:

```csharp
            PlayerState.ApplyVitals(targetMaxHp, targetMaxMp, data.currentHp, data.currentMp);
            PlayerState.ApplyProgression(data.playerLevel, data.playerXp);
            PlayerState.SetUnlockedSpellIds(data.unlockedSpellIds ?? Array.Empty<string>());
            EnsureSpellUnlockService();
            _spellUnlockService?.RestoreFromIds(data.unlockedSpellIds ?? Array.Empty<string>());
            _spellUnlockService?.NotifyPlayerLevel(PlayerState.Level);
            PlayerState.SetInventoryItemIds(ExpandInventory(data.inventory));
```

- [ ] **Step 4: Call `NotifyPlayerLevel` from `StartNewGame`**

Still in `GameManager.cs`, in the `StartNewGame()` method, add a call to grant starter spells right after the `EnsureSaveService(); _saveService.DeleteSave();` lines:

```csharp
            EnsureSpellUnlockService();
            if (_spellUnlockService != null)
            {
                // Reset the service by restoring from an empty list, then grant level-1 starters.
                _spellUnlockService.RestoreFromIds(Array.Empty<string>());
                _spellUnlockService.NotifyPlayerLevel(PlayerState.Level);
            }
```

The full `StartNewGame` should now read:

```csharp
        public void StartNewGame()
        {
            PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            ClearPendingBattle();
            ClearWorldSnapshot();
            ClearDefeatedEnemies();

            EnsureSaveService();
            _saveService.DeleteSave();

            EnsureSpellUnlockService();
            if (_spellUnlockService != null)
            {
                _spellUnlockService.RestoreFromIds(Array.Empty<string>());
                _spellUnlockService.NotifyPlayerLevel(PlayerState.Level);
            }

            LoadScene("Platformer");
        }
```

- [ ] **Step 5: Unsubscribe in `OnDestroy`**

Update the existing `OnDestroy` method to release the unlock-event handler:

```csharp
        private void OnDestroy()
        {
            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked -= HandleSpellUnlocked;

            if (Instance == this)
                Instance = null;
        }
```

- [ ] **Step 6: Verify Unity compiles and all existing tests still pass**

> **Unity Editor task (user):** Wait for recompile in Unity. Open Console — zero errors. Open Test Runner → Edit Mode → Run All. Expected: all prior `GameManager*Tests`, `SpellCatalogTests`, `SpellUnlockServiceTests`, `SaveService*Tests` pass.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-38): wire SpellUnlockService into GameManager`
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Scripts/Core/GameManager.cs.meta` *(only if modified — normally unchanged)*

---

## Task 5: Add `Axiom.Core` reference to Voice assembly

**Files:**
- Modify: `Assets/Scripts/Voice/Axiom.Voice.asmdef`

- [ ] **Step 1: Replace the file contents**

Replace `Assets/Scripts/Voice/Axiom.Voice.asmdef` with (adds `"Axiom.Core"` — all other fields unchanged):

```json
{
    "name": "Axiom.Voice",
    "references": [
        "Axiom.Data",
        "Axiom.Battle",
        "Axiom.Core",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Vosk.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Verify Unity compiles**

> **Unity Editor task (user):** Wait for recompile. Open Console — zero errors. (Test Runner is not required here; the next task will re-run tests.)

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-38): add Axiom.Core reference to Axiom.Voice asmdef`
- `Assets/Scripts/Voice/Axiom.Voice.asmdef`

---

## Task 6: Hot-swap Vosk recognizer on `OnSpellUnlocked`

**Files:**
- Modify: `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`

This rewires `BattleVoiceBootstrap` so that (a) it reads the initial unlocked spell list from `GameManager.Instance.SpellUnlockService` in Play Mode (falling back to the Inspector field when no GameManager exists — e.g. Battle scene run in isolation), and (b) it subscribes to `OnSpellUnlocked` and rebuilds the `VoskRecognizer` live each time a new spell is granted.

- [ ] **Step 1: Add imports and fields**

Open `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`. Add this import near the top:

```csharp
using System.Collections.Generic;
using Axiom.Core;
```

(Some of those may already exist — do not duplicate.)

Inside the class, directly below the existing `private Model _voskModel;` field, add:

```csharp
        private SpellUnlockService _spellUnlockService;
        private List<SpellData> _activeSpells;
```

- [ ] **Step 2: Replace the `Start()` coroutine's spell-list acquisition**

Inside `IEnumerator Start()`, locate the existing line:

```csharp
            _voskModel = modelTask.Result;
            SpellData[] spells = _unlockedSpells ?? Array.Empty<SpellData>();
```

Replace those two lines with:

```csharp
            _voskModel = modelTask.Result;

            // Prefer the runtime-owned SpellUnlockService so the recognizer stays in sync
            // with story unlocks + level-up grants. Fall back to the Inspector array
            // when running the Battle scene in isolation (no GameManager in scene).
            _spellUnlockService = GameManager.Instance != null
                ? GameManager.Instance.SpellUnlockService
                : null;

            _activeSpells = _spellUnlockService != null
                ? new List<SpellData>(_spellUnlockService.UnlockedSpells)
                : new List<SpellData>(_unlockedSpells ?? Array.Empty<SpellData>());

            SpellData[] spells = _activeSpells.ToArray();
```

- [ ] **Step 3: Subscribe to `OnSpellUnlocked` after the pipeline is ready**

Still inside `Start()`, directly after the existing final line `Debug.Log("[BattleVoiceBootstrap] Vosk pipeline ready.");`, append:

```csharp
            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked += HandleSpellUnlocked;
```

- [ ] **Step 4: Add the `HandleSpellUnlocked` method**

Below the existing `DisableSpell()` method, add a new method that performs the hot-swap:

```csharp
        private void HandleSpellUnlocked(SpellData newSpell)
        {
            if (newSpell == null) return;
            if (_voskModel == null) return;            // pipeline never initialized — ignore
            if (_recognizerService == null) return;    // no active recognizer to swap

            _activeSpells.Add(newSpell);

            StartCoroutine(RebuildRecognizer(_activeSpells.ToArray()));
        }

        private System.Collections.IEnumerator RebuildRecognizer(SpellData[] spells)
        {
            Task<VoskRecognizer> rebuildTask =
                SpellVocabularyManager.RebuildRecognizerAsync(_voskModel, _sampleRate, spells);

            yield return new WaitUntil(() => rebuildTask.IsCompleted);

            if (rebuildTask.IsFaulted)
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to rebuild Vosk recognizer on spell unlock: " +
                    $"{rebuildTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            VoskRecognizer newRecognizer = rebuildTask.Result;
            if (newRecognizer == null) yield break;  // empty set — should not happen post-init

            // Atomic-enough swap: stop old service (drains queues), hand the new recognizer
            // to a fresh VoskRecognizerService reusing the existing shared queues.
            ConcurrentQueue<short[]> inputQueue = _recognizerService.InputQueue;
            ConcurrentQueue<string>  resultQueue = _recognizerService.ResultQueue;

            _recognizerService.Dispose();

            _recognizerService = new VoskRecognizerService(newRecognizer, inputQueue, resultQueue);
            _recognizerService.Start();

            _microphoneInputHandler.Inject(inputQueue, _recognizerService);
            _spellCastController.Inject(resultQueue, spells);

            Debug.Log($"[BattleVoiceBootstrap] Vosk recognizer rebuilt with {spells.Length} spells after unlock.");
        }
```

> **Note:** This step relies on `VoskRecognizerService` exposing its `InputQueue` and `ResultQueue` as public read-only properties. Verify in the next step.

- [ ] **Step 5: Expose `InputQueue` / `ResultQueue` on `VoskRecognizerService` if not already present**

Open `Assets/Scripts/Voice/VoskRecognizerService.cs`. If the service already holds `_inputQueue` and `_resultQueue` as private fields, add the following two public read-only properties near the top of the class (directly after the field declarations):

```csharp
        public ConcurrentQueue<short[]> InputQueue => _inputQueue;
        public ConcurrentQueue<string>  ResultQueue => _resultQueue;
```

Field names in `VoskRecognizerService` may differ (e.g. `_pcmQueue`, `_jsonResultQueue`) — use whatever the existing private field names are. Only add the two properties; do not rename fields.

If the fields are named differently (discovered by reading the file at Step 5 time), update the property body accordingly but keep the property **names** `InputQueue` and `ResultQueue` so they match the caller in Step 4.

- [ ] **Step 6: Unsubscribe in `OnDestroy`**

Locate the existing `OnDestroy()` method in `BattleVoiceBootstrap.cs`:

```csharp
        private void OnDestroy()
        {
            _recognizerService?.Dispose();
            _recognizerService = null;
            _voskModel?.Dispose();
            _voskModel = null;
        }
```

Replace with:

```csharp
        private void OnDestroy()
        {
            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked -= HandleSpellUnlocked;

            _recognizerService?.Dispose();
            _recognizerService = null;
            _voskModel?.Dispose();
            _voskModel = null;
        }
```

- [ ] **Step 7: Verify Unity compiles and all tests still pass**

> **Unity Editor task (user):** Wait for recompile. Console must be clean. Test Runner → Edit Mode → Run All — everything green.

- [ ] **Step 8: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-38): hot-swap Vosk recognizer on spell unlock`
- `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
- `Assets/Scripts/Voice/VoskRecognizerService.cs` *(only if Step 5 required changes)*

---

## Task 7: Create the `SpellCatalog` asset and wire it on the GameManager prefab

**Files (editor-only wiring — no code):**
- Create: `Assets/Data/Spells/SpellCatalog.asset`
- Modify: the `GameManager` prefab (location TBD — most likely `Assets/Prefabs/Core/GameManager.prefab` per existing project layout)

- [ ] **Step 1: Create the SpellCatalog asset**

> **Unity Editor task (user):** In the Project window, right-click `Assets/Data/Spells/` → **Create → Axiom → Data → Spell Catalog** → name it `SpellCatalog`. Select the new asset. Drag the three existing `SpellData` assets (`SD_Freeze`, `SD_Combust`, `SD_Neutralize`) into the `Spells` list.

- [ ] **Step 2: Assign the catalog on the GameManager prefab**

> **Unity Editor task (user):** Locate the `GameManager` prefab (search the Project window for `t:Prefab GameManager`). Open it in Prefab Mode. On the `GameManager` script component, assign `Assets/Data/Spells/SpellCatalog.asset` to the new `Spell Catalog` field. Save the prefab (Ctrl/Cmd + S).

- [ ] **Step 3: Smoke-test in Play Mode**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity`. Enter Play Mode. In the Console, confirm no `SpellCatalog is not assigned` warning appears. Transition into the Battle scene (walk into an enemy). Expected console log: `[BattleVoiceBootstrap] Vosk pipeline ready.` Try casting one of the three starter spells (push-to-talk → say the spell name). It should resolve correctly. Exit Play Mode.

- [ ] **Step 4: Story-trigger smoke test (optional manual test)**

> **Unity Editor task (user):** With Play Mode running in the Battle scene, use the Debug menu or a temporary test-only inspector button to call `GameManager.Instance.SpellUnlockService.Unlock(storyOnlySpell)` where `storyOnlySpell` is a `SpellData` asset with `unlockLevel = 0` that is already in the catalog. Confirm: (a) no errors, (b) console shows `Vosk recognizer rebuilt with N spells after unlock`, (c) the newly unlocked spell is speakable in that same battle.
>
> If you do not yet have a story-trigger debug hook, skip this step — it will be exercised when DEV-### (story-trigger system) lands.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-38): author SpellCatalog asset and wire to GameManager prefab`
- `Assets/Data/Spells/SpellCatalog.asset`
- `Assets/Data/Spells/SpellCatalog.asset.meta`
- The modified `GameManager` prefab file (path TBD by user) + its `.meta`

---

## Task 8: Acceptance verification against Jira DEV-38

Run through the acceptance criteria one by one and tick each off.

- [ ] **AC1 — `SpellUnlockService` (plain C#) manages the player's unlocked spell list and exposes an event fired on each new unlock.**
  Verified by `SpellUnlockServiceTests.Unlock_AddsSpell_AndFiresEvent` and the `OnSpellUnlocked` event in Task 3.

- [ ] **AC2 — Level-up spell grants: unlocks configured per SpellData (unlock condition = level threshold) are granted automatically when the player reaches that level.**
  Verified by `SpellUnlockServiceTests.NotifyPlayerLevel_GrantsEligibleSpells_AndFiresEventPerNewSpell` plus the `GameManager.ApplySaveData` / `StartNewGame` hooks in Task 4.

- [ ] **AC3 — Story-trigger spell grants: a story event can call `SpellUnlockService.Unlock(SpellData)` directly.**
  Verified by `SpellUnlockServiceTests.Unlock_AddsSpell_AndFiresEvent`. The `Unlock(SpellData)` public API accepts any catalog spell regardless of `unlockLevel`.

- [ ] **AC4 — On any unlock, `SpellVocabularyManager` is called to rebuild the Vosk JSON grammar so the new spell is immediately recognizable mid-session.**
  Verified by `BattleVoiceBootstrap.HandleSpellUnlocked` → `RebuildRecognizer` coroutine (Task 6). Mid-session smoke test in Task 7 Step 4.

- [ ] **AC5 — Newly unlocked spells are reflected in the player's spell list available in battle.**
  Verified by the `_spellCastController.Inject(resultQueue, spells)` call inside `RebuildRecognizer` (Task 6 Step 4). `SpellCastController._unlockedSpells` is replaced with the new array, so `SpellResultMatcher.Match` sees it on the next voice dequeue.

- [ ] **AC6 — Duplicate unlock calls for an already-unlocked spell are silently ignored (no double-add).**
  Verified by `SpellUnlockServiceTests.Unlock_DuplicateCall_IsSilentlyIgnored`.

No UVCS check-in for this task — it is review-only.

---

## Appendix A: Manual Smoke Test Plan

This section covers end-to-end manual verification in the Unity Editor after all Tasks 1–7 are implemented and checked in. Tests are grouped by unlock path (level-up vs. story-driven) and then by cross-cutting concerns (persistence, voice, edge cases).

### Prerequisites (test data setup)

Before running smoke tests, create the following temporary test spell assets under `Assets/Data/Spells/`. Add them to the `SpellCatalog` asset alongside the three existing starter spells.

| Asset name | `spellName` | `requiredLevel` | `prerequisiteSpell` | Purpose |
|------------|-------------|-----------------|---------------------|---------|
| `SD_Freeze` | `freeze` | `1` | *(none)* | Existing starter spell |
| `SD_Combust` | `combust` | `1` | *(none)* | Existing starter spell |
| `SD_Neutralize` | `neutralize` | `1` | *(none)* | Existing starter spell |
| `SD_CrystalSpike` | `crystal spike` | `3` | *(none)* | Level-gated mid-game spell |
| `SD_Shatter` | `shatter` | `3` | `SD_CrystalSpike` | Level-gated spell WITH prerequisite |
| `SD_AncientBurn` | `ancient burn` | `0` | *(none)* | Story-only spell (never auto-granted) |
| `SD_VoidPulse` | `void pulse` | `0` | `SD_AncientBurn` | Story-only spell WITH prerequisite |

After smoke testing is complete, these temporary assets can be kept (if they fit the game design) or deleted.

---

### Group 1: Level-Up Spell Unlocks

**ST-1.1 — Starter spells (level 1) granted on New Game**

1. Open `Platformer.unity`. Enter Play Mode.
2. Trigger `GameManager.Instance.StartNewGame()` (or use the New Game flow if a menu exists).
3. Walk into an enemy to enter the Battle scene.
4. **Verify:** Console shows `[BattleVoiceBootstrap] Vosk pipeline ready.`
5. Hold push-to-talk and say "freeze", "combust", and "neutralize" one at a time.
6. **Expected:** All three starter spells resolve and cast correctly.
7. **Expected:** Console does NOT show `Vosk recognizer rebuilt` — starters were loaded at pipeline init, no hot-swap needed.

**ST-1.2 — Mid-game spell granted on level-up (no prerequisite)**

1. While in Play Mode (Battle or Platformer scene), open the Debug Inspector on `GameManager`.
2. Call `GameManager.Instance.PlayerState.ApplyProgression(3, 0)` to set the player to level 3 (or use whatever XP-grant mechanism exists to reach level 3).
3. Then call `GameManager.Instance.SpellUnlockService.NotifyPlayerLevel(3)`.
4. **Expected:** Console shows `Vosk recognizer rebuilt with 4 spells after unlock` (3 starters + crystal spike).
5. Hold push-to-talk and say "crystal spike".
6. **Expected:** The spell resolves and casts correctly.

**ST-1.3 — Prerequisite-gated spell unlocks in the same level-up**

1. Continuing from ST-1.2 (player is level 3, crystal spike already unlocked).
2. **Expected:** "shatter" (requires crystal spike AND level 3) should ALSO have been granted in the same `NotifyPlayerLevel(3)` call.
3. Console should show the recognizer rebuilt with 5 spells (3 starters + crystal spike + shatter).
4. Hold push-to-talk and say "shatter".
5. **Expected:** The spell resolves and casts correctly.

**ST-1.4 — Prerequisite NOT met blocks the dependent spell**

1. Start a fresh Play Mode session. Call `StartNewGame()`.
2. Set player level to 3 WITHOUT crystal spike unlocked yet: skip step — `NotifyPlayerLevel(3)` should handle this automatically.
3. BUT: modify `SD_CrystalSpike` temporarily to `requiredLevel = 5` (higher than current level 3).
4. Call `NotifyPlayerLevel(3)`.
5. **Expected:** Neither crystal spike (level too low) NOR shatter (prerequisite not met) are granted. Only the 3 starter spells exist.
6. Restore `SD_CrystalSpike` back to `requiredLevel = 3` after this test.

**ST-1.5 — Repeated level-up does not re-fire events for already-unlocked spells**

1. Start a fresh session. `StartNewGame()` → enter battle.
2. Call `NotifyPlayerLevel(1)` — starters granted.
3. Subscribe a debug listener: in the Console, watch for any additional `Vosk recognizer rebuilt` logs.
4. Call `NotifyPlayerLevel(1)` again.
5. **Expected:** No `Vosk recognizer rebuilt` log. No duplicate spells in `UnlockedSpells`. Event did not fire again.

---

### Group 2: Story-Driven Spell Unlocks

**ST-2.1 — Story-only spell is NOT granted by level-up**

1. Start a fresh Play Mode session. `StartNewGame()`.
2. Set player to level 99: `PlayerState.ApplyProgression(99, 0)` then `NotifyPlayerLevel(99)`.
3. **Expected:** `SD_AncientBurn` (`requiredLevel = 0`) is NOT in `UnlockedSpells`. Only level-gated spells are granted.
4. Hold push-to-talk and say "ancient burn".
5. **Expected:** The voice system does NOT recognize the phrase (it is not in the grammar).

**ST-2.2 — Story-only spell granted via direct Unlock() call**

1. Continuing from ST-2.1 (player at level 99, ancient burn not yet unlocked).
2. Get a reference to the `SD_AncientBurn` asset. Call:
   `GameManager.Instance.SpellUnlockService.Unlock(ancientBurnSpellData)`
3. **Expected:** Console shows `Vosk recognizer rebuilt with N+1 spells after unlock`.
4. Hold push-to-talk and say "ancient burn".
5. **Expected:** The spell resolves and casts correctly — it is now in the grammar.

**ST-2.3 — Story-only spell with prerequisite**

1. Start a fresh Play Mode session. `StartNewGame()`.
2. Attempt to unlock `SD_VoidPulse` (requires `SD_AncientBurn` as prerequisite) directly:
   `GameManager.Instance.SpellUnlockService.Unlock(voidPulseSpellData)`
3. **Expected:** The unlock SUCCEEDS — `Unlock()` is a direct grant and does NOT check prerequisites (prerequisites are only enforced by `NotifyPlayerLevel`). This is by design: story triggers are authoritative.
4. Alternatively, if the design intent changes to enforce prerequisites on direct unlocks too, this test documents the current behavior for review.

**ST-2.4 — Duplicate story unlock is silently ignored**

1. Continuing from ST-2.2 (ancient burn already unlocked).
2. Call `Unlock(ancientBurnSpellData)` again.
3. **Expected:** Returns `false`. No `Vosk recognizer rebuilt` log. `UnlockedSpells.Count` unchanged.

---

### Group 3: Persistence (Save/Load)

**ST-3.1 — Unlocked spells persist across save/load**

1. Start a fresh session. `StartNewGame()` → enter battle.
2. Level up to 3 (`NotifyPlayerLevel(3)`). Also unlock `SD_AncientBurn` via story call.
3. Save the game (however the save flow is triggered — manual or auto).
4. Exit Play Mode. Re-enter Play Mode.
5. Load the save.
6. **Expected:** `SpellUnlockService.UnlockedSpells` contains all 5 spells (3 starters + crystal spike + shatter) plus ancient burn.
7. Enter battle. Hold push-to-talk and say "crystal spike" and "ancient burn".
8. **Expected:** Both resolve correctly — grammar was rebuilt from the loaded state.

**ST-3.2 — Save/load does NOT re-fire OnSpellUnlocked events**

1. Same setup as ST-3.1. After loading, check the Console.
2. **Expected:** No `Vosk recognizer rebuilt` log during `RestoreFromIds`. The grammar is built once during `BattleVoiceBootstrap.Start()` from the restored set, not from individual unlock events.

**ST-3.3 — Unknown spell IDs in save data are silently skipped**

1. Manually edit the save JSON file to include a fake spell ID (e.g., `"nonexistent_spell"`) in `unlockedSpellIds`.
2. Load the save.
3. **Expected:** No error or exception. The unknown ID is skipped. All valid spells load correctly.

---

### Group 4: Voice Recognition Integration

**ST-4.1 — Hot-swap during active battle**

1. Enter a battle. Confirm the voice pipeline is ready.
2. Mid-battle (before the battle ends), trigger a story unlock via Debug Inspector:
   `GameManager.Instance.SpellUnlockService.Unlock(ancientBurnSpellData)`
3. **Expected:** Console shows `Vosk recognizer rebuilt with N spells after unlock`.
4. Without leaving the battle, hold push-to-talk and say "ancient burn".
5. **Expected:** The newly unlocked spell is recognized and casts correctly in the same battle session.

**ST-4.2 — Grammar only contains unlocked spells**

1. Start a fresh session. `StartNewGame()` → enter battle (only starter spells unlocked).
2. Hold push-to-talk and say "crystal spike" (not yet unlocked).
3. **Expected:** The voice system does NOT recognize the phrase — it is not in the restricted grammar. The system may return a partial/no match or silence.

**ST-4.3 — Fallback when no GameManager present (Battle scene isolation)**

1. Open `Battle.unity` directly (no Platformer scene, no GameManager in the hierarchy).
2. Ensure the `BattleVoiceBootstrap` Inspector still has `_unlockedSpells` populated with the starter spells.
3. Enter Play Mode.
4. **Expected:** Voice pipeline initializes using the Inspector-assigned spell list. Console shows `Vosk pipeline ready.` No null reference exceptions.

---

### Group 5: Edge Cases

**ST-5.1 — SpellCatalog not assigned on GameManager**

1. Open the GameManager prefab. Clear the `Spell Catalog` field (set to None).
2. Enter Play Mode via `Platformer.unity`.
3. **Expected:** Console shows `[GameManager] SpellCatalog is not assigned` error. No crash. Game continues without spell unlock functionality.

**ST-5.2 — Empty SpellCatalog**

1. Create a temporary empty `SpellCatalog` asset (no spells in the list). Assign it to GameManager.
2. `StartNewGame()` → `NotifyPlayerLevel(10)`.
3. **Expected:** No errors. `UnlockedSpells` remains empty. Voice pipeline initializes with zero spells (or uses Inspector fallback).

**ST-5.3 — Rapid consecutive unlocks**

1. Enter a battle with the voice pipeline active.
2. In quick succession, unlock 3 story spells via Debug Inspector (call `Unlock()` three times rapidly).
3. **Expected:** Three `Vosk recognizer rebuilt` logs appear. No race condition, no crash. The final recognizer contains all newly unlocked spells. The last rebuild wins.

---

### Smoke Test Cleanup

After all smoke tests pass:

1. Remove any temporary `SpellData` assets created solely for testing (unless they fit the game design and should be kept).
2. Restore any temporarily modified `requiredLevel` values (e.g., ST-1.4).
3. Ensure the `SpellCatalog` asset contains only the intended production spells.
4. No UVCS check-in needed for smoke test artifacts — this section is documentation only.

---

## Self-Review Notes

- **DEV-37 alignment:** This plan builds on the `SpellUnlockCondition` class shipped in DEV-37 rather than adding a redundant `unlockLevel` field. The only DEV-37 file modified is `SpellUnlockCondition.cs` (`[Min(1)]` → `[Min(0)]`).
- **Prerequisite chain handling:** `NotifyPlayerLevel` loops until no new grants, so prerequisite chains resolve in a single call (e.g., spell A unlocks → spell B requiring A becomes eligible on the next pass). Bounded by catalog size.
- **Spec coverage:** all six AC items are mapped to a specific test or runtime code path in Task 8.
- **Placeholders:** none — every step has concrete code or a concrete Unity action.
- **Type consistency:**
  - `SpellCatalog.TryGetByName(string, out SpellData)` — used identically in service + tests.
  - `SpellCatalog.GetUnlocksAtOrBelowLevel(int)` returns `IEnumerable<SpellData>` — iterated in both test and service. Filters by `unlockCondition.requiredLevel`.
  - `SpellUnlockService.NotifyPlayerLevel` additionally calls `unlockCondition.IsUnlockedFor()` to respect prerequisites.
  - `SpellUnlockService` API (`Unlock`, `Contains`, `NotifyPlayerLevel`, `RestoreFromIds`, `OnSpellUnlocked`, `UnlockedSpells`, `UnlockedSpellNames`) — identical names in tests, service, GameManager wiring.
  - `VoskRecognizerService.InputQueue` / `ResultQueue` — Step 5 of Task 6 adds them explicitly; caller in Step 4 uses those exact names.
- **Guard clause ordering:**
  - `SpellUnlockService.Unlock` — null-guard first (throws), then dedupe check (silent no-op per AC6).
  - `SpellCatalog.TryGetByName` — null/empty check before array iteration.
  - `BattleVoiceBootstrap.HandleSpellUnlocked` — null spell → exit before touching model; null model → exit before touching service; null service → exit before queue access. This mirrors the existing startup guard pattern in the file.
- **UVCS staged file audit:** every created `.cs` file has its `.cs.meta` in the check-in step. `.asmdef` modification carries only the `.asmdef` (no new meta since it already existed). Task 7 asset creation carries the `.asset.meta`.
- **Unity Editor task isolation:** every user-in-the-Editor action is marked `> **Unity Editor task (user):**` and separated from code steps.
