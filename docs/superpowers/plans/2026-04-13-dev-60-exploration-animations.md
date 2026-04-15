# DEV-60: Exploration Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire existing animation clips into the exploration (platformer) scene — attack animations for the player (with movement lock + deferred scene transition) and idle/move animations for the Ice Slime enemy (driven by patrol state).

**Architecture:** Two independent subsystems share a common pattern: a plain-C# driver class (`PlayerAnimator`, `EnemyAnimator`) owned by a MonoBehaviour coordinator (`PlayerController`, `EnemyController`). Unity Animation Events use a thin bridge MonoBehaviour (`PlayerExplorationAnimator`) on the Animator child to delegate back to the coordinator without coupling the Animator child to game logic. Enemy animation reuse is guaranteed by an `IsMoving` bool parameter contract that each enemy's Animator Controller must honour.

**Tech Stack:** Unity 6 LTS, URP 2D, Unity Animator / Animation Events, Unity 2D Input System, C# plain-class injection pattern.

---

## Scope Check

The spec covers two independent subsystems. They share no runtime state and can be verified independently. A single plan is reasonable here because both are small and the only shared artifact is a commit boundary — but **read the task headers** to understand which subsystem each task belongs to. If you need to pause between subsystems, Tasks 1–8 (Player) and Tasks 9–12 (Enemy) are each a complete, testable unit.

---

## File Map

| Action                | Path                                                                 | Responsibility                                                                    |
| --------------------- | -------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| Modify                | `Assets/Scripts/Platformer/PlayerMovement.cs`                        | Add `SetMovementLocked(bool)`                                                     |
| Modify                | `Assets/Scripts/Platformer/PlayerAnimator.cs`                        | Add `TriggerAttack()`                                                             |
| Modify                | `Assets/Scripts/Platformer/PlayerController.cs`                      | Add `BeginAttack()`, `OnAttackAnimationEnd()`, `_pendingAttackTrigger` field      |
| Create                | `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs`          | Animation Event bridge on Animator child                                          |
| Modify                | `Assets/Scripts/Platformer/PlayerExplorationAttack.cs`               | Replace direct `TriggerAdvantagedBattle()` call with `BeginAttack()`              |
| Modify (Unity Editor) | `Assets/Animations/Player/Player.controller`                         | Add `Attack` trigger, `AttackRight`/`AttackLeft` states, transitions              |
| Modify (Unity Editor) | `Assets/Animations/Player/playerAttackRight.anim`                    | Add Animation Event at last frame → `OnAttackEnd`                                 |
| Modify (Unity Editor) | `Assets/Animations/Player/playerAttackLeft.anim`                     | Add Animation Event at last frame → `OnAttackEnd`                                 |
| Modify (Unity Editor) | Player prefab / scene — Animator child                               | Add `PlayerExplorationAnimator` component                                      |
| Create                | `Assets/Scripts/Platformer/EnemyAnimator.cs`                         | Plain C# driver — sets `IsMoving` from xVelocity                                  |
| Modify                | `Assets/Scripts/Platformer/EnemyController.cs`                       | Add `_animator` SerializeField + `EnemyAnimator` wiring                           |
| Create (Unity Editor) | `Assets/Animations/Enemies/Ice Slime/IceSlimeExploration.controller` | Idle/Move states with `IsMoving` bool                                             |
| Modify (Unity Editor) | `Assets/Prefabs/Enemies/Enemy.prefab`                                | Rename Visual→Animator child, add Animator component, assign controller and field |

---

## Sub-system 1: Player Attack Animation

---

### Task 1: Add `SetMovementLocked` to `PlayerMovement`

**Files:**

- Modify: `Assets/Scripts/Platformer/PlayerMovement.cs`

- [ ] **Step 1: Add the `_movementLocked` field and `SetMovementLocked` method**

  Open `Assets/Scripts/Platformer/PlayerMovement.cs`. After the line declaring `private bool _isGrounded;`, add:

  ```csharp
  private bool _movementLocked;
  ```

  After the `CutJump()` method, add:

  ```csharp
  /// <summary>
  /// Lock or unlock horizontal movement. When locked, Move() applies zero horizontal velocity.
  /// Called by PlayerController during attack animation playback.
  /// </summary>
  public void SetMovementLocked(bool locked)
  {
      _movementLocked = locked;
  }
  ```

  Modify the `Move` method body so it reads:

  ```csharp
  public void Move(float horizontalInput)
  {
      float velocity = _movementLocked ? 0f : horizontalInput * _moveSpeed;
      _rb.linearVelocity = new Vector2(velocity, _rb.linearVelocity.y);
  }
  ```

