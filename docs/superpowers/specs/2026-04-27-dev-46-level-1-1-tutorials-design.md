# Level_1-1 Remaining Tutorials — Design Spec

**Ticket:** DEV-46 (Level 1: Snow Mountain) — tutorial completion sub-feature
**Date:** 2026-04-27
**Status:** Design — pending user review

## Goal

Complete the five remaining tutorial moments in Level_1-1 (the tutorial stage):

1. **First-death prompt** — fires once after the player's first death/respawn, explaining that torches are save points and the first touch of each one heals fully.
2. **First-battle button-lock + Surprised reinforcement** — the very first battle (IceSlime, Surprised start) locks Spell/Item/Flee, leaving only Attack. An in-Battle prompt reinforces that "Surprised" means the enemy acts first.
3. **First spike-hit prompt** — fires once the first time the player touches a spike hazard, explaining DoT-while-standing.
4. **Ice-wall tutorial** — already covered by the existing `Tutorial_IceWall` zone (no new work for this item; included for completeness).
5. **Spell-tutorial battle** — the second battle (Meltspawn, Advantaged start) walks the player through Liquid → Freeze → Frozen + Solid → Attack. Movement is locked at the `Tutorial_Advantaged` zone before the battle so the player must engage by attacking.

Existing forced-zone prompts (`Tutorial_Movement`, `Tutorial_HighJump`, `Tutorial_Surprised` text, `Tutorial_Advantaged` text, `Tutorial_IceWall`) stay as they are.

## Scope and non-goals

**In scope:**
- Persistence of "I've seen this tutorial" flags across save/quit/reload.
- Runtime detection of first-death and first-spike-hit events.
- Movement input lock when the player enters `Tutorial_Advantaged`.
- Battle-side tutorial controller for both first-battle (button lock) and spell-tutorial (multi-step state machine) modes.
- Editor wiring of existing trigger zones, enemy combat triggers, and EnemyData.

**Out of scope:**
- Reworking the platformer's existing trigger zones beyond the additions described.
- Modifying Vosk/voice recognition. Freeze must already be unlocked in `SpellVocabularyManager` for the player when the spell-tutorial battle begins; that's a content/data decision tracked by DEV-38, not this spec.
- Tutorials in Level_1-2..1-4. Per the Level_1 plan, Level_1-2 onward is "no tutorial prompts."
- Localization. All tutorial text is English-only TextArea fields, editable in the Inspector.

## Architectural constraints (from CLAUDE.md)

- `MonoBehaviour` handles Unity lifecycle only; gameplay logic lives in plain C# classes injected into them.
- No new static singletons (only `GameManager` is allowed). Static C# events are fine.
- ScriptableObject-driven data; tutorial text is configurable per-instance via `[SerializeField] [TextArea]` fields.
- No premature abstraction: each tutorial is a small, focused controller.

## Architecture overview

**Approach:** composable per-tutorial controllers. Each conditional tutorial is its own small `MonoBehaviour`. They share two pieces of infrastructure: persisted flags on `PlayerState` and small extensions to existing components (`TutorialPromptTrigger`, `PlayerMovement`, `ActionMenuUI`).

```
                   ┌─────────────────────────┐
                   │ PlayerState (persisted) │
                   │  HasSeenFirstDeath      │
                   │  HasSeenFirstSpikeHit   │
                   │  HasCompletedFirstBattle│
                   │  HasCompletedSpellTuto. │
                   └────────────┬────────────┘
                                │ read by
        ┌───────────────────────┼─────────────────────────────┐
        ▼                       ▼                             ▼
┌────────────────────┐  ┌──────────────────┐    ┌──────────────────────┐
│ TutorialPromptTrig.│  │ FirstDeathPrompt │    │ BattleTutorialContrl │
│  (extended)        │  │ Controller       │    │  (Battle scene)      │
│                    │  │                  │    │                      │
│ _oneShotFlag       │  │ subscribes to    │    │ Setup() called by    │
│ _lockMovementWhile │  │ GameManager      │    │ BattleController     │
│  Inside            │  │ transient flag   │    │ during Start()       │
└────────────────────┘  └──────────────────┘    └──────────────────────┘

┌─────────────────────────────┐
│ FirstSpikeHitPromptController│  ◄── HazardTrigger.OnPlayerFirstHitFrame
└─────────────────────────────┘       (static C# event)
```

