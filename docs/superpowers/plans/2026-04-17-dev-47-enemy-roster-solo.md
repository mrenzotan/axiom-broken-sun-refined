# Dev-47: Enemy Roster — Axiom of the Broken Sun (Solo Campaign)

**Status:** Phase 5 — Final Design (3 Levels + 1 Final Boss) — SOLO PLAYTHROUGH  
**Total Enemies:** 8 minions + 3 bosses (non-recruitable) + 1 final boss = **12 enemies**  
**Asset Path:** `Assets/Data/Enemies/`  
**Party System:** NOT IMPLEMENTED (bosses are defeated, not recruited)

---

## Game Structure

- **Level 1 (Phase Change):** 3 minions + Boss 1 (Frost-Melt Sentinel)
- **Level 2 (Combustion):** 3 minions + Boss 2 (Living Furnace)
- **Level 3 (Acid-Base):** 2 minions + Boss 3 (Corrosion Queen)
- **Level 4 (Final):** No minions, only Null-King (ultimate challenge)

**All bosses are defeated and remain defeated. No recruitment mechanic.**

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
- **Final Boss (Null-King):** HP 280, ATK 20, DEF 9, SPD 10 (adjusted for solo difficulty)

---

# LEVEL 1: Phase Change Fundamentals

## 1. Meltspawn

**Chemistry Concept:** Phase Change — Liquid Variant  
**Innate Conditions:** `Liquid`  
**Stats:** HP 20, ATK 3, DEF 2, SPD 2  
**XP Reward:** 5  
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
**XP Reward:** 6  
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
**XP Reward:** 6  
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
**XP Reward:** 50  
**MP:** 20

**Lore:**  
An ancient guardian construct corrupted by the Cognition Cascade. Still seeks balance through endless phase-shifting. Kaelen defeats it to prove he understands the laws of nature.

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
Kaelen defeats the Sentinel in combat. Its form crumbles, but in that moment, Kaelen glimpses what it once was — a being of pure phase change, neither Solid nor Liquid, but both. He understands it now, and lets it rest.

**Sprite/Animation:**  
Humanoid form that shifts between crystalline (blue/white, angular, Solid state) and fluid (cyan/blue, flowing, Liquid state) every 2 turns. Phase transition plays a visual morph animation.

---

# LEVEL 2: Combustion & Acid-Base Chemistry

## 4. Spark Sprite

**Chemistry Concept:** Combustion — Ignition  
**Innate Conditions:** `Flammable`  
**Stats:** HP 20, ATK 5, DEF 1, SPD 4  
**XP Reward:** 8  
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
**XP Reward:** 8  
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
**XP Reward:** 8  
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
**XP Reward:** 60  
**MP:** 25

**Lore:**  
Embodiment of uncontrolled combustion. A raging inferno with terrible intelligence, burning with the fury of the Cascade itself. Kaelen must master combustion to defeat it.

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
The Furnace collapses, its uncontrolled flames extinguished. Kaelen realizes that combustion, like all chemistry, can be understood and controlled through knowledge. The flames fade to ash.

**Sprite/Animation:**  
Large burning humanoid form, shifts between bright orange (Fuel 0–2) to intense red (Fuel 3–5); glows brighter as counter climbs. Deflame/spark-burst animation on Ignition reaction.

---

# LEVEL 3: Acid-Base Chemistry

## 7. Acid Pool

**Chemistry Concept:** Acid-Base — Corrosion  
**Innate Conditions:** `AcidicFluid`  
**Stats:** HP 28, ATK 3, DEF 4, SPD 1  
**XP Reward:** 8  
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
**XP Reward:** 8  
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
**XP Reward:** 70  
**MP:** 30

**Lore:**  
Being of extreme pH, spreading corrosion in all directions. Slow but mathematically inevitable, transforming all matter in her path. Kaelen must understand neutralization to survive.

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
The Queen crumbles into harmless sediment. In her defeat, Kaelen understands the power of balance — acid and base, not as opposites, but as partners in equilibrium. She has taught him well.

**Sprite/Animation:**  
Insectoid/serpentine acidic form, toxic green coloring, graceful but threatening movement. Acid Zone appears as arena background environmental change (darkened overlay with dripping acid visual).

---

# LEVEL 4: Final Boss — Solo Campaign

## FINAL BOSS: The Null-King (Solo)

**Title:** The Unmade Truth (The Silent Equation)  
**Innate Conditions:** None (immune to normal chemical conditions; represents anti-chemistry)  
**Stats:** HP 280, ATK 20, DEF 9, SPD 10  
**XP Reward:** 200  
**MP:** 0

