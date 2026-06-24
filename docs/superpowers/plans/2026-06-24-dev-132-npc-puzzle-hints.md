# DEV-132 — NPC Chemistry-Puzzle Hints Implementation Plan

> **For agentic workers:** This is a **content/text** plan, not a C# plan. There is no TDD cycle, no scripts, no asmdefs, no automated tests. All edits are performed by the user in the Unity Editor Inspector. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Teach the freeze / combust / neutralize environmental puzzles diegetically by adding implicit hint lines to three existing NPC dialogue assets — instead of firing a tutorial prompt at each new obstacle type.

**Approach:** Edit the `rawDialogueText` field of three `DialogueData` ScriptableObjects via the Unity Inspector. Each hint surfaces the exact spell incantation **once**, emphasized with `*asterisks*`, spoken in the NPC's own voice — matching the convention already present in `DD_Tavin` (`*neutralize*`). No code or scene changes.

**Tech Stack:** Unity 6 ScriptableObject `.asset` files (`Axiom.Data.DialogueData`), edited via Inspector. UVCS for version control.

## Global Constraints

- **Implicit, not instructional.** A hint evokes the mechanic; it never says "cast X" and Kaelen never echoes the word back as an instruction.
- **Exact incantations only.** The spoken word must match the spell's `spellName` verbatim, or the player's mic cast fails: `freeze` (SD_Freeze), `combust` (SD_Combust), `neutralize` (SD_Neutralize).
- **Emphasis convention:** wrap the surfaced spell word in `*asterisks*`, matching `DD_Tavin`'s existing `*neutralize*`.
- **In-character voice & existing tone** preserved per asset.
- **No code changes.** Pure ScriptableObject text edits, applied by the user in the Unity Editor.
- **UVCS only** for check-in — never git.

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Assets/Data/Dialogues/DD_Lois.asset` | Level 1-2 NPC — freeze-water hint | Modify `rawDialogueText` (new hint; none exists today) |
| `Assets/Data/Dialogues/DD_Ember.asset` | Level 2-1 NPC — combust-obstacle + steam-vent hint | Modify `rawDialogueText` (add environmental hint; existing text is boss-focused) |
| `Assets/Data/Dialogues/DD_Tavin.asset` | Level 3-1 NPC — neutralize-acid hint | **Optional** light traversal tie-in; asset already conveys `*neutralize*` |

---

### Task 1: DD_Lois — freeze-water hint (Level 1-2)

**Files:**
- Modify: `Assets/Data/Dialogues/DD_Lois.asset` (`rawDialogueText`)

**Rationale:** Lois currently says nothing about the water platforms. "freeze" is plain English, so a cautious villager can speak it naturally. The hint is inserted just before her closing "turn back" line. Kaelen's half-question ("Not as it is…?") voices the player's realization diegetically — replacing what would otherwise be a tutorial prompt.

- [ ] **Step 1 — Unity Editor task (user):** Select `Assets/Data/Dialogues/DD_Lois.asset` in the Project window. In the Inspector, replace the entire `Raw Dialogue Text` field with the text below. (Unity re-handles YAML escaping on save.)

```
Lois : "Wait. It's dangerous to go any further. Ever since the sun shattered to pieces strange creatures have been showing up."
Kaelen: "Strange creatures? What do they look like?"
Lois : "They look like amalgamations of beasts and incomplete reactions."
Lois: "Reactions forced into perpetual motion by a force that refuses to let them settle."
Kaelen: "Glimmerlings..."
Lois : "You know what they are?"
Kaelen: "Not exactly, that word just popped into my head for some reason."
Lois: "And mind the waters past the ridge. The current's too quick to wade, and it won't hold your weight — not as it is."
Kaelen: "Not as it is…?"
Lois: "I've watched the cold *freeze* a river still and solid as stone. Whether you've the means to do the same… that's your concern, not mine."
Lois : "Either way, with those things walking around, you'd be better off just turning back"
Kaelen: "Well lucky for me I don't have anywhere to go back to. Don't worry I can take care of myself."
```

- [ ] **Step 2 — Unity Editor task (user):** Save the asset (Ctrl/Cmd+S). Confirm the Inspector shows the three new lines and the word renders as intended in-game.

---

### Task 2: DD_Ember — combust-obstacle + steam-vent hint (Level 2-1)

**Files:**
- Modify: `Assets/Data/Dialogues/DD_Ember.asset` (`rawDialogueText`)

**Rationale:** Ember's existing text is rich on combustion *theme* but speaks only of the Living Furnace boss — nothing about the environmental burnable obstacles or steam vents the player must navigate. She already asks "You understand combustion?", so "combust" is consistent with her voice. **Placement matters:** her retort "Can you? Or will you just unleash more chaos?" is a direct answer to Kaelen's "I can control reactions. With my voice." — those two must stay adjacent. The hint is therefore a single terse line inserted *after* the retort (framed as a grudging warning, fitting her reluctant register), with her existing "You'll know when you're close" kept as the closer.

- [ ] **Step 1 — Unity Editor task (user):** Select `Assets/Data/Dialogues/DD_Ember.asset`. In the Inspector, replace the entire `Raw Dialogue Text` field with:

```
Kaelen: "Another person... alive out here?"
Ember: "Step back. Fire out here doesn't follow the old rules anymore. Since the Cascade, flames don't just warm they rage. They spread where they shouldn't. They refuse to die when you need them to."
Kaelen: "You understand combustion?"
Ember: "I survived it. Before the Cascade, I kept furnaces. Fuel, oxygen, heat I knew how to balance them. But now... the reactions are wild."
Ember: "There's a Theorem ahead. A creature made entirely of uncontrolled combustion. It destroyed the rest of my settlement. It's called the Living Furnace. If you're wise, you'll turn back."
Kaelen: "I can control reactions. With my voice."
Ember: "Can you? Or will you just unleash more chaos? Either way... maybe understanding is what we need now."
Ember: "Then a warning, for what it's worth. The wreckage on that path is soaked with old fuel — one spark and it'll *combust*, whether you mean it to or not. And watch the ground where it vents steam; the heat's still trapped below, and it'll scald you raw."
Ember: "The path ahead is dangerous. You'll know when you're close."
```

- [ ] **Step 2 — Unity Editor task (user):** Save the asset. Confirm Ember's "Can you?..." retort directly follows Kaelen's "I can control reactions" line, and the new warning line sits before the "You'll know when you're close" closer.

---

### Task 3: DD_Tavin — neutralize-acid traversal tie-in (Level 3-1) — OPTIONAL

**Files:**
- Modify: `Assets/Data/Dialogues/DD_Tavin.asset` (`rawDialogueText`)

**Rationale:** Tavin **already** conveys the neutralize mechanic well (`"If you can... *neutralize* it. ... Acid and base... they cancel each other out."`). The only gap is that it's framed around reaching the Corrosion Queen rather than crossing the acid **pools**. This optional one-line insert (after her "being eaten away" line) ties the mechanic to traversal **without** re-stating `*neutralize*`, so the emphasized word stays unique to its existing line. **Skip this task entirely if you judge Tavin already sufficient.**

- [ ] **Step 1 — Unity Editor task (user):** *(Optional)* Select `Assets/Data/Dialogues/DD_Tavin.asset`. In the Inspector, replace the entire `Raw Dialogue Text` field with:

```
Kaelen: "Someone's stranded..."
Tavin: "Please... don't get closer. The ground — it's being eaten away. Everything the acid touches dissolves."
Tavin: "Those pools are death to step in as they are. Half a reaction, that's all acid is — give it its opposite and the ground falls quiet long enough to cross."
Kaelen: "What happened here?"
Tavin: "After the Cascade, acid started pooling everywhere. At first it was okay. But then... a Theorem emerged."
Tavin: "The Corrosion Queen. It spreads acid. It controls it. Everything it touches becomes poisoned."
Kaelen: "Can it be stopped?"
Tavin: "If you can... *neutralize* it. Before the Cascade, I worked in refineries. Acid and base... they cancel each other out. If you understand that, maybe you can reach it. But first, you have to cross this wasteland."
Kaelen: "I understand chemistry. Acids have an opposite."
Tavin: "Bases. Find the right balance. And if you succeed... save us. Please."
```

- [ ] **Step 2 — Unity Editor task (user):** *(Optional)* Save the asset. Confirm only one emphasized `*neutralize*` remains and the new pool line reads naturally.

---

### Task 4: Play Mode verification

**Goal:** Confirm each hint is encountered *before* its puzzle and that the surfaced word is the real incantation. No automated test — this is a manual Play Mode walkthrough.

- [ ] **Step 1 — Unity Editor task (user):** Enter Play Mode in Level 1-2. Walk to Lois, read the dialogue, and confirm: (a) the Lois encounter occurs **before** the first water-platform puzzle in level flow; (b) speaking **"freeze"** into the mic on a water platform freezes it. If Lois is placed *after* the puzzle, flag for a placement fix (out of scope for this plan — note it on DEV-132).

- [ ] **Step 2 — Unity Editor task (user):** Repeat in Level 2-1 for Ember: hint precedes the first burnable obstacle / steam vent, and **"combust"** ignites a burnable obstacle.

- [ ] **Step 3 — Unity Editor task (user):** Repeat in Level 3-1 for Tavin: hint precedes the first acid pool, and **"neutralize"** neutralizes a pool.

- [ ] **Step 4:** Confirm tone/voice of each edited asset still reads in-character and no stray YAML/escaping artifacts appear in the rendered dialogue box.

---

### Task 5: UVCS check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `content(DEV-132): add implicit chemistry-puzzle hints to NPC dialogue`
  - `Assets/Data/Dialogues/DD_Lois.asset`
  - `Assets/Data/Dialogues/DD_Ember.asset`
  - `Assets/Data/Dialogues/DD_Tavin.asset` *(only if Task 3 was performed)*

  > Note: `.asset` metadata is unchanged by a text edit, so the `.meta` files generally do **not** appear in Pending Changes. If a `.meta` does show as modified, stage it alongside its `.asset`.

---

## Self-Review

**Spec coverage:** Lois→freeze (Task 1), Ember→combust+steam (Task 2), Tavin→neutralize (Task 3, optional since already covered), verification of encounter-order + mic cast (Task 4), check-in (Task 5). All AC from DEV-132 mapped except the spell-availability gate, which the user explicitly descoped from this plan.

**Incantation accuracy:** verified against spell assets — `freeze` / `combust` / `neutralize` match `SD_Freeze` / `SD_Combust` / `SD_Neutralize` `spellName` fields exactly.

**Convention:** emphasis via `*asterisks*` matches `DD_Tavin`'s pre-existing `*neutralize*`; no second emphasized word introduced in Tavin.

**Out of scope (per user):** spell-unlock / level-gating verification for `combust` at 2-1; NPC trigger placement changes (only flagged in Task 4 if found wrong).