## 1. Persistence layer

### `PlayerState` — 4 new flags

```csharp
public bool HasSeenFirstDeath { get; private set; }
public bool HasSeenFirstSpikeHit { get; private set; }
public bool HasCompletedFirstBattleTutorial { get; private set; }
public bool HasCompletedSpellTutorialBattle { get; private set; }

public void MarkFirstDeathSeen()                  { HasSeenFirstDeath = true; }
public void MarkFirstSpikeHitSeen()               { HasSeenFirstSpikeHit = true; }
public void MarkFirstBattleTutorialCompleted()    { HasCompletedFirstBattleTutorial = true; }
public void MarkSpellTutorialBattleCompleted()    { HasCompletedSpellTutorialBattle = true; }
```

Defaults: `false`. Reconstructed `false` on New Game (PlayerState rebuilds from `CharacterData`).

### Save migration

The existing JSON serializer needs the four new keys added. Old saves missing them deserialize to `false`. This is acceptable for a pre-release game: a returning player who already finished the tutorials will see them once on the first load after the update, then never again.

## 2. `TutorialPromptTrigger` extensions

```csharp
public enum OneShotTutorialFlag { None, FirstBattle, SpellTutorialBattle, FirstSpikeHit, FirstDeath }

[SerializeField] private OneShotTutorialFlag _oneShotFlag = OneShotTutorialFlag.None;
[SerializeField] private bool _lockMovementWhileInside = false;
[SerializeField] private PlayerMovement _playerMovement; // required when _lockMovementWhileInside is true

private void Awake()
{
    if (_oneShotFlag == OneShotTutorialFlag.None) return;
    var gm = GameManager.Instance;
    if (gm == null) return;
    if (IsFlagSet(gm.PlayerState, _oneShotFlag))
        gameObject.SetActive(false);
}

private static bool IsFlagSet(PlayerState ps, OneShotTutorialFlag f) => f switch
{
    OneShotTutorialFlag.FirstBattle          => ps.HasCompletedFirstBattleTutorial,
    OneShotTutorialFlag.SpellTutorialBattle  => ps.HasCompletedSpellTutorialBattle,
    OneShotTutorialFlag.FirstSpikeHit        => ps.HasSeenFirstSpikeHit,
    OneShotTutorialFlag.FirstDeath           => ps.HasSeenFirstDeath,
    _                                        => false,
};
```

`OnTriggerEnter2D` additionally calls `_playerMovement.SetMovementLocked(true)` when `_lockMovementWhileInside` is true; `OnTriggerExit2D` calls `SetMovementLocked(false)`. The existing message-show/hide logic stays unchanged.

**Why `Awake`-self-disable solves the post-victory softlock:** when the player wins the spell-tutorial battle, the level reloads. `Tutorial_Advantaged.Awake` reads `HasCompletedSpellTutorialBattle == true` and disables the GameObject. The player respawns next to the (now defeated) Meltspawn with no prompt and no movement lock.

## 3. `PlayerMovement` extension

`PlayerMovement` already has `SetMovementLocked(bool)` (line 101) and `_movementLocked` is wired into `Move()` at line 78 — but it currently only zeroes **horizontal** velocity. The existing call site is `PlayerController` during attack-animation playback, which doesn't currently care about jump.

The tutorial needs a full lock: no walking and no jumping. Two options for the implementation plan:

- **(A) Expand the existing method** to also gate jump input. Per the comment on `SetMovementLocked`, the only existing caller is the attack-animation freeze, which arguably *should* also block jump for consistency. Verify in-Editor that this doesn't degrade the attack-animation feel.
- **(B) Add a sibling `SetJumpLocked(bool)`** and have the trigger call both. Strictly additive, lower regression risk.

Recommendation: start with (A) and only fall back to (B) if the attack-anim playtest reveals a problem.

Either way, no new fields beyond what `_movementLocked` already provides. Attack input — read by `PlayerExplorationAttack`, a separate component — is unaffected.

## 4. `ActionMenuUI` per-button setters

