# DEV-48: Chemical Spell Vocabulary — Spell Data Assets

> **Phase 5 Data Layer & Progression**  
> **Blocking on:** DEV-37 (SpellData with `List<ReactionEntry>` must be complete before authoring)  
> **Goal:** Author 12+ spells as `SpellData` ScriptableObject assets, grounded in game chemistry lore and distributed across player progression

---

## Overview

Each spell in Axiom derives its mechanics and lore from one of the **Four Core Chemistry Concepts** (Phase Change, Acid–Base, Combustion, Precipitation). Spells are pronounceable, phonetically distinct for Vosk recognition, and unlock through level progression with optional prerequisite chains.

**Spell Design Constraints:**

1. **Grounded in real chemistry** — spell effects and names drawn from actual reactions, states of matter, and chemistry vocabulary (e.g., "Freeze," "Neutralize," "Condense")
2. **Phonetically distinct** — spell names differ clearly when spoken aloud to minimize Vosk mis-recognition (not "Fire" + "Ire", but "Combust" + "Ignite")
3. **No hardcoded names in scripts** — all spell definitions live in `.asset` files only
4. **Distributed progression** — unlock gates spread spells across levels 1–20+, not clumped at level 1
5. **Balanced roles** — mix offensive (Damage), healing (Heal), protective (Shield), and status-inflicting spells

---

## Four Chemistry Concepts & Spell Domains

| Concept | In-game Theme | Sample Spells | Learnable Use | Vosk Grammar |
|---------|---|---|---|---|
| **Phase Change** | Shifting matter states (Solid ⇄ Liquid ⇄ Vapor) | Freeze, Melt, Evaporate, Condense | Control enemy movement; phase-lock weak states | "Freeze", "Melt", "Evaporate", "Condense" |
| **Acid–Base** | Neutralization & pH reactions | Neutralize, Corrode, Surge Base | Cleanse poisons; inflict acid damage | "Neutralize", "Corrode", "Surge Base" |
| **Combustion** | Exothermic energy release; heat | Combust, Ignite Vapor, Flash Fuel | Damage output; trigger chain reactions | "Combust", "Ignite Vapor", "Flash Fuel" |
| **Precipitation** | Solubility & crystal formation | Crystallize, Mineral Bind, Crystal Bridge | Crowd control (ATK reduction); solid shields | "Crystallize", "Mineral Bind", "Crystal Bridge" |

---

## Master Spell List (12 Canonical Spells)

### Tier 1: Beginner (Levels 1–5)

#### 1. **Freeze** (Phase Change)
- **Vosk Name (Pronunciation):** `freeze` [FREEZ]
- **Chemistry Concept:** `PhaseChange`
- **Effect Type:** `Damage`
- **Base Power:** 8
- **MP Cost:** 4
- **Unlock Condition:** `requiredLevel: 1, prerequisiteSpell: null` (starting spell)
- **Reactions:**
  - `reactsWith: Liquid` → `transformsTo: Solid`, `transformationDuration: 2 turns`, `reactionBonusDamage: +3`
- **Inflicts Condition:** `Frozen` (1 turn, skip next action)
- **Lore:** Cooling liquid water into ice. An apothecary's first lesson — slower the particles, lock the motion.
- **Tutorial Context:** Taught in "The Shattered Apothecary" → used to cross flooded hallway; transitions Liquid enemies into Solid (vulnerable to physical attacks).

---

#### 2. **Combust** (Combustion)
- **Vosk Name (Pronunciation):** `combust` [kuhm-BUST]
- **Chemistry Concept:** `Combustion`
- **Effect Type:** `Damage`
- **Base Power:** 10
- **MP Cost:** 5
- **Unlock Condition:** `requiredLevel: 2, prerequisiteSpell: null`
- **Reactions:**
  - `reactsWith: Flammable` → `transformsTo: None`, `reactionBonusDamage: +5` (no material change; bonus burst)
  - `reactsWith: Pressurized` → `transformsTo: None`, `reactionBonusDamage: +8` (explosive synergy)
- **Inflicts Condition:** `Burning` (2 turns, 5 DoT/turn)
- **Lore:** Fuel + oxygen → heat + products. The orange flame that consumed the apothecary when the Cascade struck.
- **Combat Use:** Primary offensive option for early game; devastating vs. Flammable/Pressurized enemies.

---

