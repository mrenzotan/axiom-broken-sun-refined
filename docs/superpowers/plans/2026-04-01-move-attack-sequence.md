# Move-Attack Sequence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the existing step-in nudge with a full move-attack-return sequence: the character plays a directional run animation while physically moving to a target X position, transitions directly into the attack animation, then runs back to their origin and fires `OnAttackSequenceComplete` so `BattleController` can advance the turn precisely.

**Architecture:** `PlayerBattleAnimator` and `EnemyBattleAnimator` each own the full coroutine sequence internally — `TriggerAttack()` is still the single entry point called by `BattleAnimationService`, no changes needed there. `BattleController.CompletePlayerAction` / `CompleteEnemyAction` replace the fixed `WaitForSeconds(_actionDelay)` with a polling loop that exits as soon as `OnAttackSequenceComplete` fires or `_actionDelay` seconds pass, whichever comes first. `_actionDelay` therefore becomes a safety-net timeout rather than a pacing delay. The existing `OnHitFrame` / damage visual pipeline is untouched.

**Tech Stack:** Unity 6 LTS · C# · Unity Animator (bool parameters) · Unity Coroutines

---

## File Map

| Action | Path | Change |
|--------|------|--------|
| **Modify** | `Assets/Scripts/Battle/PlayerBattleAnimator.cs` | Replace step-in with move-attack sequence + `OnAttackSequenceComplete` event |
| **Modify** | `Assets/Scripts/Battle/EnemyBattleAnimator.cs` | Identical change (step direction handled by `_attackPositionX` value, not code) |
| **Modify** | `Assets/Scripts/Battle/BattleController.cs` | Subscribe to sequence-complete events; replace fixed delay with polling loop |

No new `.asmdef` files needed.

---

## Task 1: Replace Step-In with Move-Attack Sequence in PlayerBattleAnimator

**File:** Modify `Assets/Scripts/Battle/PlayerBattleAnimator.cs`

### Why

The current `StepSequence` nudges the sprite 0.5 world units with no animation. This task replaces it with a coroutine that plays the run animation while lerping to `_attackPositionX`, cuts directly to the attack clip on arrival, waits for it to finish, then runs back and fires `OnAttackSequenceComplete`.

- [ ] **Step 1: Read the file**

Read `Assets/Scripts/Battle/PlayerBattleAnimator.cs` before editing.