Mirrors the existing `SetSpellInteractable`:

```csharp
public void SetAttackInteractable(bool b)  { _attackButton.interactable = b; }
public void SetItemInteractable(bool b)    { _itemButton.interactable   = b; }
public void SetFleeInteractable(bool b)    { _fleeButton.interactable   = b; }
```

The existing `SetInteractable(bool)` (all-buttons) stays as-is — `BattleController` still calls it on `EnemyTurn` / `Victory` / `Defeat`. The tutorial controller layers on top: when the menu re-enables on `PlayerTurn`, the tutorial controller re-applies its own per-button restrictions immediately.

## 5. First-death tutorial

### Cross-scene-reload signal

When the player dies, `PlayerDeathHandler` calls `GameManager.RespawnAtLastCheckpoint`, which transitions the scene. The post-respawn scene needs to know "we just respawned from a death" — this signal lives on `GameManager` (DontDestroyOnLoad).

`GameManager` gains a transient (not persisted) field:

```csharp
private bool _firstDeathPromptPending;

public void NotifyDiedAndRespawning()
{
    if (!PlayerState.HasSeenFirstDeath) _firstDeathPromptPending = true;
}

public bool ConsumeFirstDeathPromptPending()
{
    bool wasPending = _firstDeathPromptPending;
    _firstDeathPromptPending = false;
    return wasPending;
}
```

`PlayerDeathHandler` calls `GameManager.Instance.NotifyDiedAndRespawning()` immediately before invoking `RespawnAtLastCheckpoint`.

### `FirstDeathPromptController`

Small MonoBehaviour placed in Level_1-1 (and any future level where this prompt should appear):

```csharp
[SerializeField] private TutorialPromptPanelUI _panel;
[SerializeField, TextArea] private string _message =
    "Defeated. You respawned at your last lit torch. Light new torches to save your progress — the first touch on each also fully restores HP and MP.";
[SerializeField] private float _displaySeconds = 6f;

private void Start()
{
    var gm = GameManager.Instance;
    if (gm == null) return;
    if (!gm.ConsumeFirstDeathPromptPending()) return;
    _panel.Show(_message);
    gm.PlayerState.MarkFirstDeathSeen();
    Invoke(nameof(Hide), _displaySeconds);
}

private void Hide() => _panel.Hide();
```

The pending-flag is consumed atomically; if the player somehow dies again before the prompt finishes, the second death respawn won't re-trigger because `HasSeenFirstDeath` is now `true`.

## 6. First spike-hit tutorial

### `HazardTrigger` extension

Add a static C# event (events are explicitly allowed by CLAUDE.md):

```csharp
public static event Action OnPlayerFirstHitFrame;
```

Fire it inside the existing first-hit damage branch of `HazardTrigger` for `HazardMode.PercentMaxHpDamage`. **Do not** fire it on DoT ticks — the prompt should map to "I just touched a spike," not "I'm still standing on it."

### `FirstSpikeHitPromptController`

Small MonoBehaviour in Level_1-1, no collider needed (event-driven):

```csharp
[SerializeField] private TutorialPromptPanelUI _panel;
[SerializeField, TextArea] private string _message =
    "Spikes hurt on touch and keep ticking while you stand on them. Move off quickly.";
[SerializeField] private float _displaySeconds = 6f;

private void OnEnable()
{
    var gm = GameManager.Instance;
    if (gm == null || gm.PlayerState.HasSeenFirstSpikeHit) return;
    HazardTrigger.OnPlayerFirstHitFrame += HandleHit;
}

private void OnDisable()
{
    HazardTrigger.OnPlayerFirstHitFrame -= HandleHit;
}

private void HandleHit()
{
    var gm = GameManager.Instance;
    if (gm == null || gm.PlayerState.HasSeenFirstSpikeHit) return;
    _panel.Show(_message);
    gm.PlayerState.MarkFirstSpikeHitSeen();
    Invoke(nameof(Hide), _displaySeconds);
    HazardTrigger.OnPlayerFirstHitFrame -= HandleHit;
}

private void Hide() => _panel.Hide();
```

The controller unsubscribes itself once the prompt fires, so subsequent spike hits don't re-trigger even within the same scene instance.