#### 3. **Neutralize** (Acid–Base)
- **Vosk Name (Pronunciation):** `neutralize` [NOO-truh-lyz]
- **Chemistry Concept:** `AcidBase`
- **Effect Type:** `Heal` (caster only)
- **Base Power:** 12
- **MP Cost:** 6
- **Unlock Condition:** `requiredLevel: 3, prerequisiteSpell: Freeze` (requires learning Freeze first)
- **Reactions:**
  - `reactsWith: Corroded` → on **caster** → `transformsTo: None`, `reactionBonusDamage: +0` (pure cleanse, no damage boost)
- **Inflicts Condition:** `None`
- **Special:** Consumes `Corroded` condition on caster if present before healing (e.g., caster hit by Corrode, then Neutralize removes it).
- **Lore:** Acid + base → salt + water. The apothecary mixed two volatile solutions; they went quiet. Safety in balance.
- **Combat Use:** Status cleanse + heal; essential for surviving Acid-inflicting enemies.

---

#### 4. **Crystallize** (Precipitation)
- **Vosk Name (Pronunciation):** `crystallize` [KRIS-tuh-lyz]
- **Chemistry Concept:** `Precipitation`
- **Effect Type:** `Damage`
- **Base Power:** 7
- **MP Cost:** 4
- **Unlock Condition:** `requiredLevel: 4, prerequisiteSpell: null`
- **Reactions:**
  - `reactsWith: MineralSaturated` → `transformsTo: None`, `reactionBonusDamage: +4` (supersaturation → solid matrix forms)
- **Inflicts Condition:** `Crystallized` (2 turns, target ATK halved)
- **Lore:** Dissolved minerals precipitate into solid matrix. Weaken the enemy's ability to strike back.
- **Combat Use:** Crowd control via ATK reduction; pairs well with defensive playstyle.

---

### Tier 2: Intermediate (Levels 6–12)

#### 5. **Melt** (Phase Change)
- **Vosk Name (Pronunciation):** `melt` [MELT]
- **Chemistry Concept:** `PhaseChange`
- **Effect Type:** `Damage`
- **Base Power:** 9
- **MP Cost:** 5
- **Unlock Condition:** `requiredLevel: 6, prerequisiteSpell: Freeze` (progression: learn to freeze, then heat)
- **Reactions:**
  - `reactsWith: Solid` → `transformsTo: Liquid`, `transformationDuration: 2 turns`, `reactionBonusDamage: +4` (hardens into soft form)
- **Inflicts Condition:** `None`
- **Lore:** Inverse of Freeze — input heat energy, break solid bonds. Meltspawns learned to fear this.
- **Combat Use:** Counter-spell for Solid enemies; enables physical damage while they're liquid.

---

#### 6. **Evaporate** (Phase Change)
- **Vosk Name (Pronunciation):** `evaporate` [ih-VAP-uh-rayt]
- **Chemistry Concept:** `PhaseChange`
- **Effect Type:** `Damage`
- **Base Power:** 11
- **MP Cost:** 6
- **Unlock Condition:** `requiredLevel: 7, prerequisiteSpell: Freeze` (progression chain)
- **Reactions:**
  - `reactsWith: Liquid` → `transformsTo: Vapor`, `transformationDuration: 2 turns`, `reactionBonusDamage: +5` (extreme heat vaporizes)
- **Inflicts Condition:** `Evaporating` (2 turns, 3 DoT/turn as lingering vapor)
- **Lore:** Boil water into steam. High-temperature phase shift; dangerous if overdone on allies.
- **Combat Use:** mid-game damage option; enemies in Vapor form become physically immune but can be Condensed.

---

#### 7. **Corrode** (Acid–Base)
- **Vosk Name (Pronunciation):** `corrode` [kuh-ROHD]
- **Chemistry Concept:** `AcidBase`
- **Effect Type:** `Damage`
- **Base Power:** 9
- **MP Cost:** 6
- **Unlock Condition:** `requiredLevel: 8, prerequisiteSpell: Neutralize` (understand acid-base first)
- **Reactions:**
  - `reactsWith: AlkalineBase` → `transformsTo: None`, `reactionBonusDamage: +6` (powerful exothermic neutralization)
- **Inflicts Condition:** `Corroded` (3 turns, escalating DoT: 4×1.0 / 4×1.5 / 4×2.0 per turn)
- **Lore:** Acid dissolves anything it touches. Slow, grinding erosion. Paired with Neutralize as a counter.
- **Combat Use:** High-DPS status effect; stack on Alkaline enemies for massive scaling damage.

---

