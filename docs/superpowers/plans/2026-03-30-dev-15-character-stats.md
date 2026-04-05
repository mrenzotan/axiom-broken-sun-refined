# DEV-15: Stats System — CharacterStats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete `CharacterStats` by adding `Heal`, `SpendMP`, and `RestoreMP` methods with full Edit Mode test coverage.

**Architecture:** `CharacterStats` is a plain C# `[Serializable]` class in `Axiom.Battle` — no MonoBehaviour, no Unity references. All three new methods follow the same clamping pattern as the existing `TakeDamage`. Tests live in Edit Mode (`Assets/Tests/Editor/Battle/CharacterStatsTests.cs`) and use NUnit directly.

**Tech Stack:** C# 9, NUnit via Unity Test Framework (Edit Mode), `Axiom.Battle` asmdef, UVCS for check-ins.

---

## Current State

`CharacterStats.cs` already exists and is partially complete. Do **not** recreate it — only add to it.

**Already implemented and tested:**
- Fields: `MaxHP`, `MaxMP`, `ATK`, `DEF`, `SPD`
- Properties: `CurrentHP`, `CurrentMP` (private set), `IsDefeated`
- Methods: `Initialize()`, `TakeDamage(int amount)`
- Tests: 7 passing tests in `CharacterStatsTests.cs`

**Missing (scope of this ticket):**
- `Heal(int amount)` — restore HP, clamped to `MaxHP`
- `SpendMP(int amount) : bool` — consume MP; returns `false` and does nothing if insufficient
- `RestoreMP(int amount)` — restore MP, clamped to `MaxMP`

---

## File Map

| Action | Path | Change |
|--------|------|--------|
| Modify | `Assets/Scripts/Battle/CharacterStats.cs` | Add `Heal`, `SpendMP`, `RestoreMP` |
| Modify | `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` | Add tests for all three methods |

No new files. No new asmdefs.

---

## Task 1: Add `Heal` with tests (TDD)

**Files:**
- Modify: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- Modify: `Assets/Scripts/Battle/CharacterStats.cs`

### Step 1.1 — Write the failing tests

Append these three tests to `CharacterStatsTests.cs`, inside the class body after the existing `IsDefeated` block:

```csharp
// ---- Heal ----

[Test]
public void Heal_RestoresCurrentHP_ByAmount()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.TakeDamage(40);   // CurrentHP = 60
    stats.Heal(20);
    Assert.AreEqual(80, stats.CurrentHP);
}

[Test]
public void Heal_ClampsToMaxHP_WhenHealExceedsMax()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.TakeDamage(10);   // CurrentHP = 90
    stats.Heal(9999);
    Assert.AreEqual(100, stats.CurrentHP);
}

[Test]
public void Heal_ZeroAmount_LeavesHPUnchanged()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize();
    stats.TakeDamage(30);   // CurrentHP = 70
    stats.Heal(0);
    Assert.AreEqual(70, stats.CurrentHP);
}
```

### Step 1.2 — Run tests and confirm they fail

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All.
> Expected: 3 new tests fail with `CS0117: 'CharacterStats' does not contain a definition for 'Heal'` (compile error) or `NullReferenceException`. The 7 existing tests must still pass.

### Step 1.3 — Implement `Heal`

Add this method to `CharacterStats.cs`, after `TakeDamage`:

```csharp
/// <summary>Restores CurrentHP by <paramref name="amount"/>, clamped to MaxHP.</summary>
public void Heal(int amount)
{
    CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
}
```

### Step 1.4 — Run tests and confirm they pass

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All.
> Expected: All 10 tests pass (7 existing + 3 new Heal tests). Zero failures.

### Step 1.5 — Check in

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage:
> - `Assets/Scripts/Battle/CharacterStats.cs`
> - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
>
> Check in with message: `feat(DEV-15): add Heal method to CharacterStats`

---

## Task 2: Add `SpendMP` with tests (TDD)

**Files:**
- Modify: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- Modify: `Assets/Scripts/Battle/CharacterStats.cs`

### Step 2.1 — Write the failing tests

Append these four tests to `CharacterStatsTests.cs` after the Heal block:

```csharp
// ---- SpendMP ----

[Test]
public void SpendMP_ReducesCurrentMP_WhenSufficientMP()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    bool result = stats.SpendMP(10);
    Assert.IsTrue(result);
    Assert.AreEqual(20, stats.CurrentMP);
}

[Test]
public void SpendMP_ReturnsFalse_WhenInsufficientMP()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    bool result = stats.SpendMP(31);
    Assert.IsFalse(result);
}

[Test]
public void SpendMP_DoesNotReduceMP_WhenInsufficientMP()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    stats.SpendMP(31);
    Assert.AreEqual(30, stats.CurrentMP);
}

[Test]
public void SpendMP_ZeroAmount_ReturnsTrueAndLeavesMPUnchanged()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    bool result = stats.SpendMP(0);
    Assert.IsTrue(result);
    Assert.AreEqual(30, stats.CurrentMP);
}
```

### Step 2.2 — Run tests and confirm they fail

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All.
> Expected: 4 new tests fail with compile error (`SpendMP` not defined). All 10 prior tests still pass.