**No `Tutorial_SpikeHazard` collider needed.** The existing `HazardTrigger` components on the spike clusters already detect the contact; the static event piggybacks on that detection.

## 7. Battle tutorials

### Mode propagation

```csharp
public enum BattleTutorialMode { None, FirstBattle, SpellTutorial }
```

- `BattleEntry` (the DTO held in `GameManager.PendingBattle`, defined in `Assets/Scripts/Data/BattleEntry.cs`) gains a `BattleTutorialMode TutorialMode { get; }` property and a new optional last constructor parameter `BattleTutorialMode tutorialMode = BattleTutorialMode.None`. Existing callers that omit the argument default to `None`, so no other call sites change.
- `ExplorationEnemyCombatTrigger` gains `[SerializeField] BattleTutorialMode _tutorialMode = None;`. Wire IceSlime_01 → `FirstBattle`, Meltspawn_01 → `SpellTutorial`, all others → `None`.
- `BattleController.Start` (which already reads `pending`) calls `_tutorialController?.Setup(pending.TutorialMode)` immediately after capturing the other pending fields and before `Initialize` runs.
- `BattleTutorialController.Setup(BattleTutorialMode mode)` checks `PlayerState`: if the matching flag is already true, downgrades `mode` to `None` and self-deactivates. This handles the post-victory-respawn-into-the-trigger-zone case symmetrically with the platformer-side `Tutorial_*` triggers.

### File breakdown

| File | Type | Responsibility |
|---|---|---|
| `Battle/BattleTutorialMode.cs` | enum | None / FirstBattle / SpellTutorial |
| `Battle/BattleTutorialAction.cs` | struct | Output: `string PromptText` (`null` = no change, `""` = hide), `bool? AttackInteractable`, `bool? SpellInteractable`, `bool? ItemInteractable`, `bool? FleeInteractable`, `bool MarkComplete` |
| `Battle/BattleTutorialFlow.cs` | plain C# | Pure state machine. Methods listed below. Each returns a `BattleTutorialAction`. |
| `Battle/BattleTutorialController.cs` | MonoBehaviour | Subscribes to `BattleController` events, forwards them to the flow, applies returned actions to `ActionMenuUI` and `BattleTutorialPromptUI`. Sets the persisted flag on Victory. |
| `Battle/UI/BattleTutorialPromptUI.cs` | MonoBehaviour | `Show(string)` / `Hide()` — mirrors `TutorialPromptPanelUI`, lives in the Battle Canvas. ~25 lines. |

### `BattleTutorialFlow` API

```csharp
public class BattleTutorialFlow
{
    public BattleTutorialFlow(BattleTutorialMode mode, CombatStartState startState);

    public BattleTutorialMode Mode { get; }

    public BattleTutorialAction OnInit();
    public BattleTutorialAction OnPlayerTurnStarted();
    public BattleTutorialAction OnPlayerAttackImmune();
    public BattleTutorialAction OnPlayerAttackHit();
    public BattleTutorialAction OnSpellCast(SpellData spell);
    public BattleTutorialAction OnConditionsChanged();
    public BattleTutorialAction OnSpellCastRejected(string reason);
    public BattleTutorialAction OnBattleEnded(bool victory);
}
```

### `FirstBattle` flow (IceSlime, Surprised start state)

| Step | Triggered by | Prompt text | Buttons (A/S/I/F) |
|------|--------------|-------------|-------------------|
| 1 | `OnInit` | "The Frostbug surprised you — it acts first." | T/F/F/F |
| 2 | `OnPlayerTurnStarted` (first call only) | "Press Attack to strike." | T/F/F/F |
| 3 | `OnPlayerAttackHit` (first call only) | _hide_ | T/F/F/F |
| 4 | `OnBattleEnded(victory=true)` | _hide_ + `MarkComplete=true` | n/a |

Spell, Item, and Flee stay disabled the entire battle. After every `EnemyTurn → PlayerTurn` transition where `BattleController` re-enables the menu via `SetInteractable(true)`, the tutorial controller re-applies the per-button restriction in `OnPlayerTurnStarted`.

### `SpellTutorial` flow (Meltspawn, Advantaged start state, Liquid innate)

