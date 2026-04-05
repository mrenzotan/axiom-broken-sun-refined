# DEV-17: Battle Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add attack, hurt, and defeat sprite animations for player and enemy in the Battle scene, hooked into the existing `BattleController` event system.

**Architecture:** A plain C# `BattleAnimationService` receives `Action` delegates from two new MonoBehaviours (`PlayerBattleAnimator`, `EnemyBattleAnimator`) and subscribes to existing and new `BattleController` events to trigger the correct animation at the correct moment — keeping all dispatch logic in a testable plain C# class and all Animator API calls in the MonoBehaviours.

**Tech Stack:** Unity 6 LTS · URP 2D · C# · Unity Animator (state machine) · 2D Sprite Animation · NUnit (Edit Mode tests)

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `Assets/Scripts/Battle/BattleAnimationService.cs` | Plain C#. Subscribes to `BattleController` events, dispatches `Action` delegates for player/enemy attack, hurt, defeat. |
| **Create** | `Assets/Scripts/Battle/PlayerBattleAnimator.cs` | MonoBehaviour. Holds player `Animator` ref, exposes `TriggerAttack/Hurt/Defeat()` via cached hash IDs. Lifecycle only. |
| **Create** | `Assets/Scripts/Battle/EnemyBattleAnimator.cs` | MonoBehaviour. Identical pattern for enemy `Animator`. |
| **Modify** | `Assets/Scripts/Battle/BattleController.cs` | Add `OnPlayerActionStarted` / `OnEnemyActionStarted` events. Add `[SerializeField]` refs to the two new MonoBehaviours. Create and wire `BattleAnimationService` in `Initialize()`. |
| **Create** | `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs` | Edit Mode NUnit tests for all `BattleAnimationService` dispatch logic. |

No new `.asmdef` files needed — `Assets/Scripts/Battle/` is already covered by `Axiom.Battle`; tests go in the existing `BattleTests` asmdef at `Assets/Tests/Editor/Battle/`.

---

## Task 1: Implement `BattleAnimationService` (TDD)

**Files:**
- Create: `Assets/Scripts/Battle/BattleAnimationService.cs`
- Create: `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs`

- [ ] **Step 1: Write all failing tests**