- [ ] **Step 2: Verify — open Unity Editor, enter Play Mode in the Platformer scene**

  The player should move and jump normally. There is no visual change yet — this step only confirms the project compiles without error. Check the Console tab for any C# compile errors.

- [ ] **Step 3: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `PlayerMovement.cs` → Check in with message:

  ```
  feat(DEV-60): add SetMovementLocked to PlayerMovement
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/PlayerMovement.cs
  git commit -m "feat(DEV-60): add SetMovementLocked to PlayerMovement"
  ```

---

### Task 2: Add `TriggerAttack` to `PlayerAnimator`

**Files:**

- Modify: `Assets/Scripts/Platformer/PlayerAnimator.cs`

- [ ] **Step 1: Add the `Attack` trigger hash and `TriggerAttack` method**

  Open `Assets/Scripts/Platformer/PlayerAnimator.cs`. After the existing `ParamIsFacingRight` line, add:

  ```csharp
  private static readonly int ParamAttack = Animator.StringToHash("Attack");
  ```

  After the `Tick` method, add:

  ```csharp
  /// <summary>
  /// Sets the Attack trigger on the Animator. Called by PlayerController.BeginAttack().
  /// The Animator Controller routes to AttackRight or AttackLeft based on IsFacingRight.
  /// </summary>
  public void TriggerAttack()
  {
      _animator.SetTrigger(ParamAttack);
  }
  ```

- [ ] **Step 2: Verify compile**

  Open Unity Editor — Console must be error-free.

- [ ] **Step 3: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `PlayerAnimator.cs` → Check in with message:

  ```
  feat(DEV-60): add TriggerAttack to PlayerAnimator
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/PlayerAnimator.cs
  git commit -m "feat(DEV-60): add TriggerAttack to PlayerAnimator"
  ```

---

### Task 3: Add `BeginAttack` and `OnAttackAnimationEnd` to `PlayerController`

**Files:**

- Modify: `Assets/Scripts/Platformer/PlayerController.cs`

- [ ] **Step 1: Add the `_pendingAttackTrigger` field**

  Open `Assets/Scripts/Platformer/PlayerController.cs`. After the `private float _moveInput;` field declaration, add:

  ```csharp
  private Axiom.Platformer.ExplorationEnemyCombatTrigger _pendingAttackTrigger;
  ```

- [ ] **Step 2: Add `BeginAttack` and `OnAttackAnimationEnd` methods**

  After the `OnJumpCanceled` method, add:

  ```csharp
  /// <summary>
  /// Initiates the attack sequence: locks movement, triggers the attack animation,
  /// and stores the enemy trigger so the scene transition fires after the clip ends.
  /// Called by PlayerExplorationAttack.
  /// </summary>
  public void BeginAttack(Axiom.Platformer.ExplorationEnemyCombatTrigger pending)
  {
      _movement.SetMovementLocked(true);
      _playerAnimator.TriggerAttack();
      _pendingAttackTrigger = pending;
  }

  /// <summary>
  /// Called by PlayerExplorationAnimator when the attack animation clip ends.
  /// Unlocks movement and triggers the battle scene transition.
  /// </summary>
  public void OnAttackAnimationEnd()
  {
      _movement.SetMovementLocked(false);
      _pendingAttackTrigger?.TriggerAdvantagedBattle();
      _pendingAttackTrigger = null;
  }
  ```

- [ ] **Step 3: Verify compile**

  Open Unity Editor — Console must be error-free.

- [ ] **Step 4: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `PlayerController.cs` → Check in with message:

  ```
  feat(DEV-60): add BeginAttack and OnAttackAnimationEnd to PlayerController
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/PlayerController.cs
  git commit -m "feat(DEV-60): add BeginAttack and OnAttackAnimationEnd to PlayerController"
  ```

---

### Task 4: Create `PlayerExplorationAnimator`

**Files:**

- Create: `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs`

- [ ] **Step 1: Create the script**

  Create `Assets/Scripts/Platformer/PlayerExplorationAnimator.cs` with the following content:

  ```csharp
  using UnityEngine;

  /// <summary>
  /// MonoBehaviour bridge — must live on the same GameObject as the Player's Animator component.
  /// Unity Animation Events fire on MonoBehaviours on the same GameObject as the Animator,
  /// not on the root. This component delegates the event to PlayerController on the parent.
  /// </summary>
  public class PlayerExplorationAnimator : MonoBehaviour
  {
      private PlayerController _controller;

      private void Start()
      {
          _controller = GetComponentInParent<PlayerController>();
          Debug.Assert(_controller != null,
              "PlayerExplorationAnimator: no PlayerController found in parent hierarchy. " +
              "This component must be on the Animator child of the Player root.", this);
      }

      /// <summary>
      /// Called by Animation Event on the last frame of playerAttackLeft.anim and playerAttackRight.anim.
      /// Naming mirrors PlayerBattleAnimator.AnimEvent_OnHit — prefix signals Animation Event origin.
      /// </summary>
      public void AnimEvent_OnAttackEnd()
      {
          _controller?.OnAttackAnimationEnd();
      }
  }
  ```

- [ ] **Step 2: Verify compile**

  Open Unity Editor — Console must be error-free.

- [ ] **Step 3: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `PlayerExplorationAnimator.cs` → Check in with message:

  ```
  feat(DEV-60): create PlayerExplorationAnimator bridge
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/PlayerExplorationAnimator.cs
  git commit -m "feat(DEV-60): create PlayerExplorationAnimator bridge"
  ```

---

### Task 5: Update `PlayerExplorationAttack` to defer via `BeginAttack`

**Files:**

- Modify: `Assets/Scripts/Platformer/PlayerExplorationAttack.cs`

- [ ] **Step 1: Replace `TriggerAdvantagedBattle` call with `BeginAttack`**

  Open `Assets/Scripts/Platformer/PlayerExplorationAttack.cs`. Find the `Update()` method body:

  ```csharp
  var trigger = hit.GetComponent<ExplorationEnemyCombatTrigger>();
  trigger?.TriggerAdvantagedBattle();
  ```

  Replace it with:

  ```csharp
  var trigger = hit.GetComponent<ExplorationEnemyCombatTrigger>();
  if (trigger != null)
      GetComponent<PlayerController>().BeginAttack(trigger);
  ```

- [ ] **Step 2: Verify compile**

  Open Unity Editor — Console must be error-free.

- [ ] **Step 3: Verify Play Mode behaviour**

  Enter Play Mode in the Platformer scene. Press Attack near an enemy. The player should **not** immediately transition to the Battle scene (no controller + animator wiring yet). The Console should show no errors. Movement lock will not be visible until the Animator Controller is wired in Task 6.

- [ ] **Step 4: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `PlayerExplorationAttack.cs` → Check in with message:

  ```
  feat(DEV-60): defer battle trigger through BeginAttack in PlayerExplorationAttack
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/PlayerExplorationAttack.cs
  git commit -m "feat(DEV-60): defer battle trigger through BeginAttack in PlayerExplorationAttack"
  ```

---

### Task 6: Set up Attack states in `Player.controller` (Unity Editor)

**Files:**

- Modify (Unity Editor): `Assets/Animations/Player/Player.controller`

- [ ] **Step 1: Open the Animator Controller**

  In the Project window, navigate to `Assets/Animations/Player/` and double-click `Player.controller` to open the Animator window.

- [ ] **Step 2: Add the `Attack` trigger parameter**

  In the Parameters panel (left side of Animator window), click the `+` button and choose **Trigger**. Name it exactly `Attack`.

- [ ] **Step 3: Create the `AttackRight` state**

  Right-click in an empty area of the Animator graph → **Create State → Empty**. Name it `AttackRight`.
  - In the Inspector for `AttackRight`, set **Motion** to `playerAttackRight` (from `Assets/Animations/Player/playerAttackRight.anim`).
  - Leave **Speed** at `1`.

- [ ] **Step 4: Create the `AttackLeft` state**

  Repeat Step 3 for `AttackLeft`, assigning `playerAttackLeft.anim`.

- [ ] **Step 5: Add transitions from Any State → AttackRight**

  Right-click the **Any State** node → **Make Transition** → click `AttackRight`.
  Select the transition arrow. In the Inspector:
  - **Has Exit Time:** unchecked
  - **Transition Duration:** `0`
  - Add condition: `Attack` (trigger)
  - Add condition: `IsFacingRight` = `true`
  - **Can Transition To Self:** unchecked (prevents re-triggering while already attacking right)

- [ ] **Step 6: Add transitions from Any State → AttackLeft**

  Repeat Step 5 for `AttackLeft`, but set the `IsFacingRight` condition to `false`.

- [ ] **Step 7: Add transition AttackRight → Grounded_R (return)**

  Right-click `AttackRight` → **Make Transition** → click the default blend tree (the entry state with movement). Select the transition:
  - **Has Exit Time:** checked, **Exit Time:** `1.0` (fires at end of clip)
  - **Transition Duration:** `0`
  - No conditions needed.

- [ ] **Step 8: Add transition AttackLeft → Grounded_L (return)**

  Repeat Step 7 from `AttackLeft` to the blend tree.

- [ ] **Step 9: Verify in Play Mode**

  Enter Play Mode. Press Attack near an enemy. The player should freeze horizontally and play the attack animation, then resume movement after the clip ends. The battle scene will **not** trigger yet (Animation Event not set up until Task 7).

---

### Task 7: Add `PlayerExplorationAnimator` to the Animator child (Unity Editor)

**Files:**

- Modify (Unity Editor): Player prefab or Platformer scene — `Animator` child GameObject

> **Why this comes before Task 8:** Animation Events call `AnimEvent_OnAttackEnd` by name on MonoBehaviours on the Animator child. The component must exist on that child before the events are added, otherwise Unity logs "No receiver for Animation Event" warnings during any Play Mode test.

- [ ] **Step 1: Locate the Animator child**

  In the Hierarchy (with Platformer scene open), expand the Player GameObject. Find the child named `Animator` (this is the child that holds the `Animator` component — confirm by selecting it and checking the Inspector).

- [ ] **Step 2: Add the component**

  With the `Animator` child selected, click **Add Component** in the Inspector and search for `PlayerExplorationAnimator`. Add it.

- [ ] **Step 3: If Player is a prefab, apply the change**

  If the Player object in the scene shows prefab overrides (blue bar in Inspector), click **Overrides → Apply All** to persist the component addition to the prefab.

- [ ] **Step 4: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select the Player prefab or scene file → Check in with message:

  ```
  feat(DEV-60): add PlayerExplorationAnimator to Player Animator child
  ```

  **Git (mirror — if tracked):**

  ```
  git commit -m "feat(DEV-60): add PlayerExplorationAnimator to Player Animator child"
  ```

---

### Task 8: Add Animation Events to attack clips (Unity Editor)

**Files:**

- Modify (Unity Editor): `Assets/Animations/Player/playerAttackRight.anim`
- Modify (Unity Editor): `Assets/Animations/Player/playerAttackLeft.anim`

- [ ] **Step 1: Open `playerAttackRight.anim` in the Animation window**

  Select the Player GameObject in the Hierarchy (or open the clip directly). Open **Window → Animation → Animation**. In the clip dropdown, select `playerAttackRight`.

- [ ] **Step 2: Scrub to the last frame**

  Drag the playhead to the very last frame of the clip (the rightmost keyframe).

- [ ] **Step 3: Add an Animation Event at the last frame**

  Click the **Add Event** button (envelope icon in the Animation window toolbar) at the current playhead position.
  In the Inspector under the Animation Event:
  - **Function:** `AnimEvent_OnAttackEnd`
  - Leave all parameters empty.

- [ ] **Step 4: Repeat for `playerAttackLeft.anim`**

  Switch the clip dropdown to `playerAttackLeft`. Repeat Steps 2–3.

