# Inspector-Configurable Condition Durations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `inflictsConditionDuration` field to `SpellData` so designers can tune status condition durations per spell in the Unity Inspector, with 0 falling back to the existing hardcoded defaults.

**Architecture:** `SpellData` gains one new int field. `CharacterStats.ApplyStatusCondition` gains an optional `duration` parameter — when > 0 it uses the supplied value; when 0 it falls back to `DefaultDurationFor`. `SpellEffectResolver` passes `spell.inflictsConditionDuration` at the existing call site. No new classes or files.

**Tech Stack:** Unity 6 LTS, C#, Unity Test Framework (Edit Mode tests)

---

## File Map

| File | Change |
|------|--------|
| `Assets/Scripts/Data/SpellData.cs` | Add `inflictsConditionDuration` field |
| `Assets/Scripts/Battle/CharacterStats.cs` | Add optional `duration` param to `ApplyStatusCondition` |
| `Assets/Scripts/Battle/SpellEffectResolver.cs` | Pass `spell.inflictsConditionDuration` at call site |
| `Assets/Tests/Battle/CharacterStatsTests.cs` | Add tests for duration fallback and override |
| `Assets/Tests/Battle/SpellEffectResolverTests.cs` | Add test for duration threading |

---

### Task 1: Add failing tests for `ApplyStatusCondition` duration behaviour

**Files:**
- Modify: `Assets/Tests/Battle/CharacterStatsTests.cs`

- [ ] **Step 1: Open `CharacterStatsTests.cs` and add two failing tests at the bottom of the class**

```csharp
[Test]
public void ApplyStatusCondition_WhenDurationIsZero_UsesDefaultDuration()
{
    var stats = new CharacterStats { MaxHP = 100, MaxMP = 50 };
    stats.Initialize();

    // Frozen default is 1 turn
    stats.ApplyStatusCondition(ChemicalCondition.Frozen, duration: 0);

    Assert.AreEqual(1, stats.ActiveStatusConditions[0].TurnsRemaining);
}

[Test]
public void ApplyStatusCondition_WhenDurationIsNonZero_UsesSuppliedDuration()
{
    var stats = new CharacterStats { MaxHP = 100, MaxMP = 50 };
    stats.Initialize();

    stats.ApplyStatusCondition(ChemicalCondition.Frozen, duration: 3);

    Assert.AreEqual(3, stats.ActiveStatusConditions[0].TurnsRemaining);
}
```

- [ ] **Step 2: Run the tests in Unity Test Runner (Window → General → Test Runner → Edit Mode) and confirm both fail**

Expected: compilation error — `ApplyStatusCondition` has no `duration` parameter yet.

---

### Task 2: Add `duration` parameter to `CharacterStats.ApplyStatusCondition`

**Files:**
- Modify: `Assets/Scripts/Battle/CharacterStats.cs:172-182`

- [ ] **Step 1: Replace the existing `ApplyStatusCondition` method**

Current code (lines 172–182):
```csharp
public void ApplyStatusCondition(ChemicalCondition condition, int baseDamage = 0)
{
    int duration = DefaultDurationFor(condition);
    ActiveStatusConditions.Add(new StatusConditionEntry
    {
        Condition      = condition,
        TurnsRemaining = duration,
        TickCount      = 0,
        BaseDamage     = baseDamage
    });
}
```

Replace with:
```csharp
public void ApplyStatusCondition(ChemicalCondition condition, int baseDamage = 0, int duration = 0)
{
    int effectiveDuration = duration > 0 ? duration : DefaultDurationFor(condition);
    ActiveStatusConditions.Add(new StatusConditionEntry
    {
        Condition      = condition,
        TurnsRemaining = effectiveDuration,
        TickCount      = 0,
        BaseDamage     = baseDamage
    });
}
```

- [ ] **Step 2: Run the two new tests in Unity Test Runner and confirm both pass**

Expected: both `ApplyStatusCondition_WhenDurationIsZero_UsesDefaultDuration` and `ApplyStatusCondition_WhenDurationIsNonZero_UsesSuppliedDuration` → PASS.

- [ ] **Step 3: Run the full Edit Mode test suite and confirm no regressions**

