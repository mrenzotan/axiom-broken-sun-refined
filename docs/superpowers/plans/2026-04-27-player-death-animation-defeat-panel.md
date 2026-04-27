# Player Death Animation & Defeat Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Wire the existing `playerDeath` animation state in `Player.controller` so it plays when the player's HP reaches zero in the platformer scene, and show a defeat panel with a continue button for checkpoint respawn or game-over transition.

**Architecture:** `PlayerDeathHandler` (MonoBehaviour) polls `PlayerState.CurrentHp` each frame. On zero: disables `PlayerController`, triggers the `Defeat` animator parameter directly on the player's `Animator` via `GetComponentInChildren<Animator>()`, waits `_deathAnimSeconds`, then shows a pre-built defeat panel. The continue button invokes `GameManager.RespawnAtLastCheckpoint()` or falls back to `SceneTransition.BeginTransition(_gameOverSceneName)`. `PlayerHurtFeedback` is spike-only and does NOT participate in the death flow.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, Mono scripting backend, Unity Version Control (UVCS) for check-ins.

**Project conventions:**
- **MonoBehaviours own Unity lifecycle only** — `PlayerDeathHandler` finds and drives `Animator` directly; no intermediate service.
- **No new singletons.**
- **UVCS commit format:** `<type>(DEV-46): <short description>`.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Platformer/PlayerDeathHandler.cs` | **Modified** | Owns the full death sequence: null-guards, `PlayerDeathResolver`, controller disable, animator Defeat trigger, coroutine wait, defeat panel show/hide, checkpoint respawn. |
| `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` | **Modified (pruned)** | Removed `_defeatTriggerName` field and `PlayDefeatAnimation()` method — death animation is not this component's concern. |
| `Assets/Prefabs/Player/Player (Exploration).prefab` | **Modified** | Removed `_defeatTriggerName` from the `PlayerHurtFeedback` serialized block (if it was present). |
| `Assets/Scenes/Level_1-1.unity` | **Modified** | Copied `DefeatScreenPanel` subtree from `Battle.unity` (DimOverlay, TitleText, Subtitle, ConfirmButton), removed `DefeatScreenUI` script, wired `PlayerDeathHandler._defeatPanel` and `_continueButton`. |
| `Assets/Animations/Player/Player.controller` | **Modified (Editor — manual step)** | Add `Defeat` trigger parameter; add Any State → `playerDeath` transition with `Defeat` condition, no exit time, zero duration. |
| `Assets/Animations/Player/playerDeath.anim` | **Existing (no changes)** | Animation clip already bound to the `playerDeath` state in `Player.controller`. One-frame or looping death pose. |

---

## Task 1: Strip defeat logic from PlayerHurtFeedback

**Why:** `PlayerHurtFeedback` owns spike hazard hurt feedback only — pain tint, hurt animation, flash on tick. Death animation is the `PlayerDeathHandler`'s domain. Keeping defeat logic in `PlayerHurtFeedback` creates a split-ownership anti-pattern.

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerHurtFeedback.cs`
- Modify: `Assets/Prefabs/Player/Player (Exploration).prefab`

- [ ] **Step 1: Remove `_defeatTriggerName` serialized field from PlayerHurtFeedback.cs**

Open `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` and delete these lines (lines ~35–37 in the original):

```csharp
        [SerializeField]
        [Tooltip("Animator trigger parameter name fired when the player dies.")]
        private string _defeatTriggerName = "Defeat";
```

- [ ] **Step 2: Remove `PlayDefeatAnimation()` method from PlayerHurtFeedback.cs**

Delete these lines (lines ~63–67 in the original):

```csharp
        public void PlayDefeatAnimation()
        {
            if (_animator == null) return;
            _animator.SetTrigger(_defeatTriggerName);
        }
```

- [ ] **Step 3: Remove `_defeatTriggerName` from the prefab YAML**

Open `Assets/Prefabs/Player/Player (Exploration).prefab` and delete this line from the `PlayerHurtFeedback` serialized block:

```yaml
  _defeatTriggerName: Defeat
```

The block should look like:

```yaml
  m_EditorClassIdentifier: Axiom.Platformer::Axiom.Platformer.PlayerHurtFeedback
  _animator: {fileID: 0}
  _spriteRenderer: {fileID: ...}
  _painTint: {r: 1, g: 0.6, b: 0.6, a: 1}
  _hurtTriggerName: Hurt
```