**Lore:**  
The architect of the Cognition Cascade itself. A being of total entropy and decay that seeks to reduce all complex matter to undifferentiated dust. It cannot be reasoned with or stabilized — only defeated through mastery of every chemistry principle Kaelen has learned.

---

## Boss Design Philosophy — Solo Win Path

Unlike the party-supported fight, Null-King requires **mastery and pattern recognition**. The fight has **two learnable phases** where the player must identify patterns and adapt. Victory comes through understanding, not brute force.

---

## Behavior & Phase Structure

### **Phase 1 (Turns 1–8) — Testing**

**Null-King assesses Kaelen's knowledge. Attacks are measured; defenses are formidable.**

**Stat Adjustments:**
- ATK: 16 (lower than Phase 2)
- DEF: 9 (moderate)
- SPD: 10 (very fast)

**Core Mechanic: Reaction Immunity**
- Null-King has **NO innate conditions** and resists chemistry magic
- Spells deal minimal base damage (50% effectiveness)
- However, Null-King can be damaged through **weaknesses it creates itself**

**Phase 1 Attack Pattern (repeating cycle):**

**Turn 1:** Applies **Frozen** status to Kaelen (prevents spell casting next turn)
- Basic attack: 16 damage
- Teaching: Frozen denies spells; prepare defensive spells before Frozen hits

**Turn 2:** Single target heavy strike: 20 damage
- No status effect
- Teaching: Brute force turns exist; manage HP accordingly

**Turn 3:** Applies **Burning** status to Kaelen (2 turns, 5 DoT/turn)
- Basic attack: 16 damage
- Teaching: Burning escalates; balance offense and defense

**Turn 4:** AOE-style "Stillness Wave" → reduces all of Kaelen's spell costs by 50% for NEXT TURN ONLY
- This is a disguised weakness window
- Basic attack: 16 damage
- Teaching: Even Null-King's actions create opportunity

**Cycle Repeats**

**Winnable Phase 1 Strategy:**
- Turn 1 (Null-King applies Frozen): Kaelen uses basic attack or buff spell before Frozen hits
- Turn 2 (Heavy strike): Kaelen heals or shields
- Turn 3 (Burning applied): Kaelen casts Neutralize-equivalent (if available) or uses basic attack
- Turn 4 (Stillness Wave): **CRITICAL WINDOW** — Kaelen casts high-cost spell during 50% cost reduction
- Repeat; whittle King down with strategic spell use during Stillness Windows

**HP Threshold:** At 140 HP remaining (50% health), Null-King transitions to Phase 2

---

### **Phase 2 (Turns 9+) — Escalation**

**Null-King recognizes Kaelen understands its patterns. It evolves its strategy. The true test begins.**

**Stat Adjustments:**
- ATK: 20 (increases to maximum)
- DEF: 9 (unchanged)
- SPD: 10 (unchanged)

**Phase 2 Mechanic: Void Weakness (Player-Created Weakness)**

Null-King gains a **learnable weakness**:
- If Kaelen casts 3+ different spell types within 4 consecutive turns, Null-King becomes **destabilized**
  - Diverse spell casting (phase change + combustion + acid-base, etc.) = Break state
  - Break state lasts 1 turn; Null-King takes +50% damage
  - Reinforces learning: mastery of ALL chemistry concepts beats narrow strategy

**Phase 2 Attack Pattern (repeating cycle, more aggressive):**

**Turn 1:** Applies **Corroded** status to Kaelen (3 turns, escalating DoT)
- Basic attack: 20 damage
- Teaching: DoT becomes more dangerous; escalation pressure

**Turn 2:** Two strikes in succession: 20 + 15 damage (multi-hit)
- No status effect
- Teaching: Burst damage requires planning

**Turn 3:** "Entropy Surge" — Null-King attacks + applies random status (50% chance Frozen, 50% Burning)
- Damage: 20
- Teaching: Uncertainty requires flexibility

**Turn 4:** Resets some of Kaelen's resources or applies minor debuff to reduce next turn's effectiveness
- Basic attack: 20 damage
- Teaching: Resource management pressure

**Cycle Repeats (with escalation: ATK+1 per cycle after Turn 1)**

**Winnable Phase 2 Strategy:**
- Use diverse spell types to trigger Void Weakness (Break state)
- Example sequence: Cast Phase Change spell → Combustion spell → Acid-Base spell (within 4 turns) → Null-King breaks
- During Break state, unleash strongest spell for massive damage
- Manage Corroded escalation; balance offense and defense
- Repeat break cycles until Null-King reaches 0 HP

**HP Threshold:** 0 HP = Victory

---

## Victory Condition & Ending