Create `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;

public class BattleAnimationServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CharacterStats MakeStats(string name, int hp = 100, int atk = 10, int def = 5, int spd = 5)
    {
        var s = new CharacterStats { Name = name, MaxHP = hp, MaxMP = 0, ATK = atk, DEF = def, SPD = spd };
        s.Initialize();
        return s;
    }

    private static BattleAnimationService MakeService(
        CharacterStats player, CharacterStats enemy,
        System.Action playerAttack = null, System.Action playerHurt = null, System.Action playerDefeat = null,
        System.Action enemyAttack = null,  System.Action enemyHurt = null,  System.Action enemyDefeat = null)
    {
        return new BattleAnimationService(
            player, enemy,
            playerAttack ?? (() => {}), playerHurt ?? (() => {}), playerDefeat ?? (() => {}),
            enemyAttack  ?? (() => {}), enemyHurt  ?? (() => {}), enemyDefeat  ?? (() => {}));
    }

    // ── Attack animations ─────────────────────────────────────────────────────

    [Test]
    public void OnPlayerActionStarted_InvokesPlayerAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerAttack: () => called = true);

        svc.OnPlayerActionStarted();

        Assert.IsTrue(called);
    }

    [Test]
    public void OnPlayerActionStarted_DoesNotInvokeEnemyAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyAttack: () => called = true);

        svc.OnPlayerActionStarted();

        Assert.IsFalse(called);
    }

    [Test]
    public void OnEnemyActionStarted_InvokesEnemyAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyAttack: () => called = true);

        svc.OnEnemyActionStarted();

        Assert.IsTrue(called);
    }

    [Test]
    public void OnEnemyActionStarted_DoesNotInvokePlayerAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerAttack: () => called = true);

        svc.OnEnemyActionStarted();

        Assert.IsFalse(called);
    }

    // ── Hurt animations ───────────────────────────────────────────────────────

    [Test]
    public void OnDamageDealt_TargetIsEnemy_InvokesEnemyHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyHurt: () => called = true);

        svc.OnDamageDealt(enemy, damage: 10, isCrit: false);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnDamageDealt_TargetIsEnemy_DoesNotInvokePlayerHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerHurt: () => called = true);

        svc.OnDamageDealt(enemy, damage: 10, isCrit: false);

        Assert.IsFalse(called);
    }

    [Test]
    public void OnDamageDealt_TargetIsPlayer_InvokesPlayerHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerHurt: () => called = true);

        svc.OnDamageDealt(player, damage: 8, isCrit: false);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnDamageDealt_TargetIsPlayer_DoesNotInvokeEnemyHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyHurt: () => called = true);

        svc.OnDamageDealt(player, damage: 8, isCrit: false);

        Assert.IsFalse(called);
    }

    // ── Defeat animations ─────────────────────────────────────────────────────

    [Test]
    public void OnCharacterDefeated_IsEnemy_InvokesEnemyDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyDefeat: () => called = true);

        svc.OnCharacterDefeated(enemy);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnCharacterDefeated_IsEnemy_DoesNotInvokePlayerDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerDefeat: () => called = true);

        svc.OnCharacterDefeated(enemy);

        Assert.IsFalse(called);
    }

    [Test]
    public void OnCharacterDefeated_IsPlayer_InvokesPlayerDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerDefeat: () => called = true);

        svc.OnCharacterDefeated(player);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnCharacterDefeated_IsPlayer_DoesNotInvokeEnemyDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyDefeat: () => called = true);

        svc.OnCharacterDefeated(player);

        Assert.IsFalse(called);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Unity Editor → Window → General → Test Runner → Edit Mode → Run All
Expected: compile error — `BattleAnimationService` does not exist.

- [ ] **Step 3: Implement `BattleAnimationService`**

Create `Assets/Scripts/Battle/BattleAnimationService.cs`:

```csharp
using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# service that maps BattleController events to character animation delegates.
    /// No Unity API calls — all Animator interaction is handled by the MonoBehaviour adapters
    /// (PlayerBattleAnimator, EnemyBattleAnimator) which inject their trigger methods as Actions.
    /// </summary>
    public class BattleAnimationService
    {
        private readonly CharacterStats _playerStats;
        private readonly CharacterStats _enemyStats;

        private readonly Action _playerAttack;
        private readonly Action _playerHurt;
        private readonly Action _playerDefeat;
        private readonly Action _enemyAttack;
        private readonly Action _enemyHurt;
        private readonly Action _enemyDefeat;

        public BattleAnimationService(
            CharacterStats playerStats,
            CharacterStats enemyStats,
            Action playerAttack,
            Action playerHurt,
            Action playerDefeat,
            Action enemyAttack,
            Action enemyHurt,
            Action enemyDefeat)
        {
            _playerStats  = playerStats;
            _enemyStats   = enemyStats;
            _playerAttack = playerAttack;
            _playerHurt   = playerHurt;
            _playerDefeat = playerDefeat;
            _enemyAttack  = enemyAttack;
            _enemyHurt    = enemyHurt;
            _enemyDefeat  = enemyDefeat;
        }

        /// <summary>Call when the player starts an attack action.</summary>
        public void OnPlayerActionStarted() => _playerAttack?.Invoke();

        /// <summary>Call when the enemy starts an attack action.</summary>
        public void OnEnemyActionStarted() => _enemyAttack?.Invoke();

        /// <summary>
        /// Determines which character was hit and triggers their hurt animation.
        /// Signature matches BattleController.OnDamageDealt.
        /// </summary>
        public void OnDamageDealt(CharacterStats target, int damage, bool isCrit)
        {
            if (target == _playerStats)
                _playerHurt?.Invoke();
            else
                _enemyHurt?.Invoke();
        }

        /// <summary>
        /// Determines which character was defeated and triggers their defeat animation.
        /// Signature matches BattleController.OnCharacterDefeated.
        /// </summary>
        public void OnCharacterDefeated(CharacterStats character)
        {
            if (character == _playerStats)
                _playerDefeat?.Invoke();
            else
                _enemyDefeat?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they all pass**

Unity Editor → Window → General → Test Runner → Edit Mode → Run All
Expected: all 12 `BattleAnimationServiceTests` pass.

- [ ] **Step 5: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/BattleAnimationService.cs`
- `Assets/Scripts/Battle/BattleAnimationService.cs.meta`
- `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs`
- `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs.meta`

Check in with message: `feat: add BattleAnimationService with Edit Mode tests (DEV-17)`

---

## Task 2: Implement `PlayerBattleAnimator` and `EnemyBattleAnimator` MonoBehaviours

**Files:**
- Create: `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- Create: `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

These MonoBehaviours have no testable dispatch logic — they only wrap Animator API calls. No Edit Mode tests needed; correctness is verified in Task 6's Play Mode smoke test.

- [ ] **Step 1: Create `PlayerBattleAnimator`**

Create `Assets/Scripts/Battle/PlayerBattleAnimator.cs`:

```csharp
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour adapter for the player's battle Animator.
    /// Lifecycle only — exposes trigger methods injected into BattleAnimationService as Actions.
    /// </summary>
    public class PlayerBattleAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private static readonly int AttackHash  = Animator.StringToHash("Attack");
        private static readonly int HurtHash    = Animator.StringToHash("Hurt");
        private static readonly int DefeatHash  = Animator.StringToHash("Defeat");

        public void TriggerAttack()  => _animator.SetTrigger(AttackHash);
        public void TriggerHurt()    => _animator.SetTrigger(HurtHash);
        public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);
    }
}
```

- [ ] **Step 2: Create `EnemyBattleAnimator`**

Create `Assets/Scripts/Battle/EnemyBattleAnimator.cs`:

```csharp
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour adapter for the enemy's battle Animator.
    /// Lifecycle only — exposes trigger methods injected into BattleAnimationService as Actions.
    /// </summary>
    public class EnemyBattleAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private static readonly int AttackHash  = Animator.StringToHash("Attack");
        private static readonly int HurtHash    = Animator.StringToHash("Hurt");
        private static readonly int DefeatHash  = Animator.StringToHash("Defeat");

        public void TriggerAttack()  => _animator.SetTrigger(AttackHash);
        public void TriggerHurt()    => _animator.SetTrigger(HurtHash);
        public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);
    }
}
```

- [ ] **Step 3: Verify compile — no errors in Unity Editor console**

- [ ] **Step 4: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- `Assets/Scripts/Battle/PlayerBattleAnimator.cs.meta`
- `Assets/Scripts/Battle/EnemyBattleAnimator.cs`
- `Assets/Scripts/Battle/EnemyBattleAnimator.cs.meta`

Check in with message: `feat: add PlayerBattleAnimator and EnemyBattleAnimator MonoBehaviours (DEV-17)`

---

## Task 3: Extend `BattleController` — add events and wire `BattleAnimationService`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Step 1: Add new `SerializeField` references and events**

In `BattleController.cs`, add the two new serialized fields immediately after the `_battleHUD` field:

```csharp
[SerializeField]
[Tooltip("Attach the PlayerBattleAnimator component from the player GameObject in the Battle scene.")]
private PlayerBattleAnimator _playerAnimator;

