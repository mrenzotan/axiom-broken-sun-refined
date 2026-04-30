# Enemy Roster — Axiom of the Broken Sun

**Status:** Phase 5 — Final Design (3 Levels + 1 Final Boss)  
**Total Enemies:** 8 minions + 4 bosses = **12 enemies** (reorganized by chemistry concept)  
**Asset Path:** `Assets/Data/Enemies/`

---

## Game Structure

- **Level 1 (Phase Change):** 3 minions + Boss 1 (Frost-Melt Sentinel)
- **Level 2 (Combustion):** 3 minions + Boss 2 (Living Furnace)
- **Level 3 (Acid-Base):** 2 minions + Boss 3 (Corrosion Queen)
- **Level 4 (Final):** No minions, only Null-King

---

## Stat Baseline & Scaling

### Level 1 Minions
- **Range:** HP 20–24, ATK 3–4, DEF 1–2, SPD 2–4
- **Focus:** Phase Change teaching (three distinct variants)

### Level 2 Minions
- **Range:** HP 20–25, ATK 3–5, DEF 1–2, SPD 2–4
- **Focus:** Combustion mechanics (Flammable & Pressurized variants)

### Level 3 Minions
- **Range:** HP 18–28, ATK 3–5, DEF 1–4, SPD 1–4
- **Focus:** Acid-Base chemistry (AcidicFluid variants)

### Boss Range
- **Boss 1 (Level 1):** HP 100, ATK 10, DEF 5, SPD 6
- **Boss 2 (Level 2):** HP 120, ATK 14, DEF 6, SPD 7
- **Boss 3 (Level 3):** HP 140, ATK 12, DEF 8, SPD 5
- **Final Boss (Null-King):** HP 300, ATK 22, DEF 10, SPD 9

---

## XP Progression & Recommended Encounter Counts

XP rewards are tuned against Kaelen's level-up curve in `Assets/Data/Characters/CD_Player_Kaelen.asset` → `Xp To Next Level Curve` (per-level deltas: 100, 250, 500, 900, 1500, 2400, 3800, 6000, 9500).

### Cumulative XP to reach each player level

| Player Level | Cumulative XP |
|--------------|---------------|
| 2 | 100 |
| 3 | 350 |
| 4 | 850 |
| 5 | 1,750 |
| 6 | 3,250 |
| 7 | 5,650 |
| 8 | 9,450 |
| 9 | 15,450 |
| 10 | 24,950 |

### Target player level by game level

| Game Level | Target Lv at end | XP earned in this level |
|------------|------------------|--------------------------|
| Level 1 (Phase Change) | Lv 3 | ~360 |
| Level 2 (Combustion) | Lv 5 | ~1,370 (cumul ~1,730) |
| Level 3 (Acid-Base) | Lv 7 | ~4,000 (cumul ~5,730) |
| Level 4 (Final) | Lv 9 | ~9,800 (cumul ~15,530) |

### Recommended enemy instances per substage

Counts below reflect the minimum encounters needed to land Kaelen near the cumulative target after each game level. Designers can add more for difficulty pacing — just keep the per-enemy XP values fixed so the curve still resolves.

**Level 1 — Snow Mountain (~360 XP total)**
- Substage 1-1 (tutorial): 1 × Meltspawn → 20 XP
- Substage 1-2: 3 × Frostbite Creeper → 75 XP
- Substage 1-3: 3 × Void Wraith → 90 XP
- Substage 1-4 (boss): Frost-Melt Sentinel → 175 XP

**Level 2 — Combustion (~1,370 XP total)**
- Substage 2-1: 3 × Spark Sprite → 240 XP
- Substage 2-2: 3 × Gas Bloater → 300 XP
- Substage 2-3: 3 × Volatile Residue → 330 XP
- Substage 2-4 (boss): Living Furnace → 500 XP