#### 8. **Surge Base** (Acid–Base)
- **Vosk Name (Pronunciation):** `surge base` [SURJ BAYS]
- **Chemistry Concept:** `AcidBase`
- **Effect Type:** `Damage`
- **Base Power:** 10
- **MP Cost:** 5
- **Unlock Condition:** `requiredLevel: 9, prerequisiteSpell: Corrode` (understand acid dynamics)
- **Reactions:**
  - `reactsWith: AcidicFluid` → `transformsTo: None`, `reactionBonusDamage: +7` (base neutralizes acid + releases heat)
- **Inflicts Condition:** `None`
- **Lore:** Strong bases surge through acidic pools, neutralizing them with vigor. Offensive shield-break.
- **Combat Use:** Direct damage option vs. Acidic enemies; no lingering condition but immediate power.

---

#### 9. **Ignite Vapor** (Combustion)
- **Vosk Name (Pronunciation):** `ignite vapor` [ig-NYT VAY-per]
- **Chemistry Concept:** `Combustion`
- **Effect Type:** `Damage`
- **Base Power:** 12
- **MP Cost:** 7
- **Unlock Condition:** `requiredLevel: 10, prerequisiteSpell: Combust` (understand combustion; Evaporate optional but synergistic)
- **Reactions:**
  - `reactsWith: Vapor` → `transformsTo: None`, `reactionBonusDamage: +6` (vapor ignition = explosive burst)
  - `reactsWith: Flammable` → `transformsTo: None`, `reactionBonusDamage: +4` (secondary ignition)
- **Inflicts Condition:** `Burning` (2 turns, 5 DoT/turn)
- **Lore:** Combustible gas + ignition source = chain reaction. The apothecary's vapor hood exploded upward in the Cascade.
- **Combat Use:** High-damage nuke for Vapor phase-locked enemies; synergizes with Evaporate.

---

#### 10. **Condense** (Phase Change)
- **Vosk Name (Pronunciation):** `condense` [kun-DENS]
- **Chemistry Concept:** `PhaseChange`
- **Effect Type:** `Damage`
- **Base Power:** 10
- **MP Cost:** 6
- **Unlock Condition:** `requiredLevel: 11, prerequisiteSpell: Evaporate` (opposite direction: vapor → liquid)
- **Reactions:**
  - `reactsWith: Vapor` → `transformsTo: Liquid`, `transformationDuration: 2 turns`, `reactionBonusDamage: +5` (cooling collapses gas back)
- **Inflicts Condition:** `None`
- **Lore:** Cool steam back into liquid water. Complete the cycle of phase states.
- **Combat Use:** Counter-spell to Evaporate chains; transition Vapor enemies into Liquid (physically immune but breakable by Freeze/cleavage).

---

### Tier 3: Advanced (Levels 13+)

#### 11. **Mineral Bind** (Precipitation)
- **Vosk Name (Pronunciation):** `mineral bind` [MIN-er-ul BYND]
- **Chemistry Concept:** `Precipitation`
- **Effect Type:** `Shield` (caster only)
- **Base Power:** 15
- **MP Cost:** 8
- **Unlock Condition:** `requiredLevel: 13, prerequisiteSpell: Crystallize` (progression: learn to crystallize enemies, then self-protect)
- **Reactions:**
  - On **caster**; `reactsWith: MineralSaturated` → `transformsTo: None`, `reactionBonusDamage: +8` (bonus shield from extra dissolved minerals)
- **Inflicts Condition:** `None`
- **Lore:** Coat the body in a layer of solid mineral matrix. Defense through mineralization.
- **Combat Use:** Strong defensive shield; bonus when fighting MineralSaturated enemies that contaminated the battle field.

---

#### 12. **Flash Fuel** (Combustion)
- **Vosk Name (Pronunciation):** `flash fuel` [FLASH FYOOL]
- **Chemistry Concept:** `Combustion`
- **Effect Type:** `Damage`
- **Base Power:** 13
- **MP Cost:** 7
- **Unlock Condition:** `requiredLevel: 14, prerequisiteSpell: Ignite Vapor` (master combustion mechanics)
- **Reactions:**
  - `reactsWith: Flammable` → `transformsTo: None`, `reactionBonusDamage: +7` (fuel ignites in tremendous flash)
  - `reactsWith: Pressurized` → `transformsTo: None`, `reactionBonusDamage: +9` (explosive release of pressure + combustion)
- **Inflicts Condition:** `Burning` (2 turns, 5 DoT/turn)
- **Lore:** Inject accelerant into a flammable pool and ignite. Maximalist combustion — use carefully.
- **Combat Use:** Highest single-target combustion damage; ultimate nuke for Flammable/Pressurized enemies.