[SerializeField]
[Tooltip("Attach the EnemyBattleAnimator component from the enemy GameObject in the Battle scene.")]
private EnemyBattleAnimator _enemyAnimator;
```

Add the two new events in the `// ── UI Events ─` block, after `OnCharacterDefeated`:

```csharp
/// <summary>Fires at the start of the player's attack, before damage is calculated.</summary>
public event System.Action OnPlayerActionStarted;

/// <summary>Fires at the start of the enemy's attack, before damage is calculated.</summary>
public event System.Action OnEnemyActionStarted;
```

Add a private field for the animation service in the `// ── Private fields ─` block:

```csharp
private BattleAnimationService _animationService;
```

- [ ] **Step 2: Wire `BattleAnimationService` inside `Initialize()`**

At the top of `Initialize()`, before `_playerStats.Initialize()`, add the animation service teardown guard (mirrors the existing `_battleManager` guard pattern):

```csharp
if (_animationService != null)
{
    OnPlayerActionStarted  -= _animationService.OnPlayerActionStarted;
    OnEnemyActionStarted   -= _animationService.OnEnemyActionStarted;
    OnDamageDealt          -= _animationService.OnDamageDealt;
    OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
    _animationService = null;
}
```

At the end of `Initialize()`, after `_battleManager.StartBattle(startState)`, add the animation service construction and wiring:

```csharp
if (_playerAnimator != null && _enemyAnimator != null)
{
    _animationService = new BattleAnimationService(
        _playerStats, _enemyStats,
        _playerAnimator.TriggerAttack, _playerAnimator.TriggerHurt, _playerAnimator.TriggerDefeat,
        _enemyAnimator.TriggerAttack,  _enemyAnimator.TriggerHurt,  _enemyAnimator.TriggerDefeat);

    OnPlayerActionStarted  += _animationService.OnPlayerActionStarted;
    OnEnemyActionStarted   += _animationService.OnEnemyActionStarted;
    OnDamageDealt          += _animationService.OnDamageDealt;
    OnCharacterDefeated    += _animationService.OnCharacterDefeated;
}
```

- [ ] **Step 3: Fire events in `PlayerAttack()` and `ExecuteEnemyTurn()`**

In `PlayerAttack()`, add the event fire as the very first line after the two guard clauses:

```csharp
public void PlayerAttack()
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (_isProcessingAction) return;
    _isProcessingAction = true;

    OnPlayerActionStarted?.Invoke();   // ← new line

    AttackResult result = _actionHandler.ExecuteAttack();
    // ... rest unchanged
}
```

In `ExecuteEnemyTurn()`, add the event fire as the first line:

```csharp
private void ExecuteEnemyTurn()
{
    OnEnemyActionStarted?.Invoke();   // ← new line

    AttackResult result = _enemyActionHandler.ExecuteAttack();
    // ... rest unchanged
}
```

- [ ] **Step 4: Extend `OnDestroy()` to unwire the animation service**

Replace the existing `OnDestroy()` with:

```csharp
private void OnDestroy()
{
    if (_battleManager != null)
        _battleManager.OnStateChanged -= HandleStateChanged;

    if (_animationService != null)
    {
        OnPlayerActionStarted  -= _animationService.OnPlayerActionStarted;
        OnEnemyActionStarted   -= _animationService.OnEnemyActionStarted;
        OnDamageDealt          -= _animationService.OnDamageDealt;
        OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
    }
}
```

- [ ] **Step 5: Verify compile — no errors in Unity Editor console**

- [ ] **Step 6: Run all Edit Mode tests to confirm nothing regressed**

Unity Editor → Window → General → Test Runner → Edit Mode → Run All
Expected: all existing tests still pass; 12 `BattleAnimationServiceTests` still pass.