**Level 3 — Acid-Base (~4,000 XP total)**
- Substage 3-1: 4 × Acid Pool → 1,200 XP
- Substage 3-2: 4 × Acid Slug → 1,400 XP
- Substage 3-3 (boss): Corrosion Queen → 1,400 XP

**Level 4 — Final Boss (~9,800 XP total)**
- Null-King → 9,800 XP

> If encounter counts change, scale per-enemy XP proportionally (e.g., halving Lv 1 minion count → roughly double their XP) so each game level still ends at its target player level.

---

# LEVEL 1: Phase Change Fundamentals

## 1. Meltspawn

**Chemistry Concept:** Phase Change — Liquid Variant  
**Innate Conditions:** `Liquid`  
**Stats:** HP 20, ATK 3, DEF 2, SPD 2  
**XP Reward:** 20  
**MP:** 0

**Lore:**  
A sentient mass of sloshing liquid matter. Begins in fluid form, vulnerable to freezing.

**Behavior:**
- Starts in Liquid form (physical attacks deal reduced damage due to liquid immunity)
- Basic attack: 3 damage
- **Freeze reaction** → becomes Brittle/Solid state, takes double damage for 1 turn
- If ignored for several turns → gradually reforms into full Liquid state, restoring immunity
- Teaching points:
  - Physical immunity on Liquid enemies
  - Breaking immunity with freeze reaction
  - Consequence of delaying phase-change spells (enemy recovers)
  - Introduces phase cycling mechanics

**Sprite/Animation:**  
Sloshing liquid blob, clear/translucent appearance, fluid motion, bubbling animation.

---

## 2. Frostbite Creeper

**Chemistry Concept:** Phase Change — Liquid Variant  
**Innate Conditions:** `Liquid`  
**Stats:** HP 22, ATK 4, DEF 1, SPD 3  
**XP Reward:** 25  
**MP:** 0

**Lore:**  
Sentient frozen water writhing with pseudo-consciousness. Cold drips from its form.

**Behavior:**
- Physical attacks deal reduced damage due to Liquid immunity (enemy is physical-immune while Liquid)
- Basic attack: 4 damage + applies **Evaporating** status (2 turns, 3 DoT/turn)
- **Freeze reaction** → temporarily becomes Solid (2 turns), loses immunity, takes full physical damage
- Teaching points:
  - Physical immunity mechanics
  - Breaking immunity with correct phase-change spell
  - First status effect (DoT) introduction

**Sprite/Animation:**  
Blue dripping humanoid shape, fluid motion, frost particle effects on attacks.

---

## 3. Void Wraith

**Chemistry Concept:** Phase Change — Vapor Variant  
**Innate Conditions:** `Vapor`  
**Stats:** HP 24, ATK 4, DEF 1, SPD 4  
**XP Reward:** 30  
**MP:** 0

**Lore:**  
Ghostly vapor born from disrupted matter. Intangible, fleeting, and relentless.

**Behavior:**
- Physical immunity (Vapor condition makes it intangible)
- Basic attack: 4 damage, slightly faster (SPD 4)
- **Condense reaction** (phase change) → temporarily becomes Liquid (2 turns), loses immunity
- **Combustion reaction on Vapor** → special explosive effect, bonus damage, does NOT apply Burning (elemental interaction)
- Teaching points:
  - Different spells effective on different materials
  - Elemental synergies (Combust works especially well on gas)
  - Vapor immunity mechanics

**Sprite/Animation:**  
Wispy blue mist, floaty movement, semi-transparent, windy particle effects.

---

## BOSS 1: Frost-Melt Sentinel

**Title:** The Phasekeeper  
**Innate Conditions:** Alternates `Solid` ↔ `Liquid` every 2 turns  
**Stats:** HP 100, ATK 10, DEF 5, SPD 6  
**XP Reward:** 175  
**MP:** 20

**Lore:**  
An ancient guardian construct corrupted by the Cognition Cascade. Still seeks balance through endless phase-shifting. When defeated, Kaelen stabilizes it, and it becomes an ally.