---

## Progression Arc & Spell Distribution

```
Level 1  → Freeze (starter, mandatory)
Level 2  → Combust
Level 3  → Neutralize (requires Freeze)
Level 4  → Crystallize
Level 6  → Melt (requires Freeze)
Level 7  → Evaporate (requires Freeze)
Level 8  → Corrode (requires Neutralize)
Level 9  → Surge Base (requires Corrode)
Level 10 → Ignite Vapor (requires Combust)
Level 11 → Condense (requires Evaporate)
Level 13 → Mineral Bind (requires Crystallize)
Level 14 → Flash Fuel (requires Ignite Vapor)
```

**Design Rationale:**

- **Level 1:** Single starting spell (Freeze) to boot the game
- **Levels 2–5:** Four foundational spells covering all four chemistry concepts
- **Levels 6–11:** Unlock prerequisites for chains (Melt/Evaporate follow Freeze; Corrode/Surge Base follow Neutralize; Ignite Vapor follows Combust; Condense follows Evaporate)
- **Levels 13–14:** Capstone spells for experienced players; minor role (shield + ultimate damage)

**No level 1 clumping:** Player gains 1–2 spells per level during early game, spreading learning to player.

---

## Reaction Matrix — Which Spells React With Which Conditions

| Spell | Primary Effect | Reacts With | Bonus | Transforms To | Material | Duration |
|---|---|---|---|---|---|---|
| Freeze | Damage | Liquid | +3 dmg | Solid | Phase | 2t |
| Combust | Damage | Flammable | +5 dmg | — | — | — |
| | | Pressurized | +8 dmg | — | — | — |
| Neutralize | Heal | Corroded | cleanse | — | — | — |
| Crystallize | Damage | MineralSaturated | +4 dmg | — | — | — |
| Melt | Damage | Solid | +4 dmg | Liquid | Phase | 2t |
| Evaporate | Damage | Liquid | +5 dmg | Vapor | Phase | 2t |
| Corrode | Damage | AlkalineBase | +6 dmg | — | — | — |
| Surge Base | Damage | AcidicFluid | +7 dmg | — | — | — |
| Ignite Vapor | Damage | Vapor | +6 dmg | — | — | — |
| | | Flammable | +4 dmg | — | — | — |
| Condense | Damage | Vapor | +5 dmg | Liquid | Phase | 2t |
| Mineral Bind | Shield | MineralSaturated | +8 shield | — | — | — |
| Flash Fuel | Damage | Flammable | +7 dmg | — | — | — |
| | | Pressurized | +9 dmg | — | — | — |

---

## Vosk Recognition Notes

**Phonetic Distinctness Checklist:**

- ✅ **Freeze** [FREEZ] — unique vowel pattern, clear fricative start
- ✅ **Combust** [kuhm-BUST] — "umm" sound distinguishes from others
- ✅ **Neutralize** [NOO-truh-lyz] — long vowel + "-ize" suffix distinctive
- ✅ **Crystallize** [KRIS-tuh-lyz] — "kr" cluster vs. others' single consonants
- ✅ **Melt** [MELT] — monosyllabic, very short, unlikely to confuse
- ✅ **Evaporate** [ih-VAP-uh-rayt] — 4 syllables, distinct rhythm
- ✅ **Corrode** [kuh-ROHD] — "oh" vowel + long "d" ending
- ✅ **Surge Base** [SURJ BAYS] — two-word, "urj" and long "a" distinct
- ✅ **Ignite Vapor** [ig-NYT VAY-per] — multi-syllabic second word prevents merging
- ✅ **Condense** [kun-DENS] — "den" nucleus different from "um", "ozz", etc.
- ✅ **Mineral Bind** [MIN-er-ul BYND] — three syllables first word; short "y" pronunciation second
- ✅ **Flash Fuel** [FLASH FYOOL] — initial cluster "fl" + "oo" vowel; two-word spacing

**Vosk Grammar Entry Pattern:**

```
<grammar>
  <rule id="spell_names">
    <one-of>
      <item> freeze </item>
      <item> combust </item>
      <item> neutralize </item>
      <item> crystallize </item>
      <item> melt </item>
      <item> evaporate </item>
      <item> corrode </item>
      <item> surge base </item>
      <item> ignite vapor </item>
      <item> condense </item>
      <item> mineral bind </item>
      <item> flash fuel </item>
    </one-of>
  </rule>
</grammar>
```

---

## Implementation Checklist for Asset Creation