- [ ] **Step 7: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/BattleController.cs`

Check in with message: `feat: wire BattleAnimationService into BattleController, add action-started events (DEV-17)`

---

## Task 4 (Editor — User): Confirm Player Clips and Create Enemy Animation Clips

> **Unity Editor task (user):** All steps are performed in the Unity Editor. Claude does not touch any files here.

Naming convention (match exactly): `{character}{State}{Direction}.anim` — camelCase, no underscores, direction suffix.

**Player clips already exist** in `Assets/Animations/Player/` — no new clips needed for the player:
- `playerAttackRight.anim` → use as the player attack animation
- `playerHurtRight.anim` → use as the player hurt animation
- `playerDeath.anim` → use as the player defeat animation

Only the **enemy clips** need to be created.

- [ ] **Step 1: Create the `Assets/Animations/Enemy/` folder**

In the Project window → right-click `Assets/Animations/` → **Create → Folder** → name it `Enemy`.

- [ ] **Step 2: Import enemy sprite sheets and configure import settings**

Place your enemy sprite sheet files anywhere under `Assets/` (e.g. `Assets/Art/Sprites/`).

For each sprite sheet that contains **multiple frames** (horizontal or grid strip):
1. Select the file in the Project window.
2. In the Inspector: Texture Type → **Sprite (2D and UI)**, Sprite Mode → **Multiple**.
3. Click **Sprite Editor** → **Slice** → Type: **Grid By Cell Size**, pixel dimensions matching one frame.
4. Click **Apply**.

For any **single-frame** sprites:
- Texture Type → **Sprite (2D and UI)**, Sprite Mode → **Single** → **Apply**.

- [ ] **Step 3: Create enemy animation clips**

Open the Animation window (Window → Animation → Animation) with the enemy GameObject selected in the Hierarchy, then drag the sprite frames into the Animation timeline.

Create these three clips and save them to `Assets/Animations/Enemy/`:

| Clip name | Loop | Notes |
|---|---|---|
| `enemyAttackRight` | No | Enemy attack frames |
| `enemyHurtRight` | No | Enemy hurt frames |
| `enemyDeath` | No | Enemy defeat frame(s); freezes on last frame |

For each clip: in the Animation window Inspector panel, ensure **Loop Time is unchecked**. Match FPS to your sprite sheets (commonly 8–12 fps).

- [ ] **Step 4: Check in**

Unity Version Control → Pending Changes → stage any imported enemy sprite PNGs, their `.meta` files, and the three new `.anim` clip files.
Check in with message: `assets: create enemy battle animation clips (DEV-17)`

---

## Task 5 (Editor — User): Create Animator Controllers and Configure State Machines

> **Unity Editor task (user):** All steps are performed in the Unity Editor.

- [ ] **Step 1: Create two Animator Controllers**

In the Project window:
- Right-click `Assets/Animations/Player/` → **Create → Animator Controller** → name it `PlayerBattle`
- Right-click `Assets/Animations/Enemy/` → **Create → Animator Controller** → name it `EnemyBattle`

- [ ] **Step 2: Add Trigger parameters to each controller**

Open each Animator Controller (double-click to open the Animator window).

In the **Parameters** tab, add three **Trigger** parameters to each:
- `Attack`
- `Hurt`
- `Defeat`

- [ ] **Step 3: Add states and assign animation clips**

In the Animator window for **PlayerBattle**:
1. Right-click in the grid → **Create State → Empty** → name it `Idle`. Set it as default (right-click → Set as Layer Default State). Assign `playerIdleRight` clip.
2. Right-click → **Create State → Empty** → name it `Attack`. Assign `playerAttackRight` clip.
3. Right-click → **Create State → Empty** → name it `Hurt`. Assign `playerHurtRight` clip.
4. Right-click → **Create State → Empty** → name it `Defeat`. Assign `playerDeath` clip.

In the Animator window for **EnemyBattle**:
1. Right-click → **Create State → Empty** → name it `Idle`. Set as default. Assign your enemy idle clip if available; otherwise leave Motion empty.
2. Right-click → **Create State → Empty** → name it `Attack`. Assign `enemyAttackRight` clip.
3. Right-click → **Create State → Empty** → name it `Hurt`. Assign `enemyHurtRight` clip.
4. Right-click → **Create State → Empty** → name it `Defeat`. Assign `enemyDeath` clip.

- [ ] **Step 4: Add transitions**

For **each** controller, add these transitions:

| From | To | Condition | Has Exit Time | Transition Duration |
|---|---|---|---|---|
| `Idle` | `Attack` | Trigger `Attack` | No | 0 |
| `Attack` | `Idle` | (none — exit time) | Yes (at 1.0) | 0 |
| `Idle` | `Hurt` | Trigger `Hurt` | No | 0 |
| `Hurt` | `Idle` | (none — exit time) | Yes (at 1.0) | 0 |
| `Any State` | `Defeat` | Trigger `Defeat` | No | 0 |

**How to add a transition:** Right-click the source state → **Make Transition** → click the destination state. Select the transition arrow to configure its settings in the Inspector.

For the `Any State → Defeat` transition: right-click `Any State` → Make Transition → `Defeat`.

- [ ] **Step 5: Check in**

Unity Version Control → Pending Changes → stage the two `.controller` files (`PlayerBattle.controller`, `EnemyBattle.controller`) and their `.meta` files.
Check in with message: `assets: add PlayerBattle and EnemyBattle Animator Controllers with state machines (DEV-17)`

---

## Task 6 (Editor — User): Wire Everything in the Battle Scene and Smoke Test

> **Unity Editor task (user):** All steps are performed in the Unity Editor.

- [ ] **Step 1: Open the Battle scene**

File → Open Scene → `Assets/Scenes/Battle.unity` (or `SampleScene.unity` if Battle scene is not yet a separate file).

- [ ] **Step 2: Set up Animator on the player GameObject**

1. Select the player GameObject in the Hierarchy.
2. In the Inspector, click **Add Component → Animator**.
3. Assign `PlayerBattle` (the Animator Controller asset from `Assets/Animations/Player/`) to the **Controller** field.
4. Click **Add Component** → search for `PlayerBattleAnimator` → add the script component.
5. In the `PlayerBattleAnimator` component, assign the **Animator** field to the Animator component on the same GameObject.

- [ ] **Step 3: Set up Animator on the enemy GameObject**

The enemy uses right-facing sprites flipped via `localScale.x = -1` — do **not** use `SpriteRenderer.FlipX` (see GAME_PLAN.md § Sprite Flipping).

1. Select the enemy's sprite child GameObject in the Hierarchy (the one with the SpriteRenderer).
2. In the Inspector, set **Scale X to -1** on the Transform. Set it here in the scene — never via code or animation.
3. Add Component → **Animator** to the same sprite child GameObject.
4. Assign `EnemyBattle` (from `Assets/Animations/Enemy/`) to the **Controller** field.
5. Add Component → search for `EnemyBattleAnimator` → add the script component.
6. In the `EnemyBattleAnimator` component, assign the **Animator** field to the Animator component on the same GameObject.

- [ ] **Step 4: Wire serialized fields on `BattleController`**

Select the GameObject holding the `BattleController` component.
In the Inspector:
- Assign the **Player Animator** field → drag the player GameObject (or the `PlayerBattleAnimator` component).
- Assign the **Enemy Animator** field → drag the enemy GameObject (or the `EnemyBattleAnimator` component).

- [ ] **Step 5: Save the scene**

File → Save (Cmd/Ctrl+S).

- [ ] **Step 6: Play Mode smoke test**

Press **Play**.

Verify the following in sequence:

| Action | Expected visual |
|---|---|
| Click **Attack** in the action menu | Player plays attack animation; enemy plays hurt animation |
| Wait for enemy turn | Enemy plays attack animation; player plays hurt animation |
| Defeat the enemy (click Attack repeatedly until enemy HP = 0) | Enemy plays defeat animation and stays in that pose |
| Start a new battle with a Surprised start state (set in Inspector before Play) | Enemy attacks first; player plays hurt animation |
| Deplete player HP to 0 | Player plays defeat animation |

If any animation does not play:
- Check Unity Console for `NullReferenceException` — the `_animator` reference on the MonoBehaviour may not be assigned.
- Check the Animator window (Window → Animation → Animator) while in Play Mode to see which state is active and whether triggers are being received.

- [ ] **Step 7: Check in**

Unity Version Control → Pending Changes → stage all modified scene file(s) and any `.meta` files.
Check in with message: `feat: wire battle animations in scene, all states verified in Play Mode (DEV-17)`

---

## Self-Review

### Spec Coverage Check

| Requirement (from GAME_PLAN.md Phase 2) | Covered by |
|---|---|
| Attack animation — player | Task 1 (service), Task 2 (MonoBehaviour), Task 3 (event), Tasks 4–6 (Editor) |
| Attack animation — enemy | Same |
| Hurt animation — player | Task 1 (service), Task 2, Task 3 (OnDamageDealt), Tasks 4–6 |
| Hurt animation — enemy | Same |
| Defeat animation — player | Task 1 (service), Task 2, Task 3 (OnCharacterDefeated), Tasks 4–6 |
| Defeat animation — enemy | Same |
| Sprite assets imported and animation clips created | Task 4 (Editor) |
| Architecture: MonoBehaviour lifecycle only, logic in plain C# | `BattleAnimationService` (plain C#) + two MonoBehaviour adapters |
| Architecture: no premature abstraction | No shared base class or interface; `PlayerBattleAnimator` and `EnemyBattleAnimator` are separate concrete classes |
| Architecture: Axiom.Battle namespace | All new classes use `namespace Axiom.Battle` |
| Architecture: existing `Axiom.Battle` asmdef covers new scripts | Confirmed — no new asmdef needed |
| Edit Mode tests for plain C# logic | 12 tests in `BattleAnimationServiceTests.cs` |

### Placeholder Scan

No TBDs, TODOs, or unimplemented steps found. All code blocks are complete and compilable.

### Type Consistency Check

- `BattleAnimationService` constructor: `CharacterStats, CharacterStats, Action×6` — matches test helper `MakeService()` exactly.
- `OnDamageDealt(CharacterStats target, int damage, bool isCrit)` — matches `BattleController.OnDamageDealt` event signature exactly.
- `OnCharacterDefeated(CharacterStats)` — matches `BattleController.OnCharacterDefeated` signature exactly.
- `PlayerBattleAnimator.TriggerAttack/Hurt/Defeat()` — matches delegate `Action` (no parameters) passed in Task 3 Step 2 (`_playerAnimator.TriggerAttack`).
- `Animator.StringToHash("Attack/Hurt/Defeat")` — matches Trigger parameter names set up in Task 5 Step 2.