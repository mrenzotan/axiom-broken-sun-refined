# DEV-37: ScriptableObject Data Assets (SpellData, EnemyData, ItemData, CharacterData)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 5 data foundation. Replace the single-reaction fields on `SpellData` with a `List<ReactionEntry>`, add a per-spell `element` and `unlockCondition`, extend `EnemyData` with XP reward and loot, and introduce two new ScriptableObject types — `ItemData` and `CharacterData`. Migrate the three existing spell assets and one enemy asset to the new schema, and sweep `Assets/Scripts/` for any hardcoded tunable values that should now live on an asset.

**Architecture:**
- All data types stay in `Axiom.Data` — no new asmdef.
- `ReactionEntry` is a plain `[Serializable]` class in `Assets/Scripts/Data/ReactionEntry.cs`, consumed by `SpellEffectResolver` (iterate reactions, first matching `reactsWith` on the effect target wins).
- `ItemData` / `CharacterData` are new `ScriptableObject` assets alongside `SpellData` / `EnemyData`.
- `LootEntry` (inline `[Serializable]` on `EnemyData`) holds an `ItemData` reference + drop chance. No separate LootTable asset in this story — keep scope minimal.
- `ChemistryConcept` is a lightweight enum matching the four pillars in `docs/LORE_AND_MECHANICS.md` → "The Four Core Chemistry Concepts": `None, PhaseChange, AcidBase, Combustion, Precipitation`. It is a classification tag on `SpellData` for UI grouping / spellbook sorting / tutorial callouts only. It does **not** drive combat math — chemistry interactions remain `ChemicalCondition`-driven per `docs/game-mechanics/chemistry-spell-combat-system.md`. The name `ChemistryConcept` (not `SpellElement`) is deliberate, to prevent future drift toward a Pokémon-style element/weakness chart.
- `unlockCondition` is modeled as `{ int requiredLevel; SpellData prerequisiteSpell; }` on `SpellData`. A spell is unlocked when the player meets `requiredLevel` and, if set, has unlocked `prerequisiteSpell`. Level 1 + null prerequisite = starting spell.

**Tech Stack:** Unity 6 LTS · C# · ScriptableObjects · `Axiom.Data` asmdef (existing) · NUnit Edit Mode via Unity Test Framework

**Spec references:**
- `docs/game-mechanics/chemistry-spell-combat-system.md` (authoritative chemistry fields)
- `docs/superpowers/plans/2026-04-05-dev27-spell-combat-mechanics.md` (prior SpellData/EnemyData schema)

---

## Open questions (confirm before starting Task 1)

These were not fully pinned down by the ticket or existing code. Default choices are baked into the plan; flag to the user if any are wrong.

1. **`ChemistryConcept` enum scope** — DEV-27's plan explicitly abandoned a Pokémon-style element concept in favor of chemistry. DEV-37's AC brings `element` back. This plan reframes it as `ChemistryConcept` with values mirroring the four pillars in `docs/LORE_AND_MECHANICS.md` (PhaseChange, AcidBase, Combustion, Precipitation). **UI/filter tag only**, never read by `SpellEffectResolver`. Confirm that's the intent; if not, revisit.
2. **`unlockCondition` shape** — chosen `{ requiredLevel, prerequisiteSpell }`. Alternative would be a free-form string or enum; this pair is the minimum that supports the Phase 5 progression system without over-designing.
3. **Loot table shape** — inline `List<LootEntry>` on `EnemyData`. A standalone `LootTableData` asset can be added later when multiple enemies need to share a table; YAGNI for now.
4. **Existing asset migration** — three spell assets (`SD_Combust`, `SD_Freeze`, `SD_Neutralize`) and one enemy asset (`ED_MeltspawnTest`) carry values in the old single-reaction fields. Plan uses `FormerlySerializedAs` on one intermediate commit so Unity rehydrates the old values into the new `ReactionEntry` list without losing data. User verifies values in the Inspector after import.

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Data/ChemistryConcept.cs` | Enum: None, PhaseChange, AcidBase, Combustion, Precipitation — mirrors the four pillars in LORE_AND_MECHANICS.md |
| Create | `Assets/Scripts/Data/SpellUnlockCondition.cs` | `[Serializable]` class: requiredLevel, prerequisiteSpell |
| Create | `Assets/Scripts/Data/ReactionEntry.cs` | `[Serializable]` class: reactsWith, reactionBonusDamage, transformsTo, transformationDuration |
| Modify | `Assets/Scripts/Data/SpellData.cs` | Add concept, unlockCondition, `List<ReactionEntry> reactions`; remove single reaction fields (migration via `FormerlySerializedAs`) |
| Create | `Assets/Scripts/Data/ItemType.cs` | Enum: Consumable, KeyItem, Equipment |
| Create | `Assets/Scripts/Data/ItemEffectType.cs` | Enum: None, RestoreHP, RestoreMP, Revive |
| Create | `Assets/Scripts/Data/ItemData.cs` | ScriptableObject: itemId, displayName, description, itemType, effectType, effectPower |
| Create | `Assets/Scripts/Data/LootEntry.cs` | `[Serializable]` class: item (`ItemData`), dropChance (0–1) |
| Modify | `Assets/Scripts/Data/EnemyData.cs` | Add xpReward, `List<LootEntry> loot` |
| Create | `Assets/Scripts/Data/CharacterData.cs` | ScriptableObject: displayName, maxHP, maxMP, atk, def, spd |
| Modify | `Assets/Scripts/Battle/SpellEffectResolver.cs` | Iterate `spell.reactions`; first match wins (replaces single `reactsWith` branch) |
| Create (tests) | `Assets/Tests/Editor/Data/SpellUnlockConditionTests.cs` | `IsUnlockedFor(level, unlockedSpellIds)` truth table |
| Create (tests) | `Assets/Tests/Editor/Data/ReactionEntryResolutionTests.cs` | Verifies resolver picks first matching reaction, no match = no reaction |
| Modify (tests) | `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs` | Update existing reaction tests to the new `reactions` list shape |

**Assets touched (user Editor work):**
- Migrate: `Assets/Data/Spells/SD_Combust.asset`, `SD_Freeze.asset`, `SD_Neutralize.asset`
- Migrate: `Assets/Data/Enemies/ED_MeltspawnTest.asset`
- Create folder: `Assets/Data/Items/` (+ one smoke-test asset)
- Create folder: `Assets/Data/Characters/` (+ `CD_Player.asset`)

No new `.asmdef` files. All code lands in `Axiom.Data` (autoReferenced) and `Axiom.Battle` (existing).

---

## Task 1: New pure-data types (ReactionEntry, ChemistryConcept, SpellUnlockCondition, ItemType, ItemEffectType, LootEntry)

Pure data with no game logic. No tests in this task — logic tests arrive in Task 3 when `SpellUnlockCondition` grows a method.

**Files:**
- Create: `Assets/Scripts/Data/ReactionEntry.cs`
- Create: `Assets/Scripts/Data/ChemistryConcept.cs`
- Create: `Assets/Scripts/Data/SpellUnlockCondition.cs`
- Create: `Assets/Scripts/Data/ItemType.cs`
- Create: `Assets/Scripts/Data/ItemEffectType.cs`
- Create: `Assets/Scripts/Data/LootEntry.cs`

- [ ] **Create `Assets/Scripts/Data/ReactionEntry.cs`**

```csharp
using System;
using UnityEngine;

namespace Axiom.Data
{
    [Serializable]
    public class ReactionEntry
    {
        [Tooltip("The condition (material or status) this spell reacts with if present on the effect target.")]
        public ChemicalCondition reactsWith;

        [Tooltip("Flat bonus added to the spell's primary effect when this reaction fires.")]
        public int reactionBonusDamage;

        [Tooltip("Material condition temporarily applied to the target when a phase-change reaction fires. None if no transformation.")]
        public ChemicalCondition transformsTo;

        [Tooltip("How many turns the transformed material condition lasts. Only meaningful when transformsTo != None.")]
        public int transformationDuration;
    }
}
```

- [ ] **Create `Assets/Scripts/Data/ChemistryConcept.cs`**

```csharp
namespace Axiom.Data
{
    /// <summary>
    /// The core chemistry concept a spell belongs to. Mirrors the four pillars defined in
    /// <c>docs/LORE_AND_MECHANICS.md</c> → "The Four Core Chemistry Concepts":
    /// States of Matter &amp; Phase Changes, Acid–Base (Neutralization), Combustion &amp;
    /// Exothermic Reactions, and Solubility &amp; Precipitation.
    ///
    /// UI-only classification tag for spellbook sorting, tooltips, and tutorial callouts.
    /// Combat math is driven by <see cref="ChemicalCondition"/>, never this enum — this is
    /// deliberately NOT a Pokémon-style element/weakness system.
    /// </summary>
    public enum ChemistryConcept
    {
        None,
        PhaseChange,    // States of Matter — Freeze, Melt, Evaporate, Condense
        AcidBase,       // Neutralization   — Neutralize, Corrode
        Combustion,     // Exothermic       — Combust, Ignite Vapor
        Precipitation   // Solubility       — Crystal Bridge, Mineral Bind
    }
}
```

- [ ] **Create `Assets/Scripts/Data/SpellUnlockCondition.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [Serializable]
    public class SpellUnlockCondition
    {
        [Tooltip("Minimum player level required to unlock this spell. 1 = available from game start.")]
        [Min(1)] public int requiredLevel = 1;

        [Tooltip("Optional spell that must be unlocked first. Null = no prerequisite.")]
        public SpellData prerequisiteSpell;

        /// <summary>
        /// True when the player's level and unlocked-spell set both satisfy this condition.
        /// A null prerequisite is treated as satisfied.
        /// </summary>
        public bool IsUnlockedFor(int playerLevel, IReadOnlyCollection<string> unlockedSpellNames)
        {
            if (playerLevel < requiredLevel) return false;
            if (prerequisiteSpell == null) return true;
            if (unlockedSpellNames == null) return false;
            return unlockedSpellNames.Contains(prerequisiteSpell.spellName);
        }
    }
}
```

- [ ] **Create `Assets/Scripts/Data/ItemType.cs`**

```csharp
namespace Axiom.Data
{
    public enum ItemType
    {
        Consumable,
        KeyItem,
        Equipment
    }
}
```

- [ ] **Create `Assets/Scripts/Data/ItemEffectType.cs`**

```csharp
namespace Axiom.Data
{
    public enum ItemEffectType
    {
        None,
        RestoreHP,
        RestoreMP,
        Revive
    }
}
```

- [ ] **Create `Assets/Scripts/Data/LootEntry.cs`**

```csharp
using System;
using UnityEngine;

namespace Axiom.Data
{
    [Serializable]
    public class LootEntry
    {
        [Tooltip("The item dropped if this entry rolls. Null entries are ignored at runtime.")]
        public ItemData item;

        [Tooltip("Drop chance 0.0–1.0. 1.0 = guaranteed.")]
        [Range(0f, 1f)] public float dropChance = 1f;
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-37): add reaction, chemistry concept, unlock, item, and loot data types`
  - `Assets/Scripts/Data/ReactionEntry.cs`
  - `Assets/Scripts/Data/ReactionEntry.cs.meta`
  - `Assets/Scripts/Data/ChemistryConcept.cs`
  - `Assets/Scripts/Data/ChemistryConcept.cs.meta`
  - `Assets/Scripts/Data/SpellUnlockCondition.cs`
  - `Assets/Scripts/Data/SpellUnlockCondition.cs.meta`
  - `Assets/Scripts/Data/ItemType.cs`
  - `Assets/Scripts/Data/ItemType.cs.meta`
  - `Assets/Scripts/Data/ItemEffectType.cs`
  - `Assets/Scripts/Data/ItemEffectType.cs.meta`
  - `Assets/Scripts/Data/LootEntry.cs`
  - `Assets/Scripts/Data/LootEntry.cs.meta`

---

## Task 2: ItemData and CharacterData ScriptableObjects

**Files:**
- Create: `Assets/Scripts/Data/ItemData.cs`
- Create: `Assets/Scripts/Data/CharacterData.cs`

- [ ] **Create `Assets/Scripts/Data/ItemData.cs`**

```csharp
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Axiom/Data/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Stable string ID used by saves and inventory. Keep unique and immutable once assigned.")]
        public string itemId;

        [Tooltip("Display name shown in UI.")]
        public string displayName;

        [Tooltip("Tooltip / flavor text shown when inspecting the item.")]
        [TextArea(2, 4)] public string description;

        [Tooltip("Category of item — drives which UIs and systems can use it.")]
        public ItemType itemType;

        [Tooltip("Gameplay effect when consumed. None for KeyItem / Equipment placeholders.")]
        public ItemEffectType effectType;

        [Tooltip("Magnitude of the effect (HP restored, MP restored, etc.). Ignored for effectType == None.")]
        public int effectPower;

        [Tooltip("Chemical conditions this item cures on use (e.g. a Salt Bomb curing Frozen, Burning, etc.). Empty for most consumables.")]
        public List<ChemicalCondition> curesConditions = new List<ChemicalCondition>();
    }
}
```

- [ ] **Create `Assets/Scripts/Data/CharacterData.cs`**

```csharp
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Base stats for a playable character. Consumed by <c>PlayerState</c> / character
    /// initialization instead of hardcoded constants. Fields use the "base" prefix to
    /// signal these are Level 1 starting values — a level-up system will scale from these.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Axiom/Data/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Tooltip("Character name shown in UI and battle results.")]
        public string characterName;

        [Tooltip("Base maximum HP at Level 1.")]
        [Min(1)] public int baseMaxHP = 100;

        [Tooltip("Base maximum MP at Level 1.")]
        [Min(0)] public int baseMaxMP = 30;

        [Tooltip("Base Attack at Level 1.")]
        [Min(0)] public int baseATK = 12;

        [Tooltip("Base Defense at Level 1.")]
        [Min(0)] public int baseDEF = 6;

        [Tooltip("Base Speed at Level 1.")]
        [Min(0)] public int baseSPD = 8;

        [Tooltip("Portrait sprite shown in the character status screen (Phase 6+). Leave null for now.")]
        public Sprite portraitSprite;
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-37): add ItemData and CharacterData ScriptableObjects`
  - `Assets/Scripts/Data/ItemData.cs`
  - `Assets/Scripts/Data/ItemData.cs.meta`
  - `Assets/Scripts/Data/CharacterData.cs`
  - `Assets/Scripts/Data/CharacterData.cs.meta`

---

## Task 3: Extend SpellData (concept, unlockCondition, reactions list) with FormerlySerializedAs migration

The three existing spell assets already have values in the old single-reaction fields. We use `FormerlySerializedAs` on an intermediate list-with-single-entry shape so Unity rehydrates the old values on first import, then the user verifies in the Inspector.

**Files:**
- Modify: `Assets/Scripts/Data/SpellData.cs`
- Create: `Assets/Tests/Editor/Data/SpellUnlockConditionTests.cs`

- [ ] **Write test first: `Assets/Tests/Editor/Data/SpellUnlockConditionTests.cs`**

```csharp
using System.Collections.Generic;
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Tests.Data
{
    public class SpellUnlockConditionTests
    {
        [Test]
        public void IsUnlockedFor_NoPrerequisite_ReturnsTrueWhenLevelMet()
        {
            var condition = new SpellUnlockCondition { requiredLevel = 3, prerequisiteSpell = null };
            Assert.IsTrue(condition.IsUnlockedFor(3, new HashSet<string>()));
            Assert.IsTrue(condition.IsUnlockedFor(10, null));
        }

        [Test]
        public void IsUnlockedFor_NoPrerequisite_ReturnsFalseWhenLevelBelow()
        {
            var condition = new SpellUnlockCondition { requiredLevel = 3, prerequisiteSpell = null };
            Assert.IsFalse(condition.IsUnlockedFor(2, new HashSet<string>()));
        }

        [Test]
        public void IsUnlockedFor_WithPrerequisite_RequiresBothLevelAndSpell()
        {
            SpellData prereq = ScriptableObject.CreateInstance<SpellData>();
            prereq.spellName = "combust";

            var condition = new SpellUnlockCondition { requiredLevel = 2, prerequisiteSpell = prereq };

            Assert.IsFalse(condition.IsUnlockedFor(2, new HashSet<string>()),
                "Level met but prerequisite spell missing — should be locked.");
            Assert.IsFalse(condition.IsUnlockedFor(1, new HashSet<string> { "combust" }),
                "Prerequisite met but level too low — should be locked.");
            Assert.IsTrue(condition.IsUnlockedFor(2, new HashSet<string> { "combust" }),
                "Both conditions satisfied — should be unlocked.");

            ScriptableObject.DestroyImmediate(prereq);
        }

        [Test]
        public void IsUnlockedFor_NullUnlockedSet_WithPrerequisite_ReturnsFalse()
        {
            SpellData prereq = ScriptableObject.CreateInstance<SpellData>();
            prereq.spellName = "freeze";

            var condition = new SpellUnlockCondition { requiredLevel = 1, prerequisiteSpell = prereq };

            Assert.IsFalse(condition.IsUnlockedFor(5, null));

            ScriptableObject.DestroyImmediate(prereq);
        }
    }
}
```

Expected: all four tests fail until `SpellUnlockCondition` from Task 1 is accessible — they should pass once Task 1 code compiles.

- [ ] **Modify `Assets/Scripts/Data/SpellData.cs`** — add new fields, wire `FormerlySerializedAs` on a new single-entry reactions list migration helper, remove the four single-reaction fields.

Final shape:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewSpellData", menuName = "Axiom/Data/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Tooltip("The spoken trigger word or phrase the player says to cast this spell. MUST be lowercase — Vosk only recognizes lowercase input.")]
        public string spellName;

        [Tooltip("The core chemistry concept this spell belongs to — used for spellbook grouping, UI sorting, and tutorial callouts. Does not drive combat math; chemistry interactions are driven by ChemicalCondition fields below.")]
        public ChemistryConcept concept = ChemistryConcept.None;

        [Tooltip("The type of effect this spell applies: Damage (targets enemy), Heal (targets caster), or Shield (targets caster).")]
        public SpellEffectType effectType;

        [Tooltip("Base magnitude: damage dealt, HP restored, or shield HP added.")]
        public int power;

        [Tooltip("MP cost to cast this spell.")]
        public int mpCost;

        [Tooltip("Requirements the player must meet before this spell appears in their grammar. Null/default = available from game start.")]
        public SpellUnlockCondition unlockCondition = new SpellUnlockCondition();

        [Header("Chemistry Condition System")]

        [Tooltip("Status condition applied to the spell's primary target after it resolves. None if no condition is inflicted.")]
        public ChemicalCondition inflictsCondition;

        [Tooltip("How many turns the inflicted condition lasts. 0 = default duration for that condition.")]
        public int inflictsConditionDuration;

        [Tooltip("Reactions this spell can trigger. Evaluated in order — the first entry whose reactsWith matches the effect target fires; later entries are ignored.")]
        public List<ReactionEntry> reactions = new List<ReactionEntry>();

        [Header("Spell Effects")]

        [Tooltip("Sprite animation clip played at the VFX spawn point when this spell is cast. Leave empty for no visual effect.")]
        public AnimationClip castVfxClip;

        [Tooltip("Sound effects played when this spell is cast. Assign 1-5 clips — one is chosen at random each cast. Leave empty for no audio effect.")]
        public AudioClip[] castSfxVariants;

        // ── Migration shims — old single-reaction fields surfaced as hidden serialized
        //    properties so existing assets rehydrate into `reactions` on first import.
        //    After Task 5 verifies all assets imported cleanly, these are deleted in
        //    a follow-up task within this story.
        [HideInInspector, FormerlySerializedAs("reactsWith")] public ChemicalCondition _legacyReactsWith;
        [HideInInspector, FormerlySerializedAs("reactionBonusDamage")] public int _legacyReactionBonusDamage;
        [HideInInspector, FormerlySerializedAs("transformsTo")] public ChemicalCondition _legacyTransformsTo;
        [HideInInspector, FormerlySerializedAs("transformationDuration")] public int _legacyTransformationDuration;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (spellName != null)
                spellName = spellName.ToLower();

            // One-shot migration: if legacy fields are set and reactions is empty, hoist them.
            if (reactions.Count == 0 && _legacyReactsWith != ChemicalCondition.None)
            {
                reactions.Add(new ReactionEntry
                {
                    reactsWith             = _legacyReactsWith,
                    reactionBonusDamage    = _legacyReactionBonusDamage,
                    transformsTo           = _legacyTransformsTo,
                    transformationDuration = _legacyTransformationDuration
                });
                _legacyReactsWith             = ChemicalCondition.None;
                _legacyReactionBonusDamage    = 0;
                _legacyTransformsTo           = ChemicalCondition.None;
                _legacyTransformationDuration = 0;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
```