**Behavior:**

**Phase 1 (Turns 1–2) — Solid State:**
- Physical attacks deal normal damage
- Freeze spell is ineffective (already Solid)
- **Melt reaction** → triggers Break state (takes +50% damage for 1 turn), visual stun/flash effect

**Phase 2 (Turns 3–4) — Liquid State:**
- Physical immunity activated
- Melt spell is ineffective (already Liquid)
- **Freeze reaction** → triggers Break state (takes +50% damage for 1 turn), visual freeze/stun effect

**Shared Mechanics:**
- Automatically alternates phases every 2 turns (predictable)
- Basic attack: 10 damage (consistent across phases)
- No additional status applications
- Win Condition: Predict incoming phase, cast correct phase-change spell to trigger Break, burst-damage during vulnerability window

**Post-Defeat Narrative:**  
Kaelen recognizes the Sentinel's nature as a "Theorem" — a pure expression of a chemistry principle. He speaks **"React: Stabilize"** and the Sentinel collapses. Instead of dissolving, it reforms into a sentient, stable ally: **The Phasekeeper**.

**Recruitable Ally Bonus:**  
Passive ability amplifies duration of Freeze and Melt spells; ultimate ability forces all enemies to phase-shift, exposing weaknesses.

**Sprite/Animation:**  
Humanoid form that shifts between crystalline (blue/white, angular, Solid state) and fluid (cyan/blue, flowing, Liquid state) every 2 turns. Phase transition plays a visual morph animation.

---

# LEVEL 2: Combustion & Acid-Base Chemistry

## 4. Spark Sprite

**Chemistry Concept:** Combustion — Ignition  
**Innate Conditions:** `Flammable`  
**Stats:** HP 20, ATK 5, DEF 1, SPD 4  
**XP Reward:** 80  
**MP:** 0

**Lore:**  
Volatile wisp of unreacted fire. Burns with wild aggression, a spark waiting for fuel.

**Behavior:**
- Basic attack: 5 damage, relatively fast (SPD 4)
- **Combustion reaction on Flammable** → applies **Burning** status (2 turns, 5 DoT/turn) + bonus damage
- **Freeze reaction** → does NOT suppress Flammable or prevent future Combustion
- Teaching points:
  - Exothermic reaction (Combustion) and DoT mechanics
  - Flammable condition and how to trigger it
  - Status escalation (managing active DoT)

**Sprite/Animation:**  
Flickering ember sprite, warm colors (orange/red), fluid flame animation with heat shimmer.

---

## 5. Gas Bloater

**Chemistry Concept:** Combustion — Pressure & Self-Detonation  
**Innate Conditions:** `Pressurized`  
**Stats:** HP 25, ATK 3, DEF 1, SPD 2  
**XP Reward:** 100  
**MP:** 0

**Lore:**  
Bubble of unstable vapor barely held together. Pressure builds inside every moment, threatening catastrophic release.

**Behavior:**

**Turn Cycle:**
- **Turns 1–2:** Takes no action, visibly inflates (sprite enlarges each turn, warning player of danger)
- **Turn 3:** Auto-detonates unless prevented, deals 25 damage to Kaelen + applies **Burning**

**Spell Reactions:**
- **Combustion reaction on Pressurized** → early detonation (15 damage, prevents overcharge explosion)
  - Triggers **Break state** (1 turn, Bloater takes +30% damage)
  - Teaching point: Strategic early spell casting prevents catastrophic damage
  
- **Freeze reaction on Pressurized** → phases to Liquid, vents pressure safely, prevents detonation

**Teaching Points:**
- Forward planning (know turn 3 is dangerous)
- Prevention vs. reactive damage management
- Turn-based decision making with consequences

**Sprite/Animation:**  
Inflating bubble sprite, pulsing larger each turn (visual timer); deflating/bursting animation on early Combustion.