- [ ] **Step 5: Full end-to-end verification in Play Mode**

  Enter Play Mode in the Platformer scene. Walk near an enemy and press Attack.

  Expected sequence:
  1. Player freezes horizontally.
  2. Attack animation plays to completion.
  3. `AnimEvent_OnAttackEnd` Animation Event fires.
  4. `PlayerExplorationAnimator.AnimEvent_OnAttackEnd()` is called.
  5. `PlayerController.OnAttackAnimationEnd()` unlocks movement and calls `TriggerAdvantagedBattle()`.
  6. Battle scene loads.

  Console must show no errors. If `PlayerExplorationAnimator` logs its assert, the component is on the wrong GameObject.

- [ ] **Step 6: Save and check in (UVCS + Git)**

  Press **Ctrl+S** (or **Cmd+S** on Mac) to save the scene/asset. Unity will persist the Animation Events in the `.anim` files.

  **UVCS (primary):** Unity Version Control → Pending Changes → select `Player.controller`, `playerAttackRight.anim`, `playerAttackLeft.anim`, and the Player prefab/scene if modified → Check in with message:

  ```
  feat(DEV-60): wire player attack animation — controller states, events, receiver
  ```

  **Git (mirror — if these files are tracked):**

  ```
  git add Assets/Animations/Player/Player.controller
  git add Assets/Animations/Player/playerAttackRight.anim
  git add Assets/Animations/Player/playerAttackLeft.anim
  git commit -m "feat(DEV-60): wire player attack animation — controller states, events, receiver"
  ```

---

## Sub-system 2: Enemy Exploration Animation

---

### Task 9: Create `EnemyAnimator`

**Files:**

- Create: `Assets/Scripts/Platformer/EnemyAnimator.cs`

- [ ] **Step 1: Create the script**

  Create `Assets/Scripts/Platformer/EnemyAnimator.cs` with:

  ```csharp
  using UnityEngine;

  /// <summary>
  /// Plain C# — drives the exploration enemy Animator from patrol velocity each FixedUpdate.
  /// No MonoBehaviour, no Unity lifecycle. Injected into EnemyController.
  ///
  /// Contract: the Animator Controller assigned to this enemy must expose a bool parameter
  /// named exactly "IsMoving". Each exploration enemy supplies its own controller; all must
  /// honour this contract. See IceSlimeExploration.controller for the reference implementation.
  /// </summary>
  public class EnemyAnimator
  {
      private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
      private const float MovingThreshold = 0.01f;

      private readonly Animator _animator;

      public EnemyAnimator(Animator animator)
      {
          Debug.Assert(animator != null,
              "EnemyAnimator: animator is null — Animator component not assigned on the enemy.");
          _animator = animator;
      }

      /// <summary>
      /// Call from EnemyController.FixedUpdate() after computing xVelocity.
      /// Sets IsMoving = true when the absolute horizontal velocity exceeds the threshold.
      /// </summary>
      public void Tick(float xVelocity)
      {
          _animator.SetBool(ParamIsMoving, Mathf.Abs(xVelocity) > MovingThreshold);
      }
  }
  ```

- [ ] **Step 2: Verify compile**

  Open Unity Editor — Console must be error-free.

- [ ] **Step 3: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `EnemyAnimator.cs` → Check in with message:

  ```
  feat(DEV-60): create EnemyAnimator plain C# driver
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/EnemyAnimator.cs
  git commit -m "feat(DEV-60): create EnemyAnimator plain C# driver"
  ```

---

### Task 10: Wire `EnemyAnimator` into `EnemyController`

**Files:**

- Modify: `Assets/Scripts/Platformer/EnemyController.cs`

- [ ] **Step 1: Add the `_animator` serialized field**

  Open `Assets/Scripts/Platformer/EnemyController.cs`. Inside the class body, after the existing `[Header("Visual")]` block with `visualTransform`, add:

  ```csharp
  [SerializeField] private Animator _animator;
  ```

  So the Visual header block now reads:

  ```csharp
  [Header("Visual")]
  [SerializeField] private Transform visualTransform;
  [SerializeField] private Animator _animator;
  ```

- [ ] **Step 2: Add the `_enemyAnimator` field and wire it in `Awake`**

  After the `private float _visualOffsetX;` field, add:

  ```csharp
  private EnemyAnimator _enemyAnimator;
  ```

  At the end of `Awake()`, after the `_behavior = new EnemyPatrolBehavior(...)` block, add:

  ```csharp
  if (_animator != null)
      _enemyAnimator = new EnemyAnimator(_animator);
  ```