Expected: all pre-existing tests still pass.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Battle/CharacterStats.cs Assets/Tests/Battle/CharacterStatsTests.cs
git commit -m "feat(DEV-??): add optional duration param to ApplyStatusCondition with DefaultDurationFor fallback"
```

---

### Task 3: Add `inflictsConditionDuration` to `SpellData`

**Files:**
- Modify: `Assets/Scripts/Data/SpellData.cs:23-24` (after `inflictsCondition`)

- [ ] **Step 1: Add the new field directly below `inflictsCondition`**

Current block (lines 22–24):
```csharp
[Tooltip("Status condition applied to the spell's primary target after it resolves. None if no condition is inflicted. Spells can never directly apply a material condition via this field.")]
public ChemicalCondition inflictsCondition;
```

Replace with:
```csharp
[Tooltip("Status condition applied to the spell's primary target after it resolves. None if no condition is inflicted. Spells can never directly apply a material condition via this field.")]
public ChemicalCondition inflictsCondition;

[Tooltip("How many turns the inflicted condition lasts. 0 = use the default duration for that condition (Frozen: 1, Evaporating: 2, Burning: 2, Corroded: 3, Crystallized: 2).")]
public int inflictsConditionDuration;
```

---

### Task 4: Add failing test for `SpellEffectResolver` duration threading

**Files:**
- Modify: `Assets/Tests/Battle/SpellEffectResolverTests.cs`

- [ ] **Step 1: Add a failing test that verifies the resolver threads `inflictsConditionDuration` through**

Add inside the existing test class:
```csharp
[Test]
public void Resolve_WhenSpellHasInflictsConditionDuration_AppliesCorrectDuration()
{
    var spell = ScriptableObject.CreateInstance<SpellData>();
    spell.effectType                = SpellEffectType.Damage;
    spell.power                     = 0;
    spell.inflictsCondition         = ChemicalCondition.Frozen;
    spell.inflictsConditionDuration = 4;

    var caster = new CharacterStats { MaxHP = 100, MaxMP = 50, ATK = 10, DEF = 0, SPD = 5 };
    caster.Initialize();

    var target = new CharacterStats { MaxHP = 100, MaxMP = 50, ATK = 10, DEF = 0, SPD = 5 };
    target.Initialize();

    var resolver = new SpellEffectResolver();
    resolver.Resolve(spell, caster, target);

    Assert.AreEqual(4, target.ActiveStatusConditions[0].TurnsRemaining);

    Object.DestroyImmediate(spell);
}
```

- [ ] **Step 2: Run the test in Unity Test Runner and confirm it fails**

Expected: FAIL — `SpellEffectResolver` doesn't pass the duration yet, so the target gets the default Frozen duration (1), not 4. Assert fails: `Expected: 4, But was: 1`.

---

### Task 5: Thread `inflictsConditionDuration` through `SpellEffectResolver`

**Files:**
- Modify: `Assets/Scripts/Battle/SpellEffectResolver.cs:95`

- [ ] **Step 1: Update the `ApplyStatusCondition` call in `Resolve()` to pass the duration**

Current line 95:
```csharp
effectTarget.ApplyStatusCondition(spell.inflictsCondition, baseDamage);
```

Replace with:
```csharp
effectTarget.ApplyStatusCondition(spell.inflictsCondition, baseDamage, spell.inflictsConditionDuration);
```

- [ ] **Step 2: Run all Edit Mode tests in Unity Test Runner and confirm all pass**

Expected: the new resolver test passes; all pre-existing tests still pass.

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/Data/SpellData.cs Assets/Scripts/Battle/SpellEffectResolver.cs Assets/Tests/Battle/SpellEffectResolverTests.cs
git commit -m "feat(DEV-??): add inflictsConditionDuration to SpellData and thread through SpellEffectResolver"
```

---

### Task 6: Verify in Unity Inspector

- [ ] **Step 1: Open Unity Editor and select any spell `.asset` file (e.g. `SD_Freeze`) in the Project window**

Confirm the Inspector shows `Inflicts Condition Duration` field below `Inflicts Condition`, defaulting to `0`.

- [ ] **Step 2: Set `Inflicts Condition Duration` to `3` on `SD_Freeze`, enter Play Mode, and cast Freeze**

Confirm the enemy's Frozen badge shows 3 turns remaining (visible in `ConditionBadgeUI` or via the Unity Debugger on `CharacterStats.ActiveStatusConditions`).

- [ ] **Step 3: Reset the field back to `0` and confirm Freeze reverts to 1-turn default**

- [ ] **Step 4: Commit any `.asset` changes if you intentionally changed a spell's duration**

```
git add Assets/Data/Spells/
git commit -m "chore(DEV-??): set inflictsConditionDuration on spell assets"
```

_(Skip if you left all assets at 0.)_