### Step 2.3 — Implement `SpendMP`

Add this method to `CharacterStats.cs`, after `Heal`:

```csharp
/// <summary>
/// Attempts to spend <paramref name="amount"/> MP.
/// Returns true and deducts MP if sufficient; returns false and leaves MP unchanged if not.
/// </summary>
public bool SpendMP(int amount)
{
    if (CurrentMP < amount) return false;
    CurrentMP -= amount;
    return true;
}
```

### Step 2.4 — Run tests and confirm they pass

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All.
> Expected: All 14 tests pass. Zero failures.

### Step 2.5 — Check in

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage:
> - `Assets/Scripts/Battle/CharacterStats.cs`
> - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
>
> Check in with message: `feat(DEV-15): add SpendMP method to CharacterStats`

---

## Task 3: Add `RestoreMP` with tests (TDD)

**Files:**
- Modify: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- Modify: `Assets/Scripts/Battle/CharacterStats.cs`

### Step 3.1 — Write the failing tests

Append these three tests to `CharacterStatsTests.cs` after the SpendMP block:

```csharp
// ---- RestoreMP ----

[Test]
public void RestoreMP_RestoresCurrentMP_ByAmount()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    stats.SpendMP(20);      // CurrentMP = 10
    stats.RestoreMP(15);
    Assert.AreEqual(25, stats.CurrentMP);
}

[Test]
public void RestoreMP_ClampsToMaxMP_WhenRestoreExceedsMax()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    stats.SpendMP(10);      // CurrentMP = 20
    stats.RestoreMP(9999);
    Assert.AreEqual(30, stats.CurrentMP);
}

[Test]
public void RestoreMP_ZeroAmount_LeavesMPUnchanged()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize();
    stats.SpendMP(10);      // CurrentMP = 20
    stats.RestoreMP(0);
    Assert.AreEqual(20, stats.CurrentMP);
}
```

### Step 3.2 — Run tests and confirm they fail

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All.
> Expected: 3 new tests fail with compile error (`RestoreMP` not defined). All 14 prior tests still pass.

### Step 3.3 — Implement `RestoreMP`

Add this method to `CharacterStats.cs`, after `SpendMP`:

```csharp
/// <summary>Restores CurrentMP by <paramref name="amount"/>, clamped to MaxMP.</summary>
public void RestoreMP(int amount)
{
    CurrentMP = Math.Min(MaxMP, CurrentMP + amount);
}
```

### Step 3.4 — Run tests and confirm they pass

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All.
> Expected: All 17 tests pass. Zero failures.

### Step 3.5 — Check in

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage:
> - `Assets/Scripts/Battle/CharacterStats.cs`
> - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
>
> Check in with message: `feat(DEV-15): add RestoreMP method to CharacterStats`

---

## Final State

After all tasks, `CharacterStats.cs` will look like this in full:

```csharp
using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Serializable plain C# class holding a character's base stats and runtime HP/MP.
    /// No MonoBehaviour — attach as a SerializeField on BattleController to set values in the Inspector.
    /// Call Initialize() before battle begins to reset CurrentHP/CurrentMP to their maximums.
    /// </summary>
    [Serializable]
    public class CharacterStats
    {
        public int MaxHP;
        public int MaxMP;
        public int ATK;
        public int DEF;
        public int SPD;

        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public bool IsDefeated => CurrentHP <= 0;

        /// <summary>Resets CurrentHP and CurrentMP to their maximum values. Call once per battle start.</summary>
        public void Initialize()
        {
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Reduces CurrentHP by <paramref name="amount"/>, clamped to zero.</summary>
        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }

        /// <summary>Restores CurrentHP by <paramref name="amount"/>, clamped to MaxHP.</summary>
        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        }

        /// <summary>
        /// Attempts to spend <paramref name="amount"/> MP.
        /// Returns true and deducts MP if sufficient; returns false and leaves MP unchanged if not.
        /// </summary>
        public bool SpendMP(int amount)
        {
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        /// <summary>Restores CurrentMP by <paramref name="amount"/>, clamped to MaxMP.</summary>
        public void RestoreMP(int amount)
        {
            CurrentMP = Math.Min(MaxMP, CurrentMP + amount);
        }
    }
}
```

And `CharacterStatsTests.cs` will have **17 tests** total across: Initialize (2), TakeDamage (3), IsDefeated (2), Heal (3), SpendMP (4), RestoreMP (3).

---

## Self-Review

**Spec coverage:**
- `MaxHP`, `MaxMP`, `ATK`, `DEF`, `SPD` fields — already existed ✓
- `CurrentHP`, `CurrentMP` runtime state — already existed ✓
- `Initialize()` — already existed ✓
- `TakeDamage()` — already existed ✓
- `Heal()` — Task 1 ✓
- `SpendMP()` — Task 2 ✓
- `RestoreMP()` — Task 3 ✓
- Edit Mode tests for all methods — all tasks ✓

**Placeholder scan:** No TBDs, no "similar to Task N" references. All code blocks are complete.

**Type consistency:** `SpendMP` returns `bool` in Step 2.1 tests and Step 2.3 implementation — consistent. `Heal` and `RestoreMP` are `void` throughout — consistent.
