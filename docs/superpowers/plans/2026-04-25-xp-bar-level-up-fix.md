# Implementation Plan: XP Bar Level-Up Animation Fix (DEV-85)

> **Design doc:** `docs/plans/2026-04-25-xp-bar-level-up-fix-design.md`
> **Jira:** DEV-85 — "XP bar does not animate correctly when combat awards enough XP to level up"
> **Type:** `fix`
> **Phase:** 6 (World & Content Build-Out)

## Task Summary

| # | Task | Files |
|---|------|-------|
| 1 | Add `AnimateLevelUpFlow` coroutine to `XpBarUI` | `XpBarUI.cs` |
| 2 | Route level-up case in `VictoryScreenUI.Show()` | `VictoryScreenUI.cs` |
| 3 | Count level-ups in `PostBattleFlowController` and pass to `Show()` | `PostBattleFlowController.cs` |
| 4 | Manual Play Mode verification | Unity Editor |

---

## Task 1 — Add `AnimateLevelUpFlow` coroutine to `XpBarUI`

### Code change

Add one serialized field and one public coroutine method to `XpBarUI`.

**New serialized field** (add after `_lerpSpeed`):
```csharp
[SerializeField]
[Tooltip("How long the 'LEVEL UP!' text holds during the XP bar animation, in seconds.")]
private float _levelUpHoldDuration = 0.5f;
```

**New method** (add after `ShowLevelCap()`):
```csharp
/// <summary>
/// Multi-segment XP bar animation for when a level-up occurred during the XP award.
///
/// Phase 1: fill bar from current position to 100% (reaching the level threshold).
/// Phase 2: hold at 100% with "LEVEL UP!" text for <see cref="_levelUpHoldDuration"/>.
/// Phase 3: snap to 0% and animate toward <paramref name="after"/>.<see cref="XpProgress.Progress01"/>.
/// </summary>
public System.Collections.IEnumerator AnimateLevelUpFlow(XpProgress before, XpProgress after)
{
    // Phase 1: fill to 100%
    if (_xpBarImage != null)
    {
        _targetXpFill = 1f;
        while (_xpBarImage.fillAmount < 0.99f)
            yield return null;
        _xpBarImage.fillAmount = 1f;
    }

    if (_xpText != null)
        _xpText.text = $"{before.XpForNextLevel} / {before.XpForNextLevel}";

    // Phase 2: level-up hold
    if (_xpText != null)
        _xpText.text = "LEVEL UP!";
    yield return new UnityEngine.WaitForSecondsRealtime(_levelUpHoldDuration);

    // Phase 3: post-level-up fill
    if (_xpBarImage != null)
        _xpBarImage.fillAmount = 0f;

    if (after.IsAtLevelCap)
    {
        ShowLevelCap();
        yield break;
    }

    _targetXpFill = after.Progress01;
    if (_xpText != null)
        _xpText.text = $"{after.CurrentXp} / {after.XpForNextLevel}";
}
```

### `using` additions

Add at top of file:
```csharp
using Axiom.Core;
```

> **Rationale:** `XpProgress` is in the `Axiom.Core` namespace. The `Battle.asmdef` already references `Axiom.Core`.

---

## Task 2 — Route level-up case in `VictoryScreenUI.Show()`

### Code change

Change the signature and body of `Show()` in `VictoryScreenUI.cs`.

**New signature:**
```csharp
public void Show(PostBattleResult result, XpProgress xpBefore, XpProgress xpAfter, int levelsGained = 0)
```

**Replace the `_xpBar` block** (lines 76–82) with:
```csharp
if (_xpBar != null)
{
    if (xpAfter.IsAtLevelCap)
        _xpBar.ShowLevelCap();
    else if (levelsGained > 0)
        StartCoroutine(_xpBar.AnimateLevelUpFlow(xpBefore, xpAfter));
    else
        _xpBar.AnimateTo(xpAfter.CurrentXp, xpAfter.XpForNextLevel, xpBefore.Progress01);
}
```

