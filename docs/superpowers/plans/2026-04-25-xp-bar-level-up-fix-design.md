# Design: XP Bar Level-Up Animation Fix (DEV-85)

## Summary

When combat awards enough XP to trigger a level-up (e.g. L1 0/100 + 100 XP → L2 0/200), the Victory screen XP bar fails to animate because `xpBefore.Progress01` and `xpAfter.Progress01` are both 0f — the bar's fillAmount has nowhere to go. Fix: add a multi-segment coroutine animation to `XpBarUI` that handles the level-up journey.

## Root Cause

`PostBattleFlowController.BeginVictoryFlow()` calls `gm.AwardXp(result.Xp)` **before** taking the `xpAfter` snapshot. When a level-up triggers, `AwardXp` resets `PlayerState.Xp` to 0 and increments the level. Both `xpBefore` (L1 0/100) and `xpAfter` (L2 0/200) have `progress01 = 0f`. The bar starts at 0 and targets 0 — no visible movement.

A previous fix attempt (taking snapshots before and after `AwardXp`) addressed the ordering but couldn't resolve the fundamental issue: two `XpProgress` snapshots in different level contexts can't capture the "fill to 100% then reset" journey.

## Design

### Data Flow

```
PostBattleFlowController.BeginVictoryFlow()
  → subscribe to OnLevelUp before AwardXp, count levelsGained
  → call AwardXp(xp), which fires OnLevelUp synchronously
  → call GetXpProgress() → xpAfter
  → VictoryScreenUI.Show(result, xpBefore, xpAfter, levelsGained)

VictoryScreenUI.Show(result, xpBefore, xpAfter, levelsGained)
  → if levelsGained > 0 → _xpBar.AnimateLevelUpFlow(before, after)
  → else → _xpBar.AnimateTo(after.CurrentXp, after.XpForNextLevel, before.Progress01)
```

### `XpBarUI.AnimateLevelUpFlow` (new coroutine)

Three-phase animation driven by the existing `Update()` lerp loop:

| Phase | Duration | What happens |
|-------|----------|-------------|
| 1. Fill to 100% | Until `fillAmount ≈ 1.0` | Set `_xpText` to pre-level-up values (e.g. "0 / 100"). Set `_targetXpFill = 1.0f`. `Update()` lerps toward 1.0. |
| 2. Level-up hold | 0.5s realtime | Set `_xpText = "LEVEL UP!"`. Hold fillAmount at 1.0. |
| 3. Post-level-up | Until `fillAmount ≈ target` | Snap `_xpBarImage.fillAmount = 0f`. Set `_xpText` to post-level-up values (e.g. "0 / 200"). Set `_targetXpFill = after.Progress01`. `Update()` lerps. |

### Changes

| File | Change |
|------|--------|
| `PostBattleFlowController.cs` | Count `levelsGained` via `ProgressionService.OnLevelUp` subscription during `BeginVictoryFlow()`. Pass to `VictoryScreenUI.Show()`. |
| `VictoryScreenUI.cs` | Add `int levelsGained` parameter to `Show()`. Branch: levelsGained > 0 calls `_xpBar.AnimateLevelUpFlow()`, else existing `AnimateTo()`. |
| `XpBarUI.cs` | Add `AnimateLevelUpFlow(XpProgress before, XpProgress after)` coroutine with the three-phase animation. |
| `XpBarUI.cs` | Add `[SerializeField] float _levelUpHoldDuration = 0.5f` for the hold phase. |

### Non-changes (no regression)

- `AnimateTo()` and `SetXP()` remain unchanged for the no-level-up path
- `Update()` lerp loop stays, no modification needed
- `ShowLevelCap()` path stays, no modification needed
- `ProgressionService.AwardXp()` stays — no changes to the core level-up logic

### Edge Cases

| Case | Behavior |
|------|----------|
| No level-up (`levelsGained == 0`) | Existing `AnimateTo()` path — unchanged |
| Exact level-up (0/100 + 100, no overflow) | Phase 1 fills to 100% → Phase 2 flash → Phase 3 shows 0/200 (fillAmount stays 0, no second movement) |
| Level-up with overflow (50/100 + 150 = 50/200 L2) | Phase 1: 0.5→1.0 → flash → Phase 3: 0→0.25 |
| Multi-level (0/100 + 350 = 50/400 L3) | Same flow; one combined flash for all levels gained |
| At level cap (levelsGained == 0, IsAtLevelCap) | `ShowLevelCap()` path — unchanged |
| `_xpBarImage` or `_xpText` null | Guarded by null checks before each set |
| Coroutine stopped mid-animation (victory screen dismissed) | `OnDestroy` clears `_xpBarImage` reference; coroutine null-guards at each step |
| `_xpBar` not assigned in VictoryScreenUI | Guard clause; branches not entered |

### Serialized Fields (added)

```
XpBarUI:
  [SerializeField] float _levelUpHoldDuration = 0.5f;
```