**Solo Victory Requirements:**
1. Survive Phase 1 by identifying Stillness Window pattern (50% spell cost reduction on Turn 4 of cycle)
2. Transition to Phase 2 at 140 HP
3. Master diversity: recognize that casting multiple spell types triggers Void Weakness
4. Execute break cycles: trigger weakness → burst damage → repeat
5. Defeat Null-King at 0 HP

**Victory Narrative:**
Kaelen lands the final spell. Null-King fractures, revealing not a body, but pure void — the absence of all matter and reaction. As it collapses, Kaelen understands the fundamental truth:

*All things are made of reaction, order from chaos. Where there is no reaction, there is nothing. And where there is understanding, there is power.*

The Null-King is not destroyed; it is **unraveled**. The world, freed from its pull toward entropy, begins to stabilize. The fractured sun glows brighter.

Kaelen stands alone, his Catalyst Arm still glowing with crystalline light. He has mastered the law of the broken world.

**Sprite/Animation:**
Abstract void entity, fractal/crystalline geometry, warps space-time around itself. Counter-colored to all other enemies (desaturated grayscale with void-black edges and space distortion effects). 

Phase 1: Deliberate, measured movements; attacks are calculated.
Phase 2: Erratic, aggressive movements; reality distortions intensify around it.
Final Hit: Shatters into cascading fractals that dissolve into nothingness.

---

## Difficulty Tuning Notes

### If too hard:
- Increase Stillness Window frequency (every 3 turns instead of 4) in Phase 1
- Lower Phase 2 ATK escalation rate
- Reduce Corroded escalation multiplier (use 1.0×, 1.2×, 1.5× instead of 1.0×, 1.5×, 2.0×)
- Increase HP threshold for Phase 2 transition (150 HP instead of 140 HP, giving more Phase 1 damage time)

### If too easy:
- Require 4+ different spell types for Void Weakness (not 3+)
- Increase healing spell costs in Phase 2
- Add Phase 1→2 transition: Null-King gains +2 ATK permanently
- Add confusion mechanic: random spell failure on 15% of casts in Phase 2

---

## Summary: Solo-Focused Enemy Roster

| Level | Enemy | Type | Conditions | Stats (HP/ATK/DEF/SPD) | XP | Role |
|-------|-------|------|-----------|--------|-----|------|
| 1 | Meltspawn | Minion | Liquid | 20/3/2/2 | 5 | Phase teach |
| 1 | Frostbite Creeper | Minion | Liquid | 22/4/1/3 | 6 | Immunity teach |
| 1 | Void Wraith | Minion | Vapor | 24/4/1/4 | 6 | Elemental teach |
| 1 | Frost-Melt Sentinel | Boss | Solid↔Liquid | 100/10/5/6 | 50 | Phase leader |
| 2 | Spark Sprite | Minion | Flammable | 20/5/1/4 | 8 | Ignition teach |
| 2 | Gas Bloater | Minion | Pressurized | 25/3/1/2 | 8 | Pressure teach |
| 2 | Volatile Residue | Minion | Pressurized | 22/4/1/3 | 8 | Escalation teach |
| 2 | Living Furnace | Boss | Flammable+Pressurized | 120/14/6/7 | 60 | Combustion leader |
| 3 | Acid Pool | Minion | AcidicFluid | 28/3/4/1 | 8 | Slow tank |
| 3 | Acid Slug | Minion | AcidicFluid | 18/5/1/4 | 8 | Fast threat |
| 3 | Corrosion Queen | Boss | AcidicFluid+MineralSaturated | 140/12/8/5 | 70 | Acid-Base leader |
| 4 | Null-King | Final Boss | None | 280/20/9/10 | 200 | Solo Ultimate |

---

## Chemistry Coverage Checklist

- ✅ **Level 1 (Phase Change):** Meltspawn + Frostbite Creeper + Void Wraith + Frost-Melt Sentinel (all phase change)
- ✅ **Level 2 (Combustion):** Spark Sprite + Gas Bloater + Volatile Residue + Living Furnace (all combustion)
- ✅ **Level 3 (Acid-Base):** Acid Pool + Acid Slug + Corrosion Queen (all acid-base)
- ✅ **Level 4 (Solo Challenge):** Null-King (pattern recognition + spell diversity mastery)

**Each level teaches ONE core chemistry principle. Final boss requires mastery of ALL principles.**

---

## Design Philosophy — Solo Campaign

This roster enforces the game's core principle: **chemistry is the primary tool**. Each enemy embodies one or two chemistry concepts and teaches the player through combat. The final boss is not defeated through party synergy, but through **understanding and mastery** — the player must recognize patterns, adapt to escalation, and leverage diverse knowledge to overcome the ultimate challenge.

The fight is **learnable and fair**. Once the player understands the pattern (Stillness Window in Phase 1, Void Weakness in Phase 2), victory is achievable through skill and preparation.