- [ ] **Step 2: Replace the entire file contents**

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

        [SerializeField]
        [Tooltip("Local-space X position to move to before attacking (toward the enemy).")]
        private float _attackPositionX = 0f;

        [SerializeField]
        [Tooltip("Seconds to travel each leg of the move-attack-return sequence.")]
        private float _moveDuration = 0.3f;

        [SerializeField]
        [Tooltip("Seconds to wait after triggering the attack before running back. Set to match the attack clip length.")]
        private float _attackDuration = 0.5f;

        private static readonly int AttackHash    = Animator.StringToHash("Attack");
        private static readonly int HurtHash      = Animator.StringToHash("Hurt");
        private static readonly int DefeatHash    = Animator.StringToHash("Defeat");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int MoveRightHash = Animator.StringToHash("MoveRight");

        private Vector3 _originalLocalPosition;

        /// <summary>
        /// Fired by Unity Animation Event on the hit frame of the attack clip.
        /// BattleController subscribes to trigger damage visual feedback at the right moment.
        /// </summary>
        public event System.Action OnHitFrame;

        /// <summary>
        /// Fired when the full move → attack → return sequence is complete.
        /// BattleController subscribes to advance the turn at the right moment.
        /// </summary>
        public event System.Action OnAttackSequenceComplete;

        /// <summary>
        /// Called by Unity Animation Event on the attack clip's hit frame.
        /// The method name must match exactly what is set in the Animation Event inspector.
        /// </summary>
        public void AnimEvent_OnHit() => OnHitFrame?.Invoke();

        private void Start()
        {
            _originalLocalPosition = transform.localPosition;
        }

        public void TriggerAttack()  => StartCoroutine(MoveAndAttackSequence());
        public void TriggerHurt()    => _animator.SetTrigger(HurtHash);
        public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);

        private System.Collections.IEnumerator MoveAndAttackSequence()
        {
            // ── Leg 1: Run toward enemy ──────────────────────────────────────
            _animator.SetBool(MoveRightHash, true);
            _animator.SetBool(IsRunningHash, true);

            float elapsed = 0f;
            float startX  = _originalLocalPosition.x;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(startX, _attackPositionX, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = new Vector3(_attackPositionX, _originalLocalPosition.y, _originalLocalPosition.z);

            // ── Attack: direct run → attack transition (no idle gap) ─────────
            _animator.SetTrigger(AttackHash);
            _animator.SetBool(IsRunningHash, false);

            yield return new WaitForSeconds(_attackDuration);

            // ── Leg 2: Run back to origin ────────────────────────────────────
            _animator.SetBool(MoveRightHash, false);
            _animator.SetBool(IsRunningHash, true);

            elapsed = 0f;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(_attackPositionX, _originalLocalPosition.x, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = _originalLocalPosition;
            _animator.SetBool(IsRunningHash, false);

            OnAttackSequenceComplete?.Invoke();
        }
    }
}
```

- [ ] **Step 3: Verify compile — no errors in Unity Editor console**

- [ ] **Step 4: Check in**

Unity Version Control → stage `Assets/Scripts/Battle/PlayerBattleAnimator.cs`

Check in with message: `feat: replace step-in with move-attack sequence in PlayerBattleAnimator`

---

## Task 2: Apply Identical Changes to EnemyBattleAnimator

**File:** Modify `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

### Why

The enemy code is identical. Direction is handled by `_attackPositionX` value configured in the Inspector (enemy's value will be less than their origin X, moving them left toward the player), and by the Ice Slime's existing `localScale.x = -1` flip — `MoveRight = true` on the flipped sprite visually runs left (toward player), and `MoveRight = false` visually runs right (returning). No directional logic differs in code.

- [ ] **Step 1: Read the file**

Read `Assets/Scripts/Battle/EnemyBattleAnimator.cs` before editing.

- [ ] **Step 2: Replace the entire file contents**

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

        [SerializeField]
        [Tooltip("Local-space X position to move to before attacking (toward the player). Should be less than the enemy's origin X.")]
        private float _attackPositionX = 0f;

        [SerializeField]
        [Tooltip("Seconds to travel each leg of the move-attack-return sequence.")]
        private float _moveDuration = 0.3f;

        [SerializeField]
        [Tooltip("Seconds to wait after triggering the attack before running back. Set to match the attack clip length.")]
        private float _attackDuration = 0.5f;

        private static readonly int AttackHash    = Animator.StringToHash("Attack");
        private static readonly int HurtHash      = Animator.StringToHash("Hurt");
        private static readonly int DefeatHash    = Animator.StringToHash("Defeat");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int MoveRightHash = Animator.StringToHash("MoveRight");

        private Vector3 _originalLocalPosition;

        /// <summary>
        /// Fired by Unity Animation Event on the hit frame of the attack clip.
        /// BattleController subscribes to trigger damage visual feedback at the right moment.
        /// </summary>
        public event System.Action OnHitFrame;

        /// <summary>
        /// Fired when the full move → attack → return sequence is complete.
        /// BattleController subscribes to advance the turn at the right moment.
        /// </summary>
        public event System.Action OnAttackSequenceComplete;

        /// <summary>
        /// Called by Unity Animation Event on the attack clip's hit frame.
        /// The method name must match exactly what is set in the Animation Event inspector.
        /// </summary>
        public void AnimEvent_OnHit() => OnHitFrame?.Invoke();

        private void Start()
        {
            _originalLocalPosition = transform.localPosition;
        }

        public void TriggerAttack()  => StartCoroutine(MoveAndAttackSequence());
        public void TriggerHurt()    => _animator.SetTrigger(HurtHash);
        public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);

        private System.Collections.IEnumerator MoveAndAttackSequence()
        {
            // ── Leg 1: Run toward player ─────────────────────────────────────
            // MoveRight = true on a localScale.x = -1 sprite plays the RunRight clip
            // which visually runs LEFT (toward the player). Position lerps left via _attackPositionX.
            _animator.SetBool(MoveRightHash, true);
            _animator.SetBool(IsRunningHash, true);

            float elapsed = 0f;
            float startX  = _originalLocalPosition.x;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(startX, _attackPositionX, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = new Vector3(_attackPositionX, _originalLocalPosition.y, _originalLocalPosition.z);

            // ── Attack: direct run → attack transition (no idle gap) ─────────
            _animator.SetTrigger(AttackHash);
            _animator.SetBool(IsRunningHash, false);

            yield return new WaitForSeconds(_attackDuration);

            // ── Leg 2: Run back to origin ────────────────────────────────────
            // MoveRight = false plays RunLeft clip which on a flipped sprite visually runs RIGHT.
            _animator.SetBool(MoveRightHash, false);
            _animator.SetBool(IsRunningHash, true);

            elapsed = 0f;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(_attackPositionX, _originalLocalPosition.x, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = _originalLocalPosition;
            _animator.SetBool(IsRunningHash, false);

            OnAttackSequenceComplete?.Invoke();
        }
    }
}
```

- [ ] **Step 3: Verify compile — no errors in Unity Editor console**

- [ ] **Step 4: Check in**

Unity Version Control → stage `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

Check in with message: `feat: replace step-in with move-attack sequence in EnemyBattleAnimator`

---

## Task 3: Wire Sequence-Complete Events in BattleController

**File:** Modify `Assets/Scripts/Battle/BattleController.cs`

### Why

`CompletePlayerAction` and `CompleteEnemyAction` currently use a fixed `WaitForSeconds(_actionDelay)`. This task replaces that with a polling loop that exits as soon as `OnAttackSequenceComplete` fires, falling back to `_actionDelay` if the event never arrives (e.g. animators not yet wired). Two named private handler methods are used (not lambdas) so they can be cleanly unsubscribed.

- [ ] **Step 1: Read the file**

Read `Assets/Scripts/Battle/BattleController.cs` before editing.

- [ ] **Step 2: Add two private fields** after `_enemyDamageVisualsFired` in the `// ── Private fields ─` block:

```csharp
private bool _playerSequenceComplete;
private bool _enemySequenceComplete;
```

- [ ] **Step 3: Add two private handler methods** immediately before `OnDestroy()`:

```csharp
private void OnPlayerSequenceComplete() => _playerSequenceComplete = true;
private void OnEnemySequenceComplete()  => _enemySequenceComplete  = true;
```

- [ ] **Step 4: Subscribe in `Initialize()`** — inside the `if (_playerAnimator != null && _enemyAnimator != null)` block, after the existing `_enemyAnimator.OnHitFrame += FireEnemyDamageVisuals;` line:

```csharp
_playerAnimator.OnAttackSequenceComplete += OnPlayerSequenceComplete;
_enemyAnimator.OnAttackSequenceComplete  += OnEnemySequenceComplete;
```

- [ ] **Step 5: Unsubscribe in the teardown guard at the top of `Initialize()`** — inside the `if (_animationService != null)` block, after the existing `_enemyAnimator.OnHitFrame -= FireEnemyDamageVisuals;` line and before `_animationService = null;`:

```csharp
_playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
_enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
```

- [ ] **Step 6: Unsubscribe in `OnDestroy()`** — after the existing `_enemyAnimator.OnHitFrame` unsubscribe lines:

```csharp
if (_playerAnimator != null) _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
if (_enemyAnimator  != null) _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
```

- [ ] **Step 7: Reset sequence flags in `PlayerAttack()`** — add after `_playerDamageVisualsFired = false;`:

```csharp
_playerSequenceComplete = false;
```

- [ ] **Step 8: Reset sequence flag in `ExecuteEnemyTurn()`** — add after `_enemyDamageVisualsFired = false;`:

```csharp
_enemySequenceComplete = false;
```

- [ ] **Step 9: Replace `CompletePlayerAction()`** with the polling-loop version:

```csharp
private System.Collections.IEnumerator CompletePlayerAction(bool targetDefeated)
{
    float elapsed = 0f;
    while (!_playerSequenceComplete && elapsed < _actionDelay)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }
    _playerSequenceComplete = false;
    // Safety net: fires if the animation event was never triggered.
    FirePlayerDamageVisuals();
    _playerDamageVisualsFired = false;
    _isProcessingAction = false;
    _battleManager.OnPlayerActionComplete(targetDefeated);
}
```

- [ ] **Step 10: Replace `CompleteEnemyAction()`** with the polling-loop version:

```csharp
private System.Collections.IEnumerator CompleteEnemyAction(bool targetDefeated)
{
    float elapsed = 0f;
    while (!_enemySequenceComplete && elapsed < _actionDelay)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }
    _enemySequenceComplete = false;
    FireEnemyDamageVisuals();
    _enemyDamageVisualsFired = false;
    _battleManager.OnEnemyActionComplete(targetDefeated);
}
```

- [ ] **Step 11: Verify compile — no errors in Unity Editor console**

- [ ] **Step 12: Check in**

Unity Version Control → stage `Assets/Scripts/Battle/BattleController.cs`

Check in with message: `feat: advance turn on OnAttackSequenceComplete with _actionDelay fallback`

---

> **Unity Editor task (user) — Step 13: Increase `_actionDelay` safety-net timeout**
>
> The sequence takes approximately `_moveDuration * 2 + _attackDuration` seconds. With defaults (0.3 + 0.3 + 0.5 = 1.1 s), the current `_actionDelay = 1f` is too short to serve as a fallback.
>
> Select the **BattleController** GameObject → Inspector → set **Action Delay** to `3`.

---

> **Unity Editor task (user) — Step 14: Create run animation clips**
>
> For each sprite, create two new Animation clips in the Animation window:
>
> **Player** (select player sprite GameObject → Window → Animation → Animation):
> - `playerRunRight` — right-facing run frames, set to loop
> - `playerRunLeft` — left-facing run frames, set to loop
>
> **Enemy / Ice Slime** (select enemy sprite GameObject):
> - `enemyRunRight` — right-facing run frames, set to loop
> - `enemyRunLeft` — left-facing run frames, set to loop
>
> Save all clips.

---

> **Unity Editor task (user) — Step 15: Add Animator states and parameters**
>
> Open both `PlayerBattle.controller` and `IceSlimeBattle.controller` in the Animator window.
>
> **For each controller:**
>
> 1. Add two **Bool** parameters: `IsRunning`, `MoveRight`
>
> 2. Add two new states:
>    - `RunRight` — assign the corresponding `*RunRight` clip, enable **Loop Time**
>    - `RunLeft` — assign the corresponding `*RunLeft` clip, enable **Loop Time**
>
> 3. Add transitions **from Idle** to each run state:
>    - `Idle` → `RunRight`: condition `IsRunning = true` AND `MoveRight = true`, **Has Exit Time off**, transition duration 0
>    - `Idle` → `RunLeft`: condition `IsRunning = true` AND `MoveRight = false`, **Has Exit Time off**, transition duration 0
>
>    > **Note:** Do NOT use Any State → Run transitions. Any State causes a bug where only the first frame of the run clip is displayed. This same issue was encountered and fixed in the Platformer scene. Always transition from Idle instead.
>
> 4. Add exit transitions from each run state to `Idle`:
>    - `RunRight` → Idle: condition `IsRunning = false`, **Has Exit Time off**, transition duration 0
>    - `RunLeft` → Idle: condition `IsRunning = false`, **Has Exit Time off**, transition duration 0
>
> 5. Add **direct run → attack transitions** (this is what allows the seamless cut):
>    - `RunRight` → `Attack`: condition `Attack` trigger fires, **Has Exit Time off**, transition duration 0
>    - `RunLeft` → `Attack`: condition `Attack` trigger fires, **Has Exit Time off**, transition duration 0
>
> Save both controllers.

---

> **Unity Editor task (user) — Step 16: Configure Inspector values**
>
> **PlayerBattleAnimator component** (player sprite GameObject):
> - **Attack Position X** — the local X position the player moves to before attacking. This should be to the right of their resting position and within comfortable range of the enemy. Start with a value around `1.5` to `2.0` and tune in Play Mode.
> - **Move Duration** — `0.3` (seconds per leg)
> - **Attack Duration** — set to match the length of `playerAttackRight` clip in seconds (check Animation window — clip length shown at bottom)
>
> **EnemyBattleAnimator component** (enemy sprite GameObject):
> - **Attack Position X** — the local X position the enemy moves to before attacking. Must be **less than** the enemy's origin local X (moving left toward the player). Start with a value around `-1.5` to `-2.0` from origin and tune in Play Mode.
> - **Move Duration** — `0.3`
> - **Attack Duration** — set to match `enemyAttackRight` clip length

---

> **Play Mode smoke test — Step 17**
>
> Press **Play**, click Attack. Verify:
>
> | Expected | If wrong |
> |---|---|
> | Player runs right toward the enemy before attacking | `_attackPositionX` not set, or RunRight state/transition missing |
> | Run → attack transition is seamless, no idle frame in between | RunRight → Attack transition has exit time enabled — disable it |
> | Damage number / HP bar appears at the hit frame mid-sequence | `AnimEvent_OnHit` not on the attack clip, or OnHitFrame unwired |
> | Player runs left back to their original position after attacking | RunLeft state or transition missing; check `MoveRight = false` condition |
> | Enemy repeats the same sequence on their turn (runs left in, attacks, runs right back) | `_attackPositionX` on enemy is wrong sign — must be less than origin X |
> | No position drift after repeated attacks | `_originalLocalPosition` captured correctly in `Start()` |
> | Spell / Item actions still work (no movement, turn still advances) | `_actionDelay` fallback kicking in — check that `_actionDelay` is set to `3` |

---

## Self-Review

### Spec Coverage

| Requirement | Covered by |
|---|---|
| Run animation plays while moving toward target | Task 1/2: `IsRunning = true` + `MoveRight = true/false` in `MoveAndAttackSequence` |
| Direct run → attack, no idle gap | Task 1/2: `SetTrigger(Attack)` fires while transitioning out of run; Animator transitions in Step 15 |
| Attack at target position, not while approaching | Task 1/2: Attack trigger fires only after lerp completes |
| Run back to origin after attack | Task 1/2: Leg 2 lerp back to `_originalLocalPosition` |
| `OnAttackSequenceComplete` signals turn advance | Task 1/2: event fired at end of coroutine; Task 3: BattleController polls it |
| `_actionDelay` as safety-net fallback | Task 3: polling loop exits on timeout if event never fires |
| Spell / Item unaffected | Task 3: `_playerSequenceComplete` stays false for non-attack actions; timeout fallback handles them |
| Both right and left facing clips | Step 14 (user) + Step 15 Animator setup |
| Enemy direction correct via localScale flip | Task 2: comments explain `MoveRight = true` on flipped sprite = visual left |
| No position drift | `_originalLocalPosition` snapped at end of each leg |

### Placeholder Scan

No TBDs or incomplete steps found.

### Type Consistency

- `OnAttackSequenceComplete` is `System.Action` — matches `OnPlayerSequenceComplete`/`OnEnemySequenceComplete` void handler signatures ✓
- `IsRunningHash`, `MoveRightHash` are `int` — consistent with existing `AttackHash` / `HurtHash` / `DefeatHash` ✓
- `_attackPositionX`, `_moveDuration`, `_attackDuration` are `float` — consistent with `Mathf.Lerp` and `WaitForSeconds` ✓