- [ ] **Run Edit Mode tests** → Unity → Window → General → Test Runner → EditMode → Run All. Confirm `SpellUnlockConditionTests` all pass.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-37): extend SpellData with chemistry concept, unlock condition, and reactions list`
  - `Assets/Scripts/Data/SpellData.cs`
  - `Assets/Tests/Editor/Data/SpellUnlockConditionTests.cs`
  - `Assets/Tests/Editor/Data/SpellUnlockConditionTests.cs.meta`

---

## Task 4: Extend EnemyData (xpReward, loot list)

**Files:**
- Modify: `Assets/Scripts/Data/EnemyData.cs`

- [ ] **Modify `Assets/Scripts/Data/EnemyData.cs`**

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

        [Tooltip("Possible item drops. Each entry rolls independently against its dropChance.")]
        public List<LootEntry> loot = new List<LootEntry>();
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-37): add XP reward and loot list to EnemyData`
  - `Assets/Scripts/Data/EnemyData.cs`

---

## Task 5: Update SpellEffectResolver to iterate the reactions list

Resolver currently reads `spell.reactsWith` / `spell.reactionBonusDamage` / `spell.transformsTo` / `spell.transformationDuration`. After Task 3 those fields no longer exist on `SpellData`; the compile will break until this task lands.

**Files:**
- Modify: `Assets/Scripts/Battle/SpellEffectResolver.cs`
- Modify: `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`
- Create: `Assets/Tests/Editor/Data/ReactionEntryResolutionTests.cs`

- [ ] **Write test first: `Assets/Tests/Editor/Data/ReactionEntryResolutionTests.cs`**

Cover these cases (adapt boilerplate from the existing resolver tests):

| Scenario | Expectation |
|---|---|
| `reactions` list empty + target has condition | No reaction, no bonus damage |
| Single reaction, target has `reactsWith` | Reaction fires, bonus applied, condition consumed |
| Single reaction, target lacks `reactsWith` | No reaction |
| Two reactions, target has only the second | Second reaction fires |
| Two reactions, target has both | **First listed wins** (subsequent reactions skipped) |
| Reaction with `transformsTo` set | Material transformation applied with `transformationDuration` |

Each test should construct `SpellData` via `ScriptableObject.CreateInstance<SpellData>()`, populate `reactions`, call `resolver.Resolve(...)`, assert on the returned `SpellResult` and on `CharacterStats` condition state.

- [ ] **Modify `Assets/Scripts/Battle/SpellEffectResolver.cs`** — replace the single-branch reaction block:

```csharp
            // ── 2. Reaction check (first-match-wins iteration) ───────────────
            foreach (ReactionEntry reaction in spell.reactions)
            {
                if (reaction.reactsWith == ChemicalCondition.None) continue;
                if (!effectTarget.HasCondition(reaction.reactsWith)) continue;

                reactionTriggered = true;
                bonusDamage       = reaction.reactionBonusDamage;

                effectTarget.ConsumeCondition(reaction.reactsWith);

                if (reaction.transformsTo != ChemicalCondition.None)
                {
                    effectTarget.ApplyMaterialTransformation(
                        reaction.transformsTo,
                        reaction.reactsWith,
                        reaction.transformationDuration);
                    materialTransformed = true;
                }
                break; // first-match-wins
            }
```

Leave the rest of `Resolve()` unchanged.

- [ ] **Update `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`** — any test that sets `spell.reactsWith = X; spell.reactionBonusDamage = Y;` directly must be rewritten to add a `ReactionEntry` to `spell.reactions`. Keep assertions identical.

- [ ] **Run Edit Mode tests** → Test Runner → EditMode → Run All. All existing resolver tests plus the new reaction-list tests must pass. Do **not** proceed until green.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-37): iterate SpellData.reactions list in SpellEffectResolver`
  - `Assets/Scripts/Battle/SpellEffectResolver.cs`
  - `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`
  - `Assets/Tests/Editor/Data/ReactionEntryResolutionTests.cs`
  - `Assets/Tests/Editor/Data/ReactionEntryResolutionTests.cs.meta`

---

## Task 6: Migrate existing spell and enemy assets

> **Unity Editor task (user):** Verify each migrated asset after Unity's asset import reruns. Because Task 3 shipped `FormerlySerializedAs` shims, old values should automatically rehydrate into `reactions[0]` on the next import.

- [ ] **Unity Editor task (user):** Open Unity. Let the project reimport. Watch the console — no compile errors should remain (Task 5 resolved the last one).

- [ ] **Unity Editor task (user):** For each of `SD_Combust.asset`, `SD_Freeze.asset`, `SD_Neutralize.asset` at `Assets/Data/Spells/`:
  1. Select in Project panel; open Inspector.
  2. Confirm the old reaction values are now shown under the new `Reactions` list as a single entry.
  3. If the list is empty but the old legacy fields still hold values (visible only via the Debug-mode Inspector), toggle the Inspector to Debug mode, right-click the asset → Reimport. The `OnValidate` hoist should run. Switch back to Normal mode and verify.
  4. Set `concept` to the matching chemistry pillar: `Combustion` for Combust, `PhaseChange` for Freeze, `AcidBase` for Neutralize.
  5. Leave `unlockCondition.requiredLevel = 1` and `prerequisiteSpell = null` for all three starter spells.
  6. Ctrl+S / Cmd+S to write the asset.

- [ ] **Unity Editor task (user):** Open `Assets/Data/Enemies/ED_MeltspawnTest.asset`. Confirm existing stats are intact. Set `xpReward` to 10 as a placeholder. Leave `loot` empty for now. Save.

- [ ] **Run Play Mode smoke test:** Open `Assets/Scenes/Battle.unity`, enter Play Mode, cast each spell against the test enemy. Confirm damage / reaction / conditions still fire as they did pre-migration. Exit Play Mode.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-37): migrate existing spell/enemy assets to new reactions + tag schema`
  - `Assets/Data/Spells/SD_Combust.asset`
  - `Assets/Data/Spells/SD_Freeze.asset`
  - `Assets/Data/Spells/SD_Neutralize.asset`
  - `Assets/Data/Enemies/ED_MeltspawnTest.asset`

---

## Task 7: Create Items and Characters folders with seed assets

> **Unity Editor task (user):** All folder and asset creation happens in the Unity Editor via Assets → Create → Axiom → Data → … menu.

- [ ] **Unity Editor task (user):** In the Project panel under `Assets/Data/`, create a new folder named `Items`. Unity writes `Assets/Data/Items.meta` alongside it.

- [ ] **Unity Editor task (user):** In the Project panel under `Assets/Data/`, create a new folder named `Characters`. Unity writes `Assets/Data/Characters.meta` alongside it.

- [ ] **Unity Editor task (user):** In `Assets/Data/Items/`, Create → Axiom → Data → Item Data. Rename to `ID_Potion`. Set `itemId = "potion"`, `displayName = "Potion"`, `description = "Restores 20 HP."`, `itemType = Consumable`, `effectType = RestoreHP`, `effectPower = 20`. Save.

- [ ] **Unity Editor task (user):** In `Assets/Data/Characters/`, Create → Axiom → Data → Character Data. Rename to `CD_Player_Kaelen`. Set `characterName = "Kaelen"`, `baseMaxHP = 100`, `baseMaxMP = 30`, `baseATK = 12`, `baseDEF = 6`, `baseSPD = 8`. These match the hardcoded values currently in `BattleController` — Task 8 will replace those hardcodes with this asset. Save.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-37): seed Items/ and Characters/ folders with starter assets`
  - `Assets/Data/Items.meta`
  - `Assets/Data/Items/ID_Potion.asset`
  - `Assets/Data/Items/ID_Potion.asset.meta`
  - `Assets/Data/Characters.meta`
  - `Assets/Data/Characters/CD_Player_Kaelen.asset`
  - `Assets/Data/Characters/CD_Player_Kaelen.asset.meta`

---

## Task 8: Hardcoded-value sweep — Assets/Scripts audit

Acceptance criterion: "No hardcoded spell names, stats, or values remain anywhere in `Assets/Scripts/`."

- [ ] **Run a codebase sweep** with Grep across `Assets/Scripts/`:
  - Search for `maxHp|maxHP|MaxHP|MaxHp` numeric literal assignments
  - Search for `= 50|= 20|= 8|= 4|= 5` (typical stat magic numbers)
  - Search for string literals matching known spell names (`"combust"`, `"freeze"`, `"neutralize"`)
  - Check `BattleController.cs`, `GameManager.cs`, `PlayerState.cs` default constructions
  - Check Inspector-serialized values in MonoBehaviour fields that are *tunable* (player max HP, starting MP, etc.) — these should resolve from `CharacterData`, not Inspector numbers

- [ ] **Record findings** in a short scratch note inside this task's check-in commit description. For each hit, decide:
  - **Keep** — it's an engine/system constant (DoT magnitudes, cooldown frames). These are fine as code constants; they were not listed in the AC.
  - **Replace** — tunable player/enemy/spell values. File a follow-up sub-task below.

- [ ] **For each "Replace" hit**, update the consuming class to accept a `CharacterData` / `SpellData` / `ItemData` reference (SerializeField on the MonoBehaviour or constructor arg on the plain C# class) and read the value from there.
  - **Confirmed hit — `BattleController`:** currently constructs stats inline. Target pattern:
    ```csharp
    // BEFORE (hardcoded)
    var playerStats = new CharacterStats { Name = "Kaelen", MaxHP = 100, MaxMP = 30, ATK = 12, DEF = 6, SPD = 8 };
    var enemyStats  = new CharacterStats { Name = "Void Wraith", MaxHP = 60, MaxMP = 0, ATK = 8, DEF = 4, SPD = 5 };

    // AFTER (asset-driven)
    // [SerializeField] CharacterData playerData;
    // [SerializeField] EnemyData enemyData;
    var playerStats = new CharacterStats { Name = playerData.characterName };
    playerStats.Initialize(playerData.baseMaxHP, playerData.baseMaxMP, playerData.baseATK, playerData.baseDEF, playerData.baseSPD);
    var enemyStats = new CharacterStats { Name = enemyData.enemyName };
    enemyStats.Initialize(enemyData.maxHP, enemyData.maxMP, enemyData.atk, enemyData.def, enemyData.spd, enemyData.innateConditions);
    ```
  - **Likely hit:** `GameManager` initial player construction — same treatment.
  - **Not in scope:** `SpellVocabularyManager` — it already loads spell names dynamically from `Resources.LoadAll<SpellData>`. Full unlock integration deferred to Phase 7; do not refactor it here.

- [ ] **Add tests where feasible** for any plain C# class touched (constructor now takes a `CharacterData` — add an Edit Mode test asserting values propagate correctly and that null `CharacterData` throws `ArgumentNullException`).

- [ ] **Run Edit Mode tests** → Test Runner → EditMode → Run All. All green.

- [ ] **Run Play Mode smoke test:** Platformer → Battle transition. Confirm the player enters battle with the stats defined on `CD_Player_Kaelen`.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-37): replace hardcoded player stats with CharacterData asset`
  - (List each `.cs` and matching `.meta` touched)
  - (If Inspector values changed on a scene or prefab, list that `.unity` / `.prefab` too)

---

## Task 9: Remove FormerlySerializedAs shims from SpellData

Only runs *after* Task 6 confirmed all existing spell assets migrated cleanly. The shims must not remain long-term — they add dead fields and confuse future readers.

- [ ] **Confirm all three spell assets** have non-empty `reactions` lists in the Inspector and zero values in the hidden legacy fields (inspect in Debug mode if needed).

- [ ] **Modify `Assets/Scripts/Data/SpellData.cs`** — delete the four `_legacy*` fields and the migration branch inside `OnValidate`. Keep the lowercase-spellName branch.

- [ ] **Run Edit Mode tests + Play Mode smoke test** again. All green.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-37): remove SpellData legacy reaction migration shims`
  - `Assets/Scripts/Data/SpellData.cs`

---

## Definition of Done

- [ ] `SpellData` has `concept`, `unlockCondition`, and `List<ReactionEntry> reactions`; old single-reaction fields fully removed.
- [ ] `EnemyData` has `xpReward` and `List<LootEntry> loot`.
- [ ] `ItemData` and `CharacterData` ScriptableObjects exist with `CreateAssetMenu` entries under Axiom/Data/.
- [ ] `Assets/Data/Items/` and `Assets/Data/Characters/` folders exist with at least one seed asset each.
- [ ] All existing `Assets/Data/Spells/*.asset` and `Assets/Data/Enemies/*.asset` files migrated; no legacy fields remain.
- [ ] No hardcoded player/enemy/spell stats remain in `Assets/Scripts/` (only engine constants).
- [ ] All Edit Mode tests pass; Battle-scene Play Mode smoke test passes.
- [ ] Every file in the plan committed to UVCS with a `feat(DEV-37):` / `refactor(DEV-37):` / `chore(DEV-37):` message.