| Step | Triggered by | Prompt text | Buttons (A/S/I/F) |
|------|--------------|-------------|-------------------|
| 1 | `OnInit` | "This Meltspawn is Liquid — physical attacks pass right through. Try Attack to see." | T/F/F/F |
| 2 | `OnPlayerAttackImmune` | "Liquid blocks physical damage. Next turn, cast a spell." | T/F/F/F |
| 3 | `OnPlayerTurnStarted` (turn 2) | "Press Spell, then say 'Freeze' aloud." | T/**T**/F/F |
| 4 | `OnSpellCast(Freeze)` | _hide_ | n/a |
| 5 | `OnConditionsChanged` (after Freeze applies Frozen + Solid) | "Frozen — enemy skips a turn. Solid — physical attacks now hit." | T/T/F/F |
| 6 | `OnPlayerTurnStarted` (turn 3+) | "Strike while it's Solid!" | T/T/F/F |
| 7 | `OnPlayerAttackHit` (after step 6 only) | "Each spell turns the tide differently. Use the right one." | T/T/F/F |
| 8 | `OnBattleEnded(victory=true)` | _hide_ + `MarkComplete=true` | n/a |

**State-machine subtleties:**
- Step 5 fires on `OnConditionsChanged(_enemyStats)` only when the flow is in the "waiting-for-Freeze" state; subsequent condition ticks are no-ops.
- If the player casts a non-Freeze spell on turn 2, the flow shows: "That spell didn't apply Solid. Try 'Freeze' to convert Liquid → Solid." This is hypothetical safety — at the start of Level_1-1 only Freeze should be unlocked (DEV-38 spell unlock content), but the branch costs nothing and prevents a softlock.
- If the player attacks on turn 2 instead of using Spell, the flow re-fires step 2 (Liquid still blocks).
- If `OnSpellCastRejected` fires (insufficient MP), the flow shows: "Not enough MP. Defeat usually means retrying — torches restore MP." This is also hypothetical — Meltspawn balance should ensure MP never starves the tutorial — but the branch is cheap.
- Player Flee is disabled in both modes, so a flee scenario can't occur.

### Recovery: tutorial battle defeat

`OnBattleEnded(victory=false)` returns no flag-flip. The player respawns at the last torch (full HP), walks back into the same `Tutorial_Surprised` or `Tutorial_Advantaged` zone (still active because the persisted flag is false), and retries the encounter. The tutorial flow re-runs from the start — that's the correct behavior, since the player presumably needs the reminder.

### Standalone Battle scene testing

When the Battle scene is opened directly (no `GameManager`, no `PendingBattle`), `BattleController.Start` skips the `_tutorialController?.Setup(...)` call, leaving the controller inert. The Battle scene continues to function as it does today.

## 8. Editor wiring summary

### Level_1-1

| Object | Change |
|---|---|
| `Tutorial_Surprised` | `_oneShotFlag = FirstBattle` |
| `Tutorial_Advantaged` | `_oneShotFlag = SpellTutorialBattle`, `_lockMovementWhileInside = true`, assign `_playerMovement` |
| `IceSlime_01` (`ExplorationEnemyCombatTrigger`) | `_tutorialMode = FirstBattle` |
| `Meltspawn_01` (`ExplorationEnemyCombatTrigger`) | `_tutorialMode = SpellTutorial` |
| New `FirstDeathPrompt` GameObject | Add `FirstDeathPromptController`, assign existing `TutorialPromptPanel` |
| New `FirstSpikeHitPrompt` GameObject | Add `FirstSpikeHitPromptController`, assign existing `TutorialPromptPanel` |

### Data assets

| Asset | Change |
|---|---|
| `ED_Meltspawn` | Add `Liquid` to `innateConditions` (verify; add if missing) |

### Battle scene

| Object | Change |
|---|---|
| Battle Canvas | New `BattleTutorialPromptPanel` (root inactive, TMP_Text child) |
| New `BattleTutorialController` GameObject | Wire `BattleController`, `ActionMenuUI`, `BattleTutorialPromptUI` |
| `BattleController` | New `[SerializeField] BattleTutorialController _tutorialController` reference, assigned in Inspector |

## 9. Testing strategy

### New edit-mode tests — `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs`

`BattleTutorialFlow` is pure C#, so it's the highest-value test surface. One test per row of the flow tables:

**FirstBattle:**
- `FirstBattle_OnInit_ShowsSurprisedPromptAndLocksToAttack`
- `FirstBattle_OnPlayerTurnStarted_FirstCall_ShowsPressAttackPrompt`
- `FirstBattle_OnPlayerTurnStarted_SecondCall_NoChange`
- `FirstBattle_OnPlayerAttackHit_HidesPrompt`
- `FirstBattle_OnBattleEnded_Victory_MarksComplete`
- `FirstBattle_OnBattleEnded_Defeat_DoesNotMarkComplete`

**SpellTutorial:**
- `SpellTutorial_OnInit_LiquidPromptAndAttackOnly`
- `SpellTutorial_OnPlayerAttackImmune_ShowsLiquidBlocksPrompt`
- `SpellTutorial_PlayerTurn2_UnlocksSpellButtonAndPromptsToCast`
- `SpellTutorial_OnSpellCast_HidesPrompt`
- `SpellTutorial_OnConditionsChangedAfterFreeze_ShowsFrozenSolidPrompt`
- `SpellTutorial_PlayerTurn3_PromptsToAttack`
- `SpellTutorial_OnPlayerAttackHit_AfterTurn3_ShowsClosingLine`
- `SpellTutorial_OnBattleEnded_Victory_MarksComplete`
- `SpellTutorial_AttackOnTurn2_ReshowsLiquidPrompt`

### Extended tests

- `PlayerStateTests` — verify the 4 new flags default to false; `MarkXxx` methods flip them; save/load round-trips preserve them.
- `TutorialPromptTriggerTests` (new file) — `Awake` with the matching flag set disables the GameObject; `_lockMovementWhileInside` calls `SetMovementLocked` on enter and exit.

### Manual play-test checklist (for the implementation plan, not part of this spec)

1. New Game → walk through Level_1-1 → all forced prompts (Movement, HighJump, Surprised, Advantaged, IceWall) fire as before.
2. Take spike damage for the first time → spike prompt appears once → step on more spikes → no replay.
3. Die for the first time → respawn → torch prompt appears once → die again → no replay.
4. Walk into IceSlime path → Surprised battle starts → only Attack works → win → IceSlime defeated, return to platformer, `Tutorial_Surprised` is gone next reload.
5. Approach Meltspawn → enter `Tutorial_Advantaged` → movement locks → press Attack → Advantaged battle starts.
6. Spell tutorial battle: each scripted prompt fires in sequence (Liquid → Liquid blocks → Press Spell say Freeze → Frozen+Solid → Strike → Each spell unique) → win → `Tutorial_Advantaged` is gone next reload, Meltspawn is also gone (existing defeated-enemy persistence).
7. Save → quit → reload → none of the conditional tutorials replay; all forced zone prompts still fire.
8. Lose the spell-tutorial battle → respawn at torch → walk back → entire flow re-runs (this is correct).

## 10. Risks and open issues

- **`SpellVocabularyManager` content prerequisite**: the spell-tutorial battle requires Freeze to be in the player's unlocked vocabulary at the moment the second battle begins. If DEV-38 doesn't already grant Freeze on New Game (or before Level_1-1's second battle), the tutorial will softlock at "say Freeze." The implementation plan must verify Freeze is unlocked at the right point.
- **`ED_Meltspawn` innate Liquid**: must be set; the spell tutorial breaks if Meltspawn is anything else. The implementation plan confirms this in the Editor wiring step.
- **Transient pending-flag on `GameManager`**: not persisted. If the player triggers their first death and quits the app mid-respawn (before the new scene's `Start` runs), the pending flag is lost — `HasSeenFirstDeath` stays false but the prompt won't fire next launch until they die again. Acceptable: a cosmetic edge case, not a softlock.
- **`Tutorial_Advantaged` movement-lock + `OnTriggerExit2D` ordering**: when the battle is triggered by `ExplorationEnemyCombatTrigger`, the scene transitions immediately. The exit unlock is best-effort; if the scene tears down before exit fires, the lock state is moot because the next `PlayerMovement` instance starts unlocked.
