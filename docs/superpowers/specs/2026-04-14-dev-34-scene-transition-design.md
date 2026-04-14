# DEV-34: Animated Scene Transition Design

**Ticket:** DEV-34 — Implement animated scene transition between Platformer and Battle scenes  
**Date:** 2026-04-14  
**Status:** Approved

---

## Overview

Add a full-screen overlay transition effect when moving between the Platformer and Battle scenes in both directions. Scene events must not begin until the transition fade-in completes.

---

## Transition Effects

| Direction | Style | Fade Out | Fade In | Total |
|-----------|-------|----------|---------|-------|
| Platformer → Battle | White flash | 0.2s (flash to white) | 0.8s (reveal from white) | 1.0s |
| Battle → Platformer | Black fade | 0.5s (fade to black) | 0.5s (reveal from black) | 1.0s |

No external sprites or asset imports required. The overlay is a built-in Unity UI `Canvas` + `Image` (solid color, alpha animated via coroutine using `Color.Lerp`). Canvas sort order is set high enough to render above all game UI.

---

## Components

All new files live in `Assets/Scripts/Core/`.

### `TransitionStyle` (enum)

```
WhiteFlash  — Platformer → Battle
BlackFade   — Battle → Platformer
```

Callers declare intent via style; color and timing are internal to `SceneTransitionService`.

### `SceneTransitionService` (plain C#)

Owns transition state and config. No Unity dependencies.

- `bool IsTransitioning` — true from the start of fade-out until after fade-in completes
- Timing and color mappings keyed by `TransitionStyle`
- Injected into `SceneTransitionController`

### `SceneTransitionController` (MonoBehaviour — added to the GameManager prefab)

Unity lifecycle wrapper. Owns the child `Canvas` + `Image` overlay GameObject.

**Public API:**
```csharp
void BeginTransition(string sceneName, TransitionStyle style)
bool IsTransitioning  // delegates to SceneTransitionService
```

**Behavior:**
- No-op if `IsTransitioning` is already true (guards against double-triggers)
- Drives the three-phase coroutine (fade out → async load → fade in)
- Holds `allowSceneActivation = false` on the async operation until the overlay is fully opaque
- Fires `GameManager.OnSceneReady` after fade-in completes, then clears `IsTransitioning`

### `GameManager` changes

- Add `public event Action OnSceneReady`
- Add `public SceneTransitionController SceneTransition { get; private set; }` — assigned in `Awake` from the child component

---

## Transition Flow

### Platformer → Battle (WhiteFlash)

1. **Flash in** (0.2s): Image = white, alpha lerps `0 → 1`
2. **Scene load**: `LoadSceneAsync("Battle", Single)` fires; activation held until overlay is fully opaque
3. Scene activates
4. **Fade out** (0.8s): alpha lerps `1 → 0` — new scene revealed from white
5. `GameManager.OnSceneReady` fires

### Battle → Platformer (BlackFade)

1. **Fade to black** (0.5s): Image = black, alpha lerps `0 → 1`
2. **Scene load**: `LoadSceneAsync("Platformer", Single)` fires; activation held until overlay is fully opaque
3. Scene activates
4. **Fade from black** (0.5s): alpha lerps `1 → 0`
5. `GameManager.OnSceneReady` fires

---

## Integration Points

### Call sites replaced

| File | Current | Replacement |
|------|---------|-------------|
| `ExplorationEnemyCombatTrigger.TriggerBattle()` | `SceneManager.LoadScene("Battle")` | `GameManager.Instance.SceneTransition.BeginTransition("Battle", TransitionStyle.WhiteFlash)` |
| `BattleController.HandleStateChanged()` on `BattleState.Fled` | `SceneManager.LoadScene("Platformer")` | `GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade)` |

### Scene entry point gate — `BattleController.Start()`

```csharp
void Start()
{
    if (GameManager.Instance?.SceneTransition.IsTransitioning == true)
        GameManager.Instance.OnSceneReady += InitializeFromTransition;
    else
        InitializeFromTransition(); // standalone Battle scene testing path
}
```

`InitializeFromTransition` unsubscribes from `OnSceneReady` immediately before calling `Initialize()`.

### Scene entry point gate — `PlayerController.Start()`

Same gate pattern as `BattleController` — player input and movement blocked until `OnSceneReady` fires.

---

## No-Regression Guards

- `BeginTransition` is a no-op when `IsTransitioning == true` — prevents double-trigger if two enemies overlap the player simultaneously
- `OnSceneReady` subscribers unsubscribe immediately after the callback fires — no phantom calls on subsequent transitions
- Both scene gates fall through to immediate initialization when `GameManager` is absent — standalone scene testing in the Editor is unaffected
- `SceneTransitionController` null-checks `GameManager.Instance` before firing `OnSceneReady`

---

## Unity Setup (Editor steps, not scripted)

1. Add `SceneTransitionController` MonoBehaviour to the **GameManager prefab**
2. Create a child GameObject `TransitionOverlay` on the GameManager prefab:
   - Add `Canvas` (Screen Space — Overlay, Sort Order = 999)
   - Add `CanvasScaler` (Scale With Screen Size)
   - Add child `Image` GameObject: stretch to fill parent, color = white/black with alpha = 0
3. Wire the `Image` reference in the `SceneTransitionController` Inspector field