- [ ] **Step 4: Check in via UVCS**

  Unity Version Control → Pending Changes → stage:
  - `Assets/Scripts/Platformer/PlayerHurtFeedback.cs`
  - `Assets/Scripts/Platformer/PlayerHurtFeedback.cs.meta`
  - `Assets/Prefabs/Player/Player (Exploration).prefab`
  - `Assets/Prefabs/Player/Player (Exploration).prefab.meta`

  Check in message: `refactor(DEV-46): remove defeat animation from PlayerHurtFeedback — owned by PlayerDeathHandler`

---

## Task 2: Wire defeat animation in PlayerDeathHandler

**Why:** `PlayerDeathHandler` already polls HP and controls the death sequence. It should own the animation trigger directly, not delegate to another component. Using `GetComponentInChildren<Animator>()` avoids needing a separate serialized field while respecting the prefab hierarchy (Animator is a child of the Player root).

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerDeathHandler.cs`

- [ ] **Step 1: Update DeathSequence to trigger the Defeat animation directly on the Animator**

Replace the `DeathSequence()` method body to find the player's `Animator` through the `PlayerController`'s children hierarchy and call `SetTrigger("Defeat")`:

```csharp
private IEnumerator DeathSequence()
{
    PlayerController controller = FindFirstObjectByType<PlayerController>();
    if (controller != null)
    {
        controller.enabled = false;

        Animator animator = controller.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.SetTrigger("Defeat");
    }

    yield return new WaitForSeconds(_deathAnimSeconds);

    ShowDefeatPanel();
}
```

**Key design decisions:**
- `GetComponentInChildren<Animator>()` searches the `PlayerController`'s hierarchy — in the exploration prefab, the `Animator` child GameObject is at `Player/Animator`.
- The `Defeat` trigger name is hardcoded `"Defeat"` rather than a serialized field — the trigger name is a contract between this handler and the `Player.controller`, not a tuning knob.
- `PlayerHurtFeedback` is NOT called here — death animation is not a hurt feedback concern.

- [ ] **Step 2: Check in via UVCS**

  Unity Version Control → Pending Changes → stage:
  - `Assets/Scripts/Platformer/PlayerDeathHandler.cs`
  - `Assets/Scripts/Platformer/PlayerDeathHandler.cs.meta`

  Check in message: `feat(DEV-46): wire Defeat animator trigger directly in PlayerDeathHandler`

---

## Task 3: Add Defeat trigger and Any State transition in Player.controller

**Why:** The `playerDeath` state already exists in `Player.controller` (fileID `2673752977258769594`) and is bound to `playerDeath.anim`. It needs a trigger parameter so code can reach it, and an Any State transition so it can be entered from anywhere (idle, walk, jump, hurt, etc.).

> **Unity Editor task (user):** This task must be performed in the Unity Editor's Animator window. The `.controller` YAML uses extremely large fileIDs that overflow tool-internal types when edited programmatically.

- [ ] **Step 1: Add the `Defeat` trigger parameter**

  1. Open the Animator window: **Window → Animation → Animator**
  2. Select the `Player.controller` asset (at `Assets/Animations/Player/Player.controller`)
  3. In the **Parameters** tab (left panel), click the **+** dropdown
  4. Select **Trigger**
  5. Name it `Defeat`

- [ ] **Step 2: Create Any State → playerDeath transition**

  1. In the Animator graph, locate the **Any State** node — it's cyan/dark-blue
  2. Right-click **Any State** → **Make Transition**
  3. Click the **playerDeath** state box (named `playerDeathM` in the controller file)
  4. Select the white transition arrow you just created
  5. In the **Inspector** panel, configure:

  | Setting | Value |
  |---|---|
  | Conditions | Add → `Defeat` |
  | Has Exit Time | **Unchecked** |
  | Transition Duration | `0` |
  | Can Transition To Self | **Unchecked** |

  6. Verify: the transition arrow should point from Any State to playerDeath, the `Defeat` condition text should be visible on the arrow.

- [ ] **Step 3: Verify the playerDeath state's motion clip**

  1. Click the **playerDeath** state box in the Animator graph
  2. In the Inspector, confirm **Motion** is set to `playerDeath` (the `.anim` at `Assets/Animations/Player/playerDeath.anim`)
  3. Set **Speed** to `1`
  4. Confirm **Write Defaults** is **unchecked** (matching the YAML: `m_WriteDefaultValues: 0`)

- [ ] **Step 4: Check in via UVCS**

  Unity Version Control → Pending Changes → stage:
  - `Assets/Animations/Player/Player.controller`
  - `Assets/Animations/Player/Player.controller.meta`

  Check in message: `feat(DEV-46): add Defeat trigger and AnyState→playerDeath transition`

---

## Task 4: Copy defeat panel UI from Battle scene into Level_1-1

**Why:** The Battle scene already has a polished `DefeatScreenPanel` hierarchy (DimOverlay, TitleText "DEFEATED", Subtitle, Return button). Copy-paste in the Editor avoids rebuilding the same structure by hand. The only changes needed: remove the `DefeatScreenUI` script (battle-specific) and wire the `PlayerDeathHandler` references.

> **Unity Editor task (user):** This task is performed entirely in the Unity Editor — open both scenes simultaneously.

### Source hierarchy (Battle.unity)

```
DefeatScreenPanel (670806621) [Image, CanvasRenderer, DefeatScreenUI]
├── DimOverlay (1008520209) [Image, CanvasRenderer] — black at 78% alpha, stretches full panel
└── PanelFrame (1581078462) [VerticalLayoutGroup, Outline, Image, CanvasRenderer]
    ├── TitleBG (2042870458) [Image, CanvasRenderer] — sliced background behind title
    ├── TitleText (542590733) [TextMeshProUGUI] — "DEFEATED", size 48, dark red, centered
    ├── Subtitle (916097851) [TextMeshProUGUI] — "Return to last checkpoint?", size 32, white
    └── ConfirmButton (1347803177) [Button, Image, CanvasRenderer]
        └── Continue (963812182) [TextMeshProUGUI] — "Return", white