---

## 6. Volatile Residue

**Chemistry Concept:** Combustion — Pressure Escalation  
**Innate Conditions:** `Pressurized`  
**Stats:** HP 22, ATK 4, DEF 1, SPD 3  
**XP Reward:** 110  
**MP:** 0

**Lore:**  
Unstable pressurized vapor from combustion waste. Builds internal pressure each turn, growing deadlier with each moment.

**Behavior:**
- Moderate threat across stats
- Basic attack: 4 damage
- **Passive Mechanic:** Gains +1 ATK each turn that passes (escalating threat)
  - Turn 1: 4 ATK → Turn 2: 5 ATK → Turn 3: 6 ATK, etc.
  - Visual: sprite grows darker/more menacing as ATK stacks
- **Combustion reaction on Pressurized** → detonates early, resets ATK back to 4, applies **Burning** status
- Teaching points:
  - Pressure escalation mechanic (different from Gas Bloater's countdown)
  - Value of early combustion to prevent ATK spiral
  - Different pressurized mechanic variations (timing vs. buildup)

**Sprite/Animation:**  
Swirling dark vapor, growing more concentrated/darker each turn; bright burst animation on early Combustion.

---

## BOSS 2: Living Furnace

**Title:** The Regulated Flame  
**Innate Conditions:** `Flammable`, `Pressurized`  
**Stats:** HP 120, ATK 14, DEF 6, SPD 7  
**XP Reward:** 500  
**MP:** 25

**Lore:**  
Embodiment of uncontrolled combustion. A raging inferno with terrible intelligence, burning with the fury of the Cascade itself.

**Behavior:**

**Fuel Counter Mechanic** (Core Mechanic):
- Starts at 0 Fuel
- Each turn the King does not take action (or takes basic attack), Fuel increments by 1
- At Fuel 5: Scheduled detonation next turn, deals 40 damage to Kaelen + applies **Burning**
- **Visual indicator:** Sprite grows larger/glows redder as Fuel counter climbs (0→5)

**Combustion (Ignite) Reaction on Flammable:**
- Forces early detonation at current Fuel level
  - Fuel 1: 15 damage
  - Fuel 2: 20 damage
  - Fuel 3: 25 damage
  - Fuel 4: 35 damage
  - Fuel 5: 40 damage (same as auto-detonation)
- Resets counter to 0
- **Triggers Break state** (1 turn, King takes +30% damage)
- Teaching point: Player must choose between early safe detonation or risking high-damage buildup

**Basic Attack:**
- 14 damage + applies **Burning** (escalates threat each turn)

**Win Strategy:**
- Balance Fuel counter (prevent overcharge disasters)
- Use Combustion reactions to create Break windows
- Burst-damage King during Break vulnerability
- Manage Burning status to avoid tick damage spiral

**Post-Defeat Narrative:**  
The Furnace collapses, its uncontrolled flames dimming. Kaelen speaks **"React: Stabilize"** and the fire transforms into a controlled, intelligent entity: **The Regulated Flame**.

**Recruitable Ally Bonus:**  
Passively amplifies damage from ignition and chain reactions. Ultimate ability delivers controlled combustion burst that never harms allies.

**Sprite/Animation:**  
Large burning humanoid form, shifts between bright orange (Fuel 0–2) to intense red (Fuel 3–5); glows brighter as counter climbs. Deflame/spark-burst animation on Ignition reaction.

---

# LEVEL 3: Acid-Base Chemistry

## 7. Acid Pool

**Chemistry Concept:** Acid-Base — Corrosion  
**Innate Conditions:** `AcidicFluid`  
**Stats:** HP 28, ATK 3, DEF 4, SPD 1  
**XP Reward:** 300  
**MP:** 0

**Lore:**  
Sentient puddle of corrosive liquid left behind by the Cascade. Slow, methodical, and relentless.

**Behavior:**
- **Very slow (SPD 1)** — Kaelen acts 8+ times before Acid Pool's next turn
- **High DEF (4)** relative to HP — demonstrates tanky archetype (high defense but low damage output)
- Basic attack: 3 damage + applies **Corroded** status (3 turns, escalating DoT: 4×1.0, 4×1.5, 4×2.0 damage per turn)
- **Base neutralization reaction** → removes AcidicFluid condition + cleanses Corroded from Kaelen
- Teaching points:
  - Slow enemies allow player action economy advantage
  - DoT pressure from status effects (focus on management)
  - Acid-Base reaction priority (when to cleanse vs. endure)

**Sprite/Animation:**  
Bubbling puddle shape, acidic green coloring, slow blob movement, splash animation on attack.

---

## 8. Acid Slug

**Chemistry Concept:** Acid-Base — Fast Corrosion  
**Innate Conditions:** `AcidicFluid`  
**Stats:** HP 18, ATK 5, DEF 1, SPD 4  
**XP Reward:** 350  
**MP:** 0

**Lore:**  
Fast-moving acidic creature, trailing corrosive residue. The opposite threat profile of Acid Pool.

**Behavior:**
- Fast (SPD 4), relatively high ATK (5) — opposite of Acid Pool's slow tankiness
- Basic attack: 5 damage + applies **Corroded** status (3 turns, escalating DoT: 4×1.0, 4×1.5, 4×2.0 damage per turn)
- **Base neutralization reaction** → removes AcidicFluid condition + cleanses Corroded from Kaelen
- Teaching points:
  - Fast acid threats require quick response (vs. slow Acid Pool's methodical approach)
  - Corroded status management under time pressure
  - Contrast in threat profiles (speed vs. defense)

**Sprite/Animation:**  
Small acidic creature with a trail of green corrosive liquid, quick darting movement, acidic particles on attacks.

---

## BOSS 3: Corrosion Queen

**Title:** Bearer of Balance  
**Innate Conditions:** `AcidicFluid`, `MineralSaturated`  
**Stats:** HP 140, ATK 12, DEF 8, SPD 5  
**XP Reward:** 1400  
**MP:** 30

**Lore:**  
Being of extreme pH, spreading corrosion in all directions. Slow but mathematically inevitable, transforming all matter in her path.

**Behavior:**

**Acid Zone Arena Hazard** (Core Mechanic):
- Applies automatically on Turn 1
- Covers entire arena (visual: darkened arena background with acid drips)
- Each turn, Zone applies **Corroded** status to Kaelen (escalating damage)
- Queen gains a protective shield while Zone active (blocks incoming spell damage)
- Teaching point: Arena hazards require prioritization (clear zone vs. damage enemy)

**Base Neutralization Reaction on AcidicFluid:**
- Destroys Acid Zone (zone disappears, arena returns to normal)
- **Simultaneously** activates Break state (1 turn, Queen takes +25% damage, ATK reduced from 12 to 9)
- Cleanses Corroded from Kaelen
- Multi-benefit reaction: hazard removal + enemy break + status cleanse
- Teaching point: Strategic spell casting provides multiple benefits

**Basic Attack:**
- 12 damage + applies **Corroded** (additional stack, separate from Zone)

**Win Strategy:**
- Manage Corroded damage while Zone active
- Predict when to use Base spell for zone destruction + Break window
- Damage Queen during Break vulnerability (reduced ATK + increased damage taken)
- Prevent Corroded stacking out of control

**Post-Defeat Narrative:**  
The Queen collapses, her toxic nature neutralized. Kaelen stabilizes her essence, and she transforms into **Bearer of Balance**, an ally devoted to equilibrium.

**Recruitable Ally Bonus:**  
Support ally; passively applies enemy debuffs. Ultimate ability clears all hazards and heals allies.

**Sprite/Animation:**  
Insectoid/serpentine acidic form, toxic green coloring, graceful but threatening movement. Acid Zone appears as arena background environmental change (darkened overlay with dripping acid visual).

---

# LEVEL 4: Final Boss

## FINAL BOSS: The Null-King

**Title:** The Unmade Truth (The Silent Equation)  
**Innate Conditions:** None (immune to normal chemical conditions; represents anti-chemistry)  
**Stats:** HP 300, ATK 22, DEF 10, SPD 9  
**XP Reward:** 9800  
**MP:** 0

**Lore:**  
The architect of the Cognition Cascade itself. A being of total entropy and decay that seeks to reduce all complex matter to undifferentiated dust. It cannot be reasoned with or stabilized — only defeated and banished.

**Behavior:**

**Phase 1 (Turns 1–6) — Dormancy:**
- Low initial ATK (15, can be escalated later)
- Spells deal minimal damage (Null-King is resistant to chemistry magic, not made of material it can react with)
- Basic attack: 15 damage + applies **Corroded** (escalating)
- **Stillness Field** mechanic (every 3 turns):
  - Creates "Stillness Field" (visual: arena warps, sound muffles)
  - Duration: 1 turn
  - During field: Kaelen cannot cast spells (action locked, only basic attack available)
  - Forces player to prepare spells *before* field arrives
  - Teaching point: Advanced planning (prepare buff/defense before lockdown)

**Phase 2 (Turn 7+) — Awakening:**
- Recognizes Kaelen's patterns; becomes openly hostile
- ATK escalates to 22
- Stillness Field severity increases (blocks 2 turns instead of 1)
- Basic attack cycles through multiple status applications in sequence:
  - Turn N: applies **Frozen**
  - Turn N+1: applies **Burning**
  - Turn N+2: applies **Corroded**
  - Repeat cycle
- **Void Consumption** mechanic (unique):
  - Each spell cast by Kaelen powers the Null-King temporarily
  - King gains +2 ATK for its next turn after each enemy spell
  - Paradox: Spells are necessary for damage, but casting empowers the enemy
  - Inaction is penalized: King heals +5 HP per turn Kaelen does not act
  - Forces player to balance spell frequency with short-term power escalation risk

**Win Conditions:**

**Solo Route (Challenge Mode):**
- Kaelen alone requires 12+ successful spell hits to damage Null-King significantly
- Extremely difficult; not intended as main path
- Possible for expert players seeking mastery challenge

**Intended Route (Party Cooperation):**
- Requires Kaelen + all 3 stabilized bosses (The Phasekeeper, The Regulated Flame, Bearer of Balance)
- Multi-turn choreography:
  1. The Phasekeeper phases the King (breaks anti-chemistry shell temporarily, makes it vulnerable to chemistry)
  2. The Regulated Flame ignites the exposed core (high burst damage)
  3. Bearer of Balance neutralizes byproducts (cleanses enemy buffs, stabilizes arena)
  4. Kaelen delivers final **"React: Stabilize"** spell
- Reaction chain repeats until Null-King is permanently defeated/banished
- Narrative climax: All learned chemistry principles unite to restore balance

**Sprite/Animation:**  
Abstract void entity, fractal/crystalline geometry, warps space-time around itself. Counter-colored to all other enemies (desaturated grayscale with void-black edges and space distortion effects). Phase 1→2 transition shows visible corruption spread; Phase 2 has reality-breaking visual effects (glitches, inverse colors, dimensional tears).

---

## Summary: Enemy Roster Table

| Level | Enemy | Type | Conditions | Stats (HP/ATK/DEF/SPD) | XP | Role |
|-------|-------|------|-----------|--------|-----|------|
| 1 | Meltspawn | Minion | Liquid | 20/3/2/2 | 20 | Phase teach |
| 1 | Frostbite Creeper | Minion | Liquid | 22/4/1/3 | 25 | Immunity teach |
| 1 | Void Wraith | Minion | Vapor | 24/4/1/4 | 30 | Elemental teach |
| 1 | Frost-Melt Sentinel | Boss | Solid↔Liquid | 100/10/5/6 | 175 | Phase leader |
| 2 | Spark Sprite | Minion | Flammable | 20/5/1/4 | 80 | Ignition teach |
| 2 | Gas Bloater | Minion | Pressurized | 25/3/1/2 | 100 | Pressure teach |
| 2 | Volatile Residue | Minion | Pressurized | 22/4/1/3 | 110 | Escalation teach |
| 2 | Living Furnace | Boss | Flammable+Pressurized | 120/14/6/7 | 500 | Combustion leader |
| 3 | Acid Pool | Minion | AcidicFluid | 28/3/4/1 | 300 | Slow tank |
| 3 | Acid Slug | Minion | AcidicFluid | 18/5/1/4 | 350 | Fast threat |
| 3 | Corrosion Queen | Boss | AcidicFluid+MineralSaturated | 140/12/8/5 | 1400 | Acid-Base leader |
| 4 | Null-King | Final Boss | None | 300/22/10/9 | 9800 | Anti-chemistry |

---

## Chemistry Coverage Checklist

- ✅ **Level 1 (Phase Change):** Meltspawn + Frostbite Creeper + Void Wraith + Frost-Melt Sentinel (all phase change)
- ✅ **Level 2 (Combustion):** Spark Sprite + Gas Bloater + Volatile Residue + Living Furnace (all combustion)
- ✅ **Level 3 (Acid-Base):** Acid Pool + Acid Slug + Corrosion Queen (all acid-base)
- ✅ **Level 4 (Anti-Chemistry):** Null-King (requires party cooperation)

**Each level teaches ONE core chemistry principle through its boss and all minions.**

---

## Next Steps: Asset Creation

When ready to create ScriptableObject assets:

1. Create folder: `Assets/Data/Enemies/`
2. For each enemy, create `ED_{EnemyName}.asset` using EnemyData ScriptableObject
3. **Assign a battle background:** Each region needs a `BattleEnvironmentData` asset. Create one per region:

   - In `Assets/Data/`, create a subfolder `BattleEnvironments/` (if it doesn't exist)
   - Right-click `BattleEnvironments/` → **Create → Axiom → Data → Battle Environment Data**
   - Name it `BED_<PascalCaseRegion>` (e.g. `BED_SnowMountain`, `BED_CombustionLabs`)
   - Select the asset → Inspector: drag the region's background sprite into **Background Sprite**, set **Ambient Tint** to match the atmosphere (white for neutral, cold blue for Snow Mountain, etc.)
   - For each `ExplorationEnemyCombatTrigger` in the platformer scene, drag the BED into the **Battle Environment** field

   All enemies in the same region share one BED. See [`docs/dev-notes/adding-battle-environments.md`](docs/dev-notes/adding-battle-environments.md) for details.

4. Fields to populate per enemy:
   - `enemyName` (display name)
   - `maxHP`, `maxMP`, `atk`, `def`, `spd` (base stats)
   - `innateConditions` (List<ChemicalCondition>)
   - `xpReward` (experience points)
   - `baseAttackPower` (default 1.0x multiplier for now)

5. Boss-specific fields (Phase 5):
   - Custom BattleAI behavior script reference
   - Phase transition data (if multi-phase)
   - Special mechanic parameters (Fuel counter, Zone duration, etc.)

6. Sprite and Animator Controller assignment (Phase 5):
   - Link sprite sheet per enemy
   - Link animator controller with idle/attack/hurt/defeat animations

---

## Design Philosophy

This roster enforces the game's core principle: **chemistry is the primary tool**. Each enemy embodies one or two chemistry concepts and teaches the player through combat. Bosses are "Theorems" — pure expressions of chemistry principles that become allies after stabilization, reinforcing the narrative of balance and understanding vs. entropy and destruction.