- [ ] **Step 3: Call `Tick` in `FixedUpdate`**

  In `FixedUpdate()`, after the line `_rb.linearVelocity = new Vector2(xVel, _rb.linearVelocity.y);`, add:

  ```csharp
  _enemyAnimator?.Tick(xVel);
  ```

- [ ] **Step 4: Update the doc comment**

  Replace the class doc comment to reflect the renamed child:

  ```csharp
  /// <summary>
  /// MonoBehaviour — Unity lifecycle and physics bridge only.
  /// All patrol/aggro/return logic is delegated to EnemyPatrolBehavior.
  ///
  /// Required prefab structure:
  ///   Enemy (root — Rigidbody2D, Collider2D, EnemyController)
  ///   └── Animator (child — SpriteRenderer, Animator, EnemyController sets localScale.x for flipping)
  ///          Never use SpriteRenderer.FlipX — see GAME_PLAN.md §6 Sprite Flipping.
  /// </summary>
  ```

- [ ] **Step 5: Verify compile**

  Open Unity Editor — Console must be error-free.

- [ ] **Step 6: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `EnemyController.cs` → Check in with message:

  ```
  feat(DEV-60): wire EnemyAnimator into EnemyController
  ```

  **Git (mirror):**

  ```
  git add Assets/Scripts/Platformer/EnemyController.cs
  git commit -m "feat(DEV-60): wire EnemyAnimator into EnemyController"
  ```

---

### Task 11: Create `IceSlimeExploration.controller` (Unity Editor)

**Files:**

- Create (Unity Editor): `Assets/Animations/Enemies/Ice Slime/IceSlimeExploration.controller`

- [ ] **Step 1: Create the Animator Controller asset**

  In the Project window, right-click `Assets/Animations/Enemies/Ice Slime/` → **Create → Animator Controller**. Name it `IceSlimeExploration`.

- [ ] **Step 2: Open the controller**

  Double-click `IceSlimeExploration` to open it in the Animator window.

- [ ] **Step 3: Add the `IsMoving` bool parameter**

  In the Parameters panel, click `+` → **Bool**. Name it exactly `IsMoving`.

- [ ] **Step 4: Create the `Idle` state**

  Right-click in the graph → **Create State → Empty**. Name it `Idle`.
  - Set **Motion** to `iceSlimeIdleRight` (from `Assets/Animations/Enemies/Ice Slime/iceSlimeIdleRight.anim`).
  - Right-click `Idle` → **Set as Layer Default State** (it turns orange).

- [ ] **Step 5: Create the `Move` state**

  Right-click → **Create State → Empty**. Name it `Move`.
  - Set **Motion** to `iceSlimeMoveRight`.

- [ ] **Step 6: Add transition Idle → Move**

  Right-click `Idle` → **Make Transition** → click `Move`. Select the transition:
  - **Has Exit Time:** unchecked
  - **Transition Duration:** `0`
  - Add condition: `IsMoving` = `true`

- [ ] **Step 7: Add transition Move → Idle**

  Right-click `Move` → **Make Transition** → click `Idle`. Select the transition:
  - **Has Exit Time:** unchecked
  - **Transition Duration:** `0`
  - Add condition: `IsMoving` = `false`

  Sprite direction (left vs right) is handled by `EnemyController` flipping `visualTransform.localScale.x` — no separate left-facing states are needed.

---

### Task 12: Update `Enemy.prefab` (Unity Editor)

**Files:**

- Modify (Unity Editor): `Assets/Prefabs/Enemies/Enemy.prefab`

- [ ] **Step 1: Open the prefab**

  In the Project window, double-click `Assets/Prefabs/Enemies/Enemy.prefab` to open it in Prefab Editing mode (or select it and click **Open Prefab**).

- [ ] **Step 2: Rename the `Visual` child to `Animator`**

  In the Hierarchy (prefab view), select the child GameObject named `Visual`. In the Inspector, rename it to `Animator`. Press Enter to confirm.

- [ ] **Step 3: Add an `Animator` component to the `Animator` child**

  With the `Animator` child still selected, click **Add Component** → search `Animator` → add the **Animator** component (not `AnimatorOverrideController`).

