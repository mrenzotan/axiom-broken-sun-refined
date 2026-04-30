# One-Way Platforms Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for the Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Add Dead Cells–style one-way platforms (jump up through, land on top, Down + Jump to drop through) as a system-wide platformer mechanic on the existing `PlayerMovement` / `PlayerController` pair, plus the project-level layer + per-scene tilemap structure that platforms paint onto.

**Architecture:** New `OneWayPlatform` user layer; new sibling `Tilemap_OneWayPlatforms` GameObject under each scene's `Grid` (with `TilemapCollider2D` + `Rigidbody2D Static` + `PlatformEffector2D`); drop-through logic added in-place to `PlayerMovement` (fields + methods, no new class) and triggered from `PlayerController.OnJumpPerformed` by sampling `Move.y` at jump time. Drop-through toggles `Physics2D.IgnoreLayerCollision(player, oneWay)` for a fixed 0.2 s window; cleanup hooks in `OnDisable` / `OnDestroy` guarantee the global state never leaks across scene transitions or domain reload.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, Mono scripting backend, New Input System, 2D Tilemap + `PlatformEffector2D`, Unity Version Control (UVCS) for check-ins. No new tests — see "No Edit Mode tests" note below.

**Spec:** [`docs/superpowers/specs/2026-04-28-one-way-platforms-design.md`](../specs/2026-04-28-one-way-platforms-design.md) — source of truth for behavior, field shape, edge cases, and accepted limitations. This plan implements that spec.

**No Edit Mode tests:** The spec explicitly opts out of unit tests for drop-through because the logic is entangled with global `Physics2D.IgnoreLayerCollision` state. Verification is the manual scene smoke test in Task 6, which covers the 8 pass/fail checks listed in the spec. Do not add edit-mode tests for `TryDropThrough` / `ResetDropThrough` — that is a deliberate scope decision, not an oversight.

**Project conventions to honor:**

- **MonoBehaviours own Unity lifecycle only**; logic lives in plain C# classes (already true here — `PlayerMovement` is plain C#, `PlayerController` is the MonoBehaviour shell).
- **No new singletons.** `PlayerController` resolves `LayerMask.NameToLayer("OneWayPlatform")` once in `Awake()` and passes it to `PlayerMovement`.
- **No premature abstraction.** No new files, no `IDropThroughHandler` interface — the spec is explicit: ~30 LOC addition on the existing class. Do not extract.
- **No Co-Authored-By footer in check-in messages.** Use the project format `<type>: <short description>` (or `<type>(DEV-###): <desc>` if a Jira ticket has been assigned for this work — none is assigned in the spec, so plain `<type>: <desc>` is correct here).
- **UVCS is the source of truth** for all check-ins (code, scenes, prefabs, project settings); the GitHub mirror is scripts-only and updates automatically.

---

## File / Asset Structure

| File / Asset | Action | Responsibility |
|---|---|---|
| `ProjectSettings/TagManager.asset` | **Modified (Editor)** | Adds the `OneWayPlatform` User Layer entry. |
| `Assets/Scripts/Platformer/PlayerMovement.cs` | **Modified** | Adds `_oneWayLayer`, `_oneWayLayerIndex`, `_playerLayerIndex`, `_dropThroughDuration`, `_dropThroughTimer`, `_dropThroughActive` fields; new public `TryDropThrough()`, `ResetDropThrough()`, `IsMovementLocked` members; constructor + `UpdateConfig()` signature changes; `Tick()` decrements the drop-through timer and restores collision when it expires. |
| `Assets/Scripts/Platformer/PlayerController.cs` | **Modified** | Adds `[Header("Drop-Through")]` Inspector fields (`oneWayPlatformLayer` LayerMask, `dropThroughDuration` float); `Awake()` resolves the player + one-way layer indices and threads new params into the `PlayerMovement` constructor; `Update()` threads them into `UpdateConfig()`; `OnJumpPerformed()` samples `Move.y` and routes to drop-through when `move.y < -0.5f` and movement isn't locked; `OnDisable()` and `OnDestroy()` call `ResetDropThrough()`. |
| Player prefab (path discovered in Task 4 Step 1 — likely `Assets/Prefabs/Player/Player.prefab` or scene-only inside `Platformer.unity`) | **Modified (Editor)** | Adds `OneWayPlatform` to the existing `Ground Layer` mask; sets the new `One Way Platform Layer` field to `OneWayPlatform`; leaves `Drop Through Duration` at default `0.2`. |
| `Assets/Scenes/Platformer.unity` | **Modified (Editor)** | Adds sibling `Tilemap_OneWayPlatforms` GameObject under `Grid` with `TilemapCollider2D` (Used By Composite = true, Composite Operation = Merge), `Rigidbody2D` (Body Type = Static), `CompositeCollider2D` (Geometry Type = Polygons, Used By Effector = true) — matches the project convention used by `Tilemap_Ground` — and `PlatformEffector2D` (Use One Way = true, Surface Arc = 180°, Use Side Friction = false, Use Side Bounce = false); paints a small handful of test tiles to enable the manual smoke test. |
| `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` | **Modified (docs)** | Adds a one-line note that platform-palette tiles in Snow Mountain are now painted onto `Tilemap_OneWayPlatforms` rather than the ground tilemap. |

**Out of scope (do not touch):**

- `Assets/Scenes/Level_1-1.unity` — no tile repainting. The Player prefab wiring change applies globally and is enough for now.
- Tutorial prompt copy / Level_1-2 trigger placement — owned by the Level_1-2 ticket, not this plan.
- Any new animation states for drop-through.
- The `Crouch` Input Action — leave it alone; the spec reuses `Move.y` instead.

---

## Task 1: Add the `OneWayPlatform` User Layer

**Why first:** `LayerMask.NameToLayer("OneWayPlatform")` in `PlayerController.Awake()` returns `-1` if the layer doesn't exist, which would cause `Physics2D.IgnoreLayerCollision(-1, ...)` to throw at runtime. Defining the layer up front is cheap and safe.

**Files:**

- Modify (Editor): `ProjectSettings/TagManager.asset`

- [ ] **Step 1: Add the layer in the Unity Editor**

> **Unity Editor task (user):** In the Unity Editor, open **Edit → Project Settings → Tags and Layers**. Expand the **Layers** section. Find the first empty **User Layer** slot (typically `User Layer 6` or higher — do not overwrite Unity's reserved slots `0` Default through `5` UI). Type **`OneWayPlatform`** (exact spelling, exact casing — case-sensitive matching is used by `LayerMask.NameToLayer`). Press Enter. Close Project Settings.

- [ ] **Step 2: Verify the layer is present**

> **Unity Editor task (user):** Reopen **Edit → Project Settings → Tags and Layers → Layers**. Confirm the new entry reads exactly `OneWayPlatform` and note the slot index (you will need it visually, but the code resolves it by name so the slot number itself doesn't matter).

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `chore: add OneWayPlatform user layer`

- `ProjectSettings/TagManager.asset`

---

## Task 2: Add drop-through state and behavior to `PlayerMovement` and `PlayerController`

**Why combined into one task:** the `PlayerMovement` constructor and `UpdateConfig()` signatures both change, so `PlayerController` cannot compile against the new `PlayerMovement` until `PlayerController` is updated in the same commit. Splitting would leave a broken-build commit between them.

**Files:**

- Modify: `Assets/Scripts/Platformer/PlayerMovement.cs`
- Modify: `Assets/Scripts/Platformer/PlayerController.cs`

- [ ] **Step 1: Add new private fields to `PlayerMovement`**

Open `Assets/Scripts/Platformer/PlayerMovement.cs`. Find the existing private-field block ending with `private bool _movementLocked;` (around line 24). **Add** the following new fields immediately after `_movementLocked`:

```csharp
    // Drop-through state — one-way platforms.
    private LayerMask _oneWayLayer;
    private int _oneWayLayerIndex;
    private readonly int _playerLayerIndex;
    private float _dropThroughDuration;
    private float _dropThroughTimer;
    private bool _dropThroughActive;
```

- [ ] **Step 2: Expose `IsMovementLocked` as a read-only property**

In `Assets/Scripts/Platformer/PlayerMovement.cs`, find the existing public-property block:

```csharp
    public bool IsGrounded => _isGrounded;
    public float VelocityY => _rb.linearVelocity.y;
```

**Add** the new property immediately after `VelocityY`:

```csharp
    public bool IsMovementLocked => _movementLocked;
```

- [ ] **Step 3: Update the `PlayerMovement` constructor signature**

Replace the entire existing constructor (the one starting `public PlayerMovement(` around line 29 and ending at the closing brace around line 50) with:

```csharp
    public PlayerMovement(
        Rigidbody2D rb,
        Transform groundCheck,
        LayerMask groundLayer,
        LayerMask oneWayLayer,
        int playerLayerIndex,
        float moveSpeed,
        float jumpForce,
        float coyoteTime,
        float jumpBufferTime,
        float fallGravityMultiplier,
        float groundCheckRadius,
        float dropThroughDuration)
    {
        _rb = rb;
        _groundCheck = groundCheck;
        _groundLayer = groundLayer;
        _oneWayLayer = oneWayLayer;
        _oneWayLayerIndex = LayerMaskToSingleIndex(oneWayLayer);
        _playerLayerIndex = playerLayerIndex;
        _moveSpeed = moveSpeed;
        _jumpForce = jumpForce;
        _coyoteTime = coyoteTime;
        _jumpBufferTime = jumpBufferTime;
        _fallGravityMultiplier = fallGravityMultiplier;
        _groundCheckRadius = groundCheckRadius;
        _dropThroughDuration = dropThroughDuration;
        _defaultGravityScale = rb.gravityScale;
    }

    /// <summary>
    /// Converts a single-layer LayerMask to its layer index. Returns -1 for an empty mask.
    /// Multi-layer masks return the lowest-bit layer; the OneWayPlatform mask is expected
    /// to contain exactly one layer in production wiring.
    /// </summary>
    private static int LayerMaskToSingleIndex(LayerMask mask)
    {
        int value = mask.value;
        if (value == 0) return -1;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0) return i;
        }
        return -1;
    }
```

- [ ] **Step 4: Update `UpdateConfig` signature and body**

> **Spec deviation note:** the spec lists `playerLayerIndex` among the new `UpdateConfig` params. This plan deliberately omits it because `gameObject.layer` does not change at runtime — passing it every frame would be dead work. `_playerLayerIndex` is constructor-only and `readonly`. The two runtime-tweakable params (`oneWayLayer` mask and `dropThroughDuration`) remain in `UpdateConfig` per spec intent.


Replace the entire existing `UpdateConfig` method (around lines 53–64) with:

```csharp
    /// <summary>Syncs Inspector-tweakable values each frame. Called from PlayerController.Update().</summary>
    public void UpdateConfig(
        LayerMask oneWayLayer,
        float moveSpeed, float jumpForce,
        float coyoteTime, float jumpBufferTime,
        float fallGravityMultiplier, float groundCheckRadius,
        float dropThroughDuration)
    {
        _oneWayLayer = oneWayLayer;
        _oneWayLayerIndex = LayerMaskToSingleIndex(oneWayLayer);
        _moveSpeed = moveSpeed;
        _jumpForce = jumpForce;
        _coyoteTime = coyoteTime;
        _jumpBufferTime = jumpBufferTime;
        _fallGravityMultiplier = fallGravityMultiplier;
        _groundCheckRadius = groundCheckRadius;
        _dropThroughDuration = dropThroughDuration;
    }
```

- [ ] **Step 5: Add `TryDropThrough` and `ResetDropThrough` methods**

In `Assets/Scripts/Platformer/PlayerMovement.cs`, find the existing `SetMovementLocked` method (around lines 101–104). **Add** the following two methods immediately after `SetMovementLocked`:

```csharp
    /// <summary>
    /// Attempts to drop through a one-way platform under the player's feet.
    /// Preconditions: grounded, not movement-locked, and the ground-check overlap
    /// finds a collider on the OneWayPlatform layer.
    /// On success: ignores Player↔OneWayPlatform collisions for _dropThroughDuration
    /// seconds, cancels coyote time so the player cannot immediately jump back up,
    /// and returns true. Otherwise returns false (caller may treat the input as a
    /// normal jump).
    /// </summary>
    public bool TryDropThrough()
    {
        if (!_isGrounded) return false;
        if (_movementLocked) return false;
        if (_oneWayLayerIndex < 0) return false;
        if (_playerLayerIndex < 0) return false;

        bool overOneWay = Physics2D.OverlapCircle(
            _groundCheck.position, _groundCheckRadius, _oneWayLayer);
        if (!overOneWay) return false;

        Physics2D.IgnoreLayerCollision(_playerLayerIndex, _oneWayLayerIndex, true);
        _dropThroughActive = true;
        _dropThroughTimer = _dropThroughDuration;
        _coyoteTimeCounter = 0f;
        return true;
    }

    /// <summary>
    /// Force-restores Player↔OneWayPlatform collision and clears drop-through state.
    /// Called from PlayerController.OnDisable and OnDestroy so the global
    /// IgnoreLayerCollision state never leaks across scene transitions, player
    /// destruction, or domain reload.
    /// </summary>
    public void ResetDropThrough()
    {
        if (_oneWayLayerIndex >= 0 && _playerLayerIndex >= 0)
        {
            Physics2D.IgnoreLayerCollision(_playerLayerIndex, _oneWayLayerIndex, false);
        }
        _dropThroughActive = false;
        _dropThroughTimer = 0f;
    }
```

- [ ] **Step 6: Decrement the drop-through timer in `Tick` and suppress coyote refill while drop-through is active**

In `Assets/Scripts/Platformer/PlayerMovement.cs`, replace the existing `Tick` method (around lines 67–73):

```csharp
    public void Tick(float deltaTime)
    {
        CheckGrounded();
        UpdateCoyoteTime(deltaTime);
        UpdateJumpBuffer(deltaTime);
        ApplyFallGravity();
    }
```

with:

```csharp
    public void Tick(float deltaTime)
    {
        CheckGrounded();
        UpdateCoyoteTime(deltaTime);
        UpdateJumpBuffer(deltaTime);
        UpdateDropThrough(deltaTime);
        ApplyFallGravity();
    }
```

Then **replace** the existing `UpdateCoyoteTime` method (around lines 128–134):

```csharp
    private void UpdateCoyoteTime(float deltaTime)
    {
        if (_isGrounded)
            _coyoteTimeCounter = _coyoteTime;
        else
            _coyoteTimeCounter -= deltaTime;
    }
```

with the drop-through-aware version:

```csharp
    private void UpdateCoyoteTime(float deltaTime)
    {
        // While drop-through is active we are still geometrically inside the
        // platform's collider AABB for the first few frames of the fall.
        // Physics2D.OverlapCircle (used by CheckGrounded) does NOT honor
        // IgnoreLayerCollision — it returns hits purely from the layer mask —
        // so _isGrounded would stay true and coyote would refill to its full
        // window, allowing the player to re-jump back onto the platform they
        // just dropped from. Suppress the refill until the drop-through window
        // closes. Spec smoke-test 6 (verification step) depends on this.
        if (_isGrounded && !_dropThroughActive)
            _coyoteTimeCounter = _coyoteTime;
        else
            _coyoteTimeCounter -= deltaTime;
    }
```

Then **add** the new `UpdateDropThrough` method immediately after `UpdateJumpBuffer` (around lines 136–140):

```csharp
    private void UpdateDropThrough(float deltaTime)
    {
        if (!_dropThroughActive) return;
        _dropThroughTimer -= deltaTime;
        if (_dropThroughTimer <= 0f)
        {
            if (_oneWayLayerIndex >= 0 && _playerLayerIndex >= 0)
            {
                Physics2D.IgnoreLayerCollision(_playerLayerIndex, _oneWayLayerIndex, false);
            }
            _dropThroughActive = false;
            _dropThroughTimer = 0f;
        }
    }
```

- [ ] **Step 7: Add Inspector fields to `PlayerController`**

Open `Assets/Scripts/Platformer/PlayerController.cs`. Find the existing `[Header("Ground Detection")]` block (around lines 22–25). **Add** a new header block immediately after the `groundCheckRadius` field:

```csharp
    [Header("Drop-Through")]
    [SerializeField] private LayerMask oneWayPlatformLayer;
    [SerializeField] private float dropThroughDuration = 0.2f;
```

- [ ] **Step 8: Thread new params through `Awake`**

In `Assets/Scripts/Platformer/PlayerController.cs`, find the existing `_movement = new PlayerMovement(...)` call inside `Awake` (around lines 46–50). Replace it with:

```csharp
        int playerLayerIndex = gameObject.layer;
        _movement = new PlayerMovement(
            _rb, groundCheck, groundLayer,
            oneWayPlatformLayer, playerLayerIndex,
            moveSpeed, jumpForce,
            coyoteTime, jumpBufferTime,
            fallGravityMultiplier, groundCheckRadius,
            dropThroughDuration);
```

- [ ] **Step 9: Thread new params through `UpdateConfig` in `Update`**

In `Assets/Scripts/Platformer/PlayerController.cs`, find the existing `_movement.UpdateConfig(...)` call inside `Update` (around line 127). Replace it with:

```csharp
        _movement.UpdateConfig(
            oneWayPlatformLayer,
            moveSpeed, jumpForce, coyoteTime, jumpBufferTime,
            fallGravityMultiplier, groundCheckRadius,
            dropThroughDuration);
```

- [ ] **Step 10: Route Down + Jump to drop-through in `OnJumpPerformed`**

In `Assets/Scripts/Platformer/PlayerController.cs`, replace the existing `OnJumpPerformed` method (around lines 138–141):

```csharp
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        _movement.BufferJump();
    }
```

with:

```csharp
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        // Sample Move.y at the moment Jump is pressed. If the player is holding
        // Down (move.y < -0.5) and movement isn't locked, attempt drop-through
        // instead of buffering a jump. The lock check is required because the
        // attack-lock path leaves Jump enabled (only the tutorial-lock path
        // disables Jump outright).
        Vector2 move = _input.Player.Move.ReadValue<Vector2>();
        if (move.y < -0.5f && !_movement.IsMovementLocked)
        {
            if (_movement.TryDropThrough()) return;
        }
        _movement.BufferJump();
    }
```

- [ ] **Step 11: Reset drop-through state on disable / destroy**

In `Assets/Scripts/Platformer/PlayerController.cs`, replace the existing `OnDisable` method (around lines 68–73):

```csharp
    private void OnDisable()
    {
        _input.Player.Jump.performed -= OnJumpPerformed;
        _input.Player.Jump.canceled -= OnJumpCanceled;
        _input.Player.Disable();
    }
```

with:

```csharp
    private void OnDisable()
    {
        _input.Player.Jump.performed -= OnJumpPerformed;
        _input.Player.Jump.canceled -= OnJumpCanceled;
        _input.Player.Disable();
        _movement?.ResetDropThrough();
    }
```

Then replace the existing `OnDestroy` method (around lines 118–122):

```csharp
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSceneReady -= InitializeFromTransition;
    }
```

with:

```csharp
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSceneReady -= InitializeFromTransition;
        _movement?.ResetDropThrough();
    }
```

- [ ] **Step 12: Verify compile in the Editor**

> **Unity Editor task (user):** Return to the Unity Editor and wait for the recompile. Open the Console (Window → General → Console) and confirm there are no compile errors. Expected: clean compile, no warnings introduced by these changes.

- [ ] **Step 13: Check in via UVCS**

Unity Version Control → Pending Changes → stage the two files below → Check in with message: `feat: add one-way platform drop-through to PlayerMovement`

- `Assets/Scripts/Platformer/PlayerMovement.cs`
- `Assets/Scripts/Platformer/PlayerController.cs`

---

## Task 3: Wire the Player prefab Inspector

**Files:**

- Modify (Editor): The Player prefab (path discovered in Step 1).

- [ ] **Step 1: Locate the Player prefab and confirm its GameObject layer**

> **Unity Editor task (user):** In the Project window, navigate to `Assets/Prefabs/Player/`. If a prefab named `Player.prefab` (or similar) exists there, that's the asset to edit. If `Assets/Prefabs/Player/` does not exist, the player may be a scene-only GameObject inside `Platformer.unity`. To check: open `Platformer.unity`, find the `Player` GameObject in the Hierarchy, and look at the Inspector — if the GameObject name is shown in **blue text**, it is a prefab instance (open the prefab via the small `>` arrow next to the name); if shown in **white text** with no prefab indicator, it is scene-only and you will edit it in-scene. **Note for the next steps:** every reference below to "open the prefab" applies equally to "edit the scene-only GameObject" if the player is not a prefab.
>
> **Precondition — confirm the Player root layer is dedicated:** `Physics2D.IgnoreLayerCollision` is layer-pair-global. If the Player root happens to be on `Default` (layer 0), drop-through will briefly disable Default↔OneWayPlatform collision for **every** Default-layer object in the scene during the 0.2 s window. Look at the Inspector header's **Layer** dropdown for the Player root: a dedicated layer such as `Player` is correct; `Default` is acceptable for now (no other Default-layer object currently interacts with `OneWayPlatform`) but **note this assumption** — if a future scene puts other Default-layer objects above one-way platforms, they will fall through too. If the user wants a long-term safe setup, add a `Player` user layer in Project Settings (same procedure as Task 1) and assign it to the Player root before continuing.

- [ ] **Step 2: Add `OneWayPlatform` to the existing Ground Layer mask**

> **Unity Editor task (user):** Select the Player root GameObject in the prefab editor (or in-scene). In the Inspector, find the **Player Controller** component → **Ground Detection** section → **Ground Layer** field. Click the layer-mask dropdown and **add a checkmark** next to `OneWayPlatform` while leaving the existing checked layers (e.g. `Default`/`Ground`) intact. **Why:** the player must ground-detect on one-way platforms for `IsGrounded` to be true while standing on one — without this, jump and drop-through both fail when standing on a one-way platform.

- [ ] **Step 3: Set the new One Way Platform Layer field**

> **Unity Editor task (user):** Still on the Player Controller component, find the new **Drop-Through** section. Click the **One Way Platform Layer** dropdown and check **only** `OneWayPlatform` (no other layers). **Why:** `TryDropThrough` uses this mask to filter "is there a one-way platform under my feet specifically" — if you also include the ground layer here, plain ground would qualify as drop-through-able, breaking the spec.

- [ ] **Step 4: Confirm Drop Through Duration default**

> **Unity Editor task (user):** Verify **Drop Through Duration** reads `0.2`. If it reads `0`, type `0.2` and press Enter. (Newly added SerializeFields with default values normally pick up the default, but this is cheap to confirm.)

- [ ] **Step 5: Save the prefab (or scene)**

> **Unity Editor task (user):** Close the prefab editor (it auto-saves in Unity 6) — or if editing in-scene, **File → Save** the scene.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the modified asset (the Player prefab `.prefab` file, or `Assets/Scenes/Platformer.unity` if scene-only) → Check in with message: `feat: wire Player prefab for one-way platform layer mask`

---

## Task 4: Add `Tilemap_OneWayPlatforms` to `Platformer.unity` and paint test tiles

**Why combined:** the GameObject add and the test-tile paint both modify the same `Platformer.unity` file. Splitting would produce two scene-file diffs that overlap on the same lines and offer no review value.

**Files:**

- Modify (Editor): `Assets/Scenes/Platformer.unity`

- [ ] **Step 1: Open `Platformer.unity`**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity`. In the Hierarchy, locate the existing `Grid` GameObject and expand it to confirm `Tilemap_Ground` is present as a child.

- [ ] **Step 2: Create the sibling `Tilemap_OneWayPlatforms` GameObject**

> **Unity Editor task (user):** Right-click on the `Grid` GameObject in the Hierarchy → **2D Object → Tilemap → Rectangular**. Unity will add a new `Tilemap` child under `Grid`. Rename it to **`Tilemap_OneWayPlatforms`** (exact spelling).

- [ ] **Step 3: Set the layer to `OneWayPlatform`**

> **Unity Editor task (user):** Select `Tilemap_OneWayPlatforms` in the Hierarchy. In the Inspector, change the **Layer** dropdown (top-right of the Inspector header) to **`OneWayPlatform`**. When prompted "Do you want to set layer to … for all child objects as well?" click **Yes, change children**.

- [ ] **Step 4: Match Sorting Layer / Order to `Tilemap_Ground`**

> **Unity Editor task (user):** First, select `Tilemap_Ground` in the Hierarchy and note its **Tilemap Renderer** component's **Sorting Layer** name and **Order in Layer** number. Then select `Tilemap_OneWayPlatforms` and set its Tilemap Renderer's **Sorting Layer** and **Order in Layer** to the same values. **Why:** without this, painted one-way platform tiles render in a different layer order than ground and read as visually inconsistent (e.g. behind background parallax).

- [ ] **Step 5: Add `TilemapCollider2D`**

> **Unity Editor task (user):** With `Tilemap_OneWayPlatforms` selected, click **Add Component** in the Inspector → search for **Tilemap Collider 2D** → add it. Leave **Used By Effector** **unchecked** for now — it gets routed through the composite in the next steps. (We come back and set Composite Operation in Step 8 once the composite exists.)

- [ ] **Step 6: Add `Rigidbody2D` (Static)**

> **Unity Editor task (user):** Click **Add Component** → search for **Rigidbody 2D** → add it. In the new component, change **Body Type** from `Dynamic` to **`Static`**. Leave all other fields at default.

- [ ] **Step 7: Add `CompositeCollider2D`**

> **Unity Editor task (user):** Click **Add Component** → search for **Composite Collider 2D** → add it. **Why:** the existing `Tilemap_Ground` uses the composite-merge pattern, and the new tilemap must match for consistency (smoother edges along multi-tile strips, fewer collider seams that can snag the player). In the new component, set the following fields exactly:
> - **Geometry Type:** **Polygons** (default)
> - **Used By Effector:** **checked** — `PlatformEffector2D` will read its one-way behavior from the composite, not the tilemap collider.
> - Leave all other fields at default (Generation Type = Synchronous is fine for a small test tilemap; switch to Manual on large levels later).

- [ ] **Step 8: Route `TilemapCollider2D` through the composite**

> **Unity Editor task (user):** Re-select the `Tilemap Collider 2D` component on `Tilemap_OneWayPlatforms`. **Check** **Used By Composite** and set **Composite Operation** to **`Merge`**. Confirm **Used By Effector** stays **unchecked** on the tilemap collider — only the composite has effector enabled. (Sanity check: select `Tilemap_Ground` in the Hierarchy and confirm its Tilemap Collider 2D shows the same two settings — Used By Composite checked, Composite Operation Merge, Used By Effector unchecked. The new tilemap should mirror that exactly, plus the composite has Used By Effector checked for the one-way behavior.)

- [ ] **Step 9: Add `PlatformEffector2D`**

> **Unity Editor task (user):** Click **Add Component** → search for **Platform Effector 2D** → add it. In the new component, set the following fields exactly:
> - **Use Collider Mask:** unchecked (default)
> - **Use One Way:** **checked**
> - **Use One Way Grouping:** unchecked (default)
> - **Surface Arc:** **180**
> - **Use Side Friction:** **unchecked**
> - **Use Side Bounce:** **unchecked**

- [ ] **Step 10: Paint a small test layout**

> **Unity Editor task (user):** Open **Window → 2D → Tile Palette**. Pick any palette already used in `Platformer.unity` (e.g. `Palette_SnowMountain` if present, or whatever the existing `Tilemap_Ground` uses). In the Tile Palette window's **Active Tilemap** dropdown at the top, switch from `Tilemap_Ground` to **`Tilemap_OneWayPlatforms`**. Paint roughly:
> - Two short horizontal strips of one-way platform (3–5 tiles wide each) **above** an existing solid ground stretch, at jumpable height (around 2–3 tiles above ground).
> - One additional strip at a higher level so the player can jump from a lower one-way platform up onto a higher one (this exercises smoke-test step 8 — vertically stacked one-ways).
>
> Don't worry about polish; this layout is for verification only. The Snow Mountain plan owns final art placement.

- [ ] **Step 11: Save the scene**

> **Unity Editor task (user):** **File → Save** to write the scene.

- [ ] **Step 12: Check in via UVCS**

Unity Version Control → Pending Changes → stage the scene file (and any auto-generated `.meta` files) → Check in with message: `feat: add Tilemap_OneWayPlatforms to Platformer.unity for verification`

- `Assets/Scenes/Platformer.unity`

---

## Task 5: Manual Play Mode smoke test

**Why no automated tests:** see "No Edit Mode tests" callout at the top of this plan. The eight steps below correspond exactly to the spec's Verification section — each is a pass/fail check; **all eight must pass before this plan is considered complete.**

**Files:** none modified — verification only.

- [ ] **Step 1: Enter Play Mode in `Platformer.unity`**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity` if not already open. Click the **Play** button. Wait for the scene to fully load.

- [ ] **Step 2: Smoke test 1 — pass under from below**

> **Manual check:** Walk/run the player horizontally **under** a one-way platform. **Expected:** the player passes through freely with no collision response. **If fail:** confirm the test tilemap's GameObject layer is `OneWayPlatform` (Task 4 Step 3) and confirm the Player prefab's `Ground Layer` mask **does not** treat OneWayPlatform as fully solid blocker (it's added for ground detection only — `PlatformEffector2D` handles the directional behavior).

- [ ] **Step 3: Smoke test 2 — jump up through, land on top**

> **Manual check:** Position the player **under** a one-way platform. Press Jump. **Expected:** the player passes through on the way up; on descent, lands on top of the platform as solid ground.

- [ ] **Step 4: Smoke test 3 — Down + Jump drops through**

> **Manual check:** Stand on a one-way platform. Hold **Down** on the movement input (keyboard `S` or down arrow / gamepad d-pad down) **and then** press **Jump** (Space / gamepad south). **Expected:** the player falls through the platform and lands on the solid ground below.

- [ ] **Step 5: Smoke test 4 — Down + Jump on solid ground does normal jump**

> **Manual check:** Stand on plain `Tilemap_Ground` (not a one-way platform). Hold Down + press Jump. **Expected:** a normal jump fires upward. No drop-through (there's nothing to drop through). No fall-through.

- [ ] **Step 6: Smoke test 5 — drop-through suppressed during attack-lock or tutorial-lock**

> **Manual check (attack-lock variant):** Stand on a one-way platform. Trigger an attack so movement is locked (the simplest reliable reproduction is to attack a nearby exploration enemy via `PlayerExplorationAttack`). During the attack-lock window, press Down + Jump. **Expected:** nothing happens — the player does not drop through.
>
> **Manual check (tutorial-lock variant):** Stand on a one-way platform inside an active `TutorialPromptTrigger` zone where `_lockMovementWhileInside` is true. Press Down + Jump. **Expected:** nothing happens — Jump is fully disabled by the tutorial path so the input does not fire at all.

- [ ] **Step 7: Smoke test 6 — coyote-time suppressed after drop-through**

> **Manual check:** Drop through a one-way platform (Step 4 above). Within the next ~0.2 s, press Jump again. **Expected:** no upward jump fires — the player continues falling. (The coyote-time counter was zeroed in `TryDropThrough`.)

- [ ] **Step 8: Smoke test 7 — collision restored after Play Mode exit + re-enter**

> **Manual check:** Drop through a one-way platform. **Exit Play Mode** (click the Play button again). **Re-enter Play Mode**. Walk under a one-way platform from below. **Expected:** collision is restored — under-platform pass-through still works, and there is no leftover global `IgnoreLayerCollision` state. (`OnDisable` / `OnDestroy` cleanup verified.)

- [ ] **Step 9: Smoke test 8 — stacked one-ways within drop window pass through both**

> **Manual check:** Stand on the **upper** of two stacked one-way platforms (the layout you painted in Task 4 Step 8). Press Down + Jump. **Expected:** the player drops through both platforms within the 0.2 s window and lands on the surface below the lower platform. **This is documented expected behavior, not a bug** — level designers must avoid stacking one-way platforms within drop-window range when they want the player to be caught by the lower one.

- [ ] **Step 10: Exit Play Mode**

> **Unity Editor task (user):** Click Play to exit Play Mode. **No check-in for this task** — verification only.

---

## Task 6: Update the Snow Mountain plan reference

**Files:**

- Modify: `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`

- [ ] **Step 1: Locate the Snow Mountain plan's tilemap-paint reference**

Open `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md` and search (Ctrl/Cmd-F) for the word `platform` to find the section(s) that describe painting platform-palette tiles. The exact wording will vary by section, but you are looking for any sentence that instructs painting platform tiles onto the same tilemap as rock/ice/ground.

- [ ] **Step 2: Add a one-line cross-reference note**

Below whichever heading covers tile painting (or at the top of that section if there's no clear single anchor), **insert** the following blockquote:

```markdown
> **Update (2026-04-28):** Platform-palette tiles are now painted onto a sibling `Tilemap_OneWayPlatforms` GameObject (under `Grid`, on the `OneWayPlatform` layer) rather than `Tilemap_Ground`. The tile assets in `Assets/Art/Tilemaps/SnowMountain/Tiles/` are reused as-is — only the active paint target changes. See [`docs/superpowers/specs/2026-04-28-one-way-platforms-design.md`](../specs/2026-04-28-one-way-platforms-design.md) and [`docs/superpowers/plans/2026-04-28-one-way-platforms.md`](2026-04-28-one-way-platforms.md) for the mechanic and per-scene tilemap structure.
```

- [ ] **Step 3: Save the file**

> **Editor task:** Save the plan file (your text editor's Save command).

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `docs: cross-reference one-way platforms in Snow Mountain plan`

- `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`

---

## Done criteria

- [ ] Task 1 complete: `OneWayPlatform` user layer present in Project Settings, checked into UVCS.
- [ ] Task 2 complete: `PlayerMovement.cs` and `PlayerController.cs` compile clean with the new fields, methods, and Inspector wiring; checked into UVCS.
- [ ] Task 3 complete: Player prefab (or scene-only Player GameObject) has `OneWayPlatform` added to `Ground Layer`, `One Way Platform Layer` set to `OneWayPlatform`, `Drop Through Duration` at `0.2`; checked into UVCS.
- [ ] Task 4 complete: `Tilemap_OneWayPlatforms` GameObject exists under `Grid` in `Platformer.unity` with the four required components configured per spec; test tiles painted; checked into UVCS.
- [ ] Task 5 complete: **all eight** smoke-test steps pass.
- [ ] Task 6 complete: Snow Mountain plan has the cross-reference blockquote; checked into UVCS.