> **Note:** The `levelsGained` parameter defaults to `0` so existing callers (if any) and tests compile without change. Only `PostBattleFlowController` passes a non-zero value.

> **Guard clause ordering:** `IsAtLevelCap` check comes first — if the player hit the cap during the award, `ShowLevelCap()` handles the UI and we never enter the coroutine. The `levelsGained > 0` check comes next, then fall through to the no-level-up path.

---

## Task 3 — Count level-ups in `PostBattleFlowController`

### Code change

Modify `BeginVictoryFlow()` in `PostBattleFlowController.cs` to track level-ups during `AwardXp`.

**Replace the AwardXp block** (lines 89–96) with:

```csharp
int levelsGained = 0;

if (gm != null)
{
    if (gm.ProgressionService != null)
        gm.ProgressionService.OnLevelUp += OnLevelUpDuringAward;

    if (result.Xp > 0)
        gm.AwardXp(result.Xp);

    if (gm.ProgressionService != null)
        gm.ProgressionService.OnLevelUp -= OnLevelUpDuringAward;

    for (int i = 0; i < result.Items.Count; i++)
        gm.PlayerState.Inventory.Add(result.Items[i].ItemId, result.Items[i].Quantity);
}

void OnLevelUpDuringAward(LevelUpResult _) => levelsGained++;
```

**Replace the `_victoryScreenUI.Show()` call** (line 115):
```csharp
_victoryScreenUI.Show(result, xpBefore, xpAfter, levelsGained);
```

> **Local function:** `OnLevelUpDuringAward` is a C# local function — it captures `levelsGained` without allocating a closure object (since `levelsGained` is a stack variable and the lambda is only used within the same method). Subscribe before `AwardXp`, unsubscribe after — this ensures the counter only fires for this specific award, not for any later level-ups.

---

## Task 4 — Manual verification

> **Unity Editor task (user):**

1. Open `Assets/Scenes/Battle.unity`.
2. Enter Play Mode.
3. Trigger a battle where the player (L1, 0/100 XP) wins enough XP to level up (100+ XP).
4. **Verify:** The XP bar fills from 0 → 100%, shows "LEVEL UP!" for 0.5s, then resets to 0/200 for L2.
5. Trigger another battle where XP does NOT cause a level-up.
6. **Verify:** The XP bar animates normally (no regression).
7. Confirm the XP bar text shows correct values in both scenarios.

---

## UVCS Check-in

- [ ] **Check in via UVCS:**
  `fix(DEV-85): add multi-segment XP bar animation for level-up case`
  - `Assets/Scripts/Battle/UI/XpBarUI.cs`
  - `Assets/Scripts/Battle/UI/XpBarUI.cs.meta`
  - `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`
  - `Assets/Scripts/Battle/UI/VictoryScreenUI.cs.meta`
  - `Assets/Scripts/Battle/PostBattleFlowController.cs`
  - `Assets/Scripts/Battle/PostBattleFlowController.cs.meta`

---

## Test Strategy

| What | How | Why |
|------|-----|-----|
| No level-up path | Existing `AnimateTo()` path unchanged; covered by no-regression manual test (Task 4 step 5-6) | No code changes to that path |
| Level-up path | Manual Play Mode verification (Task 4 step 3-4) | Coroutine-based animation requires Unity runtime — cannot test in EditMode |
| Edge: level cap | `ShowLevelCap()` path unchanged and checked first in the new branching | No code changes |
| Edge: null `_xpBar` | Guard clause at top of `_xpBar` block unchanged | No code changes |
| EditMode: level-up counting logic | The `levelsGained++` counter is a trivial local variable increment executed inside a synchronous callback chain (`OnLevelUp` fires synchronously from `AwardXp`). `ProgressionServiceTests` already covers `OnLevelUp` firing correctly. | Low risk; existing test coverage of the underlying mechanic is sufficient |

No new Unity Test Framework tests added — the fix is animation-only (coroutine) and the branching logic is trivial (one `if` condition). The existing `ProgressionServiceTests` and `GameManagerProgressionTests` verify the `OnLevelUp` event fires correctly.