- [ ] **Step 4: Assign the controller**

  In the `Animator` component Inspector, set the **Controller** field to `IceSlimeExploration` (from `Assets/Animations/Enemies/Ice Slime/IceSlimeExploration.controller`).

- [ ] **Step 5: Assign the `_animator` field on `EnemyController`**

  Select the root `Enemy` prefab GameObject. In the Inspector, find the `EnemyController` component. Drag the `Animator` child GameObject into the **Animator** (`_animator`) field.

  > The `visualTransform` field must still reference the same `Animator` child (it was previously `Visual`). Confirm this field is still populated — if it was serialized by name it may have cleared. Re-assign it to the `Animator` child if needed.

- [ ] **Step 6: Save the prefab**

  Click **Save** in the top-left of the Prefab Editor (or press **Ctrl+S**).

- [ ] **Step 7: End-to-end verification in Play Mode**

  Open the Platformer scene. Enter Play Mode. Observe the Ice Slime enemy:
  - While patrolling: `iceSlimeMoveRight.anim` plays.
  - While idle / at waypoint: `iceSlimeIdleRight.anim` plays.
  - Sprite flips correctly when reversing direction (handled by `EnemyController`).
  - Console shows no errors or missing component warnings.

- [ ] **Step 8: Check in (UVCS + Git)**

  **UVCS (primary):** Unity Version Control → Pending Changes → select `IceSlimeExploration.controller` and `Enemy.prefab` → Check in with message:

  ```
  feat(DEV-60): wire Ice Slime exploration animations — controller and prefab setup
  ```

  **Git (mirror — if these files are tracked):**

  ```
  git add Assets/Animations/Enemies/Ice\ Slime/IceSlimeExploration.controller
  git add Assets/Prefabs/Enemies/Enemy.prefab
  git commit -m "feat(DEV-60): wire Ice Slime exploration animations — controller and prefab setup"
  ```

---

## Self-Review

**Spec coverage check:**

| Spec requirement                                                                          | Task       |
| ----------------------------------------------------------------------------------------- | ---------- |
| `Attack` trigger parameter on Player.controller                                           | Task 6     |
| `AttackRight` / `AttackLeft` states, trigger-driven, no exit-time entry                   | Task 6     |
| Return transitions on clip end                                                            | Task 6     |
| `PlayerExplorationAnimator` on Animator child, delegates to `PlayerController`         | Tasks 4, 7 |
| Animation Events on last frame of both attack clips → `AnimEvent_OnAttackEnd`           | Task 8     |
| `PlayerAnimator.TriggerAttack()`                                                          | Task 2     |
| `PlayerMovement.SetMovementLocked(bool)`                                                  | Task 1     |
| `PlayerController.BeginAttack(pending)` — lock + trigger + store                          | Task 3     |
| `PlayerController.OnAttackAnimationEnd()` — unlock + transition                           | Task 3     |
| `PlayerExplorationAttack` calls `BeginAttack` instead of direct `TriggerAdvantagedBattle` | Task 5     |
| `EnemyAnimator` plain C# with `Tick(xVelocity)`                                           | Task 9     |
| `EnemyController` wired: `_animator` field, `_enemyAnimator` creation and `Tick` call     | Task 10    |
| `IceSlimeExploration.controller` with `IsMoving` bool, Idle/Move states                   | Task 11    |
| Enemy.prefab renamed Visual→Animator, Animator component, controller assigned             | Task 12    |
| Doc comment updated in EnemyController                                                    | Task 10    |

All requirements covered.

**Placeholder scan:** No TBDs, no vague "handle edge cases" statements — every step has exact code or exact Editor click path.

**Type consistency:**

- `_pendingAttackTrigger` declared and used as `Axiom.Platformer.ExplorationEnemyCombatTrigger` throughout Tasks 3 and 5. ✓
- `TriggerAttack()` defined in Task 2, called in Task 3. ✓
- `SetMovementLocked(bool)` defined in Task 1, called in Task 3. ✓
- `OnAttackAnimationEnd()` defined in Task 3, referenced in Task 4's `OnAttackEnd()` and named in Task 7's Animation Event. ✓
- `EnemyAnimator.Tick(float)` defined in Task 9, called in Task 10. ✓