Once DEV-37 is merged (SpellData refactored with `List<ReactionEntry>`):

- [ ] Create folder: `Assets/Data/Spells/` (if not exist)
- [ ] Create SpellData asset for **Freeze** (`SD_Freeze.asset`) — migrate existing + add ChemistryConcept, UnlockCondition
- [ ] Create SpellData asset for **Combust** (`SD_Combust.asset`) — migrate existing + update
- [ ] Create SpellData asset for **Neutralize** (`SD_Neutralize.asset`) — migrate existing + update
- [ ] Create SpellData asset for **Crystallize** (`SD_Crystallize.asset`)
- [ ] Create SpellData asset for **Melt** (`SD_Melt.asset`)
- [ ] Create SpellData asset for **Evaporate** (`SD_Evaporate.asset`)
- [ ] Create SpellData asset for **Corrode** (`SD_Corrode.asset`)
- [ ] Create SpellData asset for **Surge Base** (`SD_SurgeBase.asset`)
- [ ] Create SpellData asset for **Ignite Vapor** (`SD_IgniteVapor.asset`)
- [ ] Create SpellData asset for **Condense** (`SD_Condense.asset`)
- [ ] Create SpellData asset for **Mineral Bind** (`SD_MineralBind.asset`)
- [ ] Create SpellData asset for **Flash Fuel** (`SD_FlashFuel.asset`)
- [ ] Verify all spells added to **SpellVocabularyManager** asset for Vosk grammar
- [ ] Smoke test: launch Battle scene, verify each spell appears in voice input menu without errors
- [ ] Grep code for ANY hardcoded spell names (e.g., `if (spellName == "freeze")`); migrate all to `SpellData.spellName` references

---

## Design Rationale (What Makes These Spells Work)

### Chemistry Authenticity
Each spell maps to a real reaction or phase state:
- **Freeze/Melt/Evaporate/Condense** — actual phase transitions (taught in high school chemistry)
- **Neutralize/Corrode/Surge Base** — acid-base chemistry; real pH scales and exothermic reactions
- **Combustion spells** — oxygen + fuel; real stoichiometry
- **Crystallize/Mineral Bind** — precipitation from solution; real solubility mechanics

### Gameplay Balance
- **3 Damage spells at level 1–4** (Freeze, Combust, Crystallize) — offensive variety
- **1 Heal spell at level 3** (Neutralize) — mandatory early survival tool
- **Reaction prerequisites form chains** — forces players to learn foundations before advanced mechanics
- **Late-game spells (13+) are either ultra-damage or defense** — reward progression but don't invalidate early game

### Vosk Usability
Spell names chosen to be pronounceable and naturally distinct when said aloud, minimizing false positives. Two-word spells (Surge Base, Ignite Vapor, Mineral Bind, Flash Fuel) prevent single-word merging.

### Tutorial/Narrative Flow
- **Freeze** taught in opening cutscene (flooded hallway → phase-locked liquid)
- **Combust** taught immediately after (burning oil on floor → path cleared)
- **Neutralize** learned as consequence of facing acidic enemies early
- **Higher tiers** naturally discovered as enemies introduce new innate conditions (Pressurized, MineralSaturated, etc.)

---

## Open Questions (Confirm Before DEV-38 Execution)

1. **Mineral Bind as Shield:** Should the formula be `basepower` (15) or a flat HP amount (e.g., always 25 shield)? Current plan uses basepower, scaled with player INT stat (future phase).
2. **Two-word spell pronunciation:** Does Vosk handle "surge base" and "ignite vapor" reliably in current setup, or should they be single words (e.g., "basesurge", "ignitevapor")? Recommend testing in Phase 3 harness ASAP.
3. **Spell counts:** Is 12 spells enough to ship Phase 5, or add more? Current count covers tutorials (1), progression (11), and allows for +4 future additions (total 16) without bloat.
4. **Future spell slot:** Should we reserve names for planned spells (e.g., "Suppress Pressure", "Phase Lock")? Current list is closed; future spells defined in DEV-39+.

---

## See Also

- [DEV-37: ScriptableObject Data Assets](./2026-04-15-dev37-scriptable-object-data-assets.md) — SpellData structure + ReactionEntry schema
- [Chemistry Spell Combat System](../../../docs/game-mechanics/chemistry-spell-combat-system.md) — authoritative resolver order, condition catalogue, invariants
- [LORE_AND_MECHANICS.md](../../../docs/LORE_AND_MECHANICS.md) — four chemistry concepts, character backstory, tutorial flow