```

Key IDs for wiring after copy:
| Element | GameObject ID | Component to reference |
|---|---|---|
| DefeatScreenPanel root | 670806621 | Drag onto `_defeatPanel` |
| ConfirmButton Button | 1347803180 (Button component) | Drag onto `_continueButton` |
| TitleText TextMeshProUGUI | 542590736 | For `_titleText` if wiring |

- [ ] **Step 1: Open both scenes**

  1. Open `Assets/Scenes/Battle.unity`
  2. File → New Scene (or remain in Battle)
  3. Open `Assets/Scenes/Level_1-1.unity` (both scenes now available in the Hierarchy tab)

> **Warning:** Do NOT save Battle.unity after making any accidental changes. Only Level_1-1.unity should be saved.

- [ ] **Step 2: Copy the DefeatScreenPanel from Battle**

  1. In the Battle scene hierarchy, locate **DefeatScreenPanel** (it's a child of the BattleHUD Canvas)
  2. Right-click **DefeatScreenPanel** → **Copy** (or Ctrl+C)

- [ ] **Step 3: Paste into Level_1-1**

  1. Switch to the Level_1-1 scene (click its tab)
  2. If there's already a Canvas for the defeat screen, select it; otherwise, right-click in the hierarchy → **UI → Canvas** first
  3. Right-click the Canvas (or empty area in hierarchy) → **Paste** (or Ctrl+V)
  4. Unity auto-renames pasted objects — rename the root back to **DefeatScreenPanel**

- [ ] **Step 4: Remove the DefeatScreenUI script**

  1. Select **DefeatScreenPanel** in the Level_1-1 hierarchy
  2. In the Inspector, find the **DefeatScreenUI** component (script icon)
  3. Click the **...** menu on the component → **Remove Component**
  4. This component is battle-specific (`Axiom.Battle.UI.DefeatScreenUI`) and is replaced by `PlayerDeathHandler`'s inline logic

- [ ] **Step 5: Set the panel to start inactive**

  1. Select **DefeatScreenPanel**
  2. Uncheck the checkbox next to its name in the Inspector (sets `m_IsActive: 0`)
  3. The panel should be hidden at scene start; `PlayerDeathHandler` activates it on death

- [ ] **Step 6: Wire PlayerDeathHandler references**

  1. In `Level_1-1.unity`, find the Player GameObject with the `PlayerDeathHandler` component
  2. Drag **DefeatScreenPanel** onto the **_defeatPanel** field
  3. Drag the **ConfirmButton** GameObject (or its Button component) onto the **_continueButton** field
  4. Confirm `_deathAnimSeconds` = `1.2`
  5. Confirm `_gameOverSceneName` = `MainMenu`
  6. Confirm `_transitionStyle` = the desired fade style

- [ ] **Step 7: Optional — adjust text for platformer context**

  The Battle scene's text may not fit the exploration context:
  - **Subtitle:** "Return to last checkpoint?" → can keep or change to "You have fallen..."
  - **Continue button label:** "Return" → can keep or change to "Continue"

  Edit these by selecting the TextMeshProUGUI components and changing the **Text** field.

- [ ] **Step 8: Verify the Canvas sorting**

  1. Find the parent Canvas of DefeatScreenPanel
  2. Ensure its **Sort Order** is higher than the HUD canvas (e.g., `100`) so the defeat panel renders on top
  3. If the Canvas is shared with other UI, set override sorting on the Canvas component or create a dedicated Canvas

- [ ] **Step 9: Check in via UVCS**

  Unity Version Control → Pending Changes → stage:
  - `Assets/Scenes/Level_1-1.unity`
  - `Assets/Scenes/Level_1-1.unity.meta`

  Check in message: `feat(DEV-46): copy defeat panel UI from Battle scene, wire to PlayerDeathHandler`

---

## Task 5: Play-Mode Verification

**Why:** The defeat flow crosses three systems (HP polling, animator, UI), each owned by a different component. A full playtest confirms the chain works end-to-end.

- [ ] **Step 1: Open `Level_1-1.unity` and enter Play Mode**

- [ ] **Step 2: Walk into the InstantKO pit**

  **Expected:**
  1. Hurt animation plays briefly (via `PlayerHurtFeedback` from `HazardTrigger`)
  2. PlayerController disables — no input, no movement
  3. `playerDeath` animation plays (sprite freezes on death frame or loops the death clip)
  4. All other player animations stop (no idle/walk/jump blending)
  5. After ~1.2 seconds, the DefeatScreenPanel appears with dim overlay, "DEFEATED" title, and "Continue" button

- [ ] **Step 3: Click the "Continue" button**

  **Expected (with checkpoint active):**
  1. DefeatScreenPanel hides
  2. Player respawns at last checkpoint with full HP
  3. PlayerController re-enables
  4. Gameplay resumes

  **Expected (no checkpoint):**
  1. Scene transitions to `MainMenu` with black fade

- [ ] **Step 4: Get hit by spikes until HP reaches 0**

  **Expected:** Same death sequence as InstantKO pit — the `PlayerDeathHandler` polls HP, not the damage source. Both paths converge.

- [ ] **Step 5: Exit Play Mode**

---

## Reversion Guide

If the defeat animation or panel needs to be removed:

1. **Defeat trigger:** Delete the `Defeat` parameter from `Player.controller` and the Any State → playerDeath transition.
2. **DeathHandler:** Revert `DeathSequence()` to its pre-Defeat state. Old code only disabled the controller and called `PlayerHurtFeedback.PlayDefeatAnimation()` — but that method was also removed in Task 1. If reverting entirely, also restore `_defeatTriggerName` and `PlayDefeatAnimation()` to `PlayerHurtFeedback`.
3. **UI:** Delete the `DefeatScreenCanvas` GameObject from `Level_1-1.unity`. Unwire `_defeatPanel` and `_continueButton` from the Player's `PlayerDeathHandler`.
4. **Prefab:** Restore `_defeatTriggerName: Defeat` to the prefab YAML if it was removed.

## Edge Cases & Design Decisions

| Scenario | Behavior |
|---|---|
| Player dies with no `Animator` found | `GetComponentInChildren<Animator>()` returns null → `SetTrigger` is skipped gracefully. Death sequence continues to show the panel after the wait. |
| `_defeatPanel` is null | `ShowDefeatPanel()` returns early. No panel appears, but the player is still stuck (controller disabled) — the scene essentially soft-locks. This is by design: wire the panel in the Editor. |
| `_continueButton` is null | Listener is not added. Panel still shows but is non-interactive. |
| Player dies twice (HP drops below 0, respawns, dies again) | `_dispatched` resets when `PlayerDeathHandler` is disabled and re-enabled (scene reload or GameManager enable cycle). Works correctly. |
| `playerDeath` animation clip is missing or unbound | The `playerDeath` state transitions anyway — the sprite holds its last frame. Functionally works but looks frozen. Verify the clip is bound in Task 3 Step 3. |
| Player dies while `PlayerHurtFeedback` is mid-flash | No conflict — the animator transitions from hurt→death via Any State. The `Defeat` trigger fires, the hurt state exits, and the sprite immediately snaps to the death animation. |
