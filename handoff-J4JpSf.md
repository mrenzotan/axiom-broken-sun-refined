# Handoff: Battle Message Readability Plan

## Objective

Finish grilling the UX decisions, then create and self-review a repository-grounded Unity implementation plan for “Improve Battle Scene Message Log Readability.” Save it under `docs/superpowers/plans/2026-06-21-<feature>.md`. Do not implement gameplay code.

## Required skills

- `writing-unity-game-dev-plans`
- `grill-with-docs`
- Required planning sub-skill: `superpowers:writing-plans`
- Supporting skills already consulted: `game-development`, `unity-developer`, `2d-games`, `pc-games`

## Decisions confirmed by the user

1. Replace the passive rolling two-line history with one acknowledgment-gated battle message at a time.
2. Continue during typewriter reveal completes the current message; the next Continue advances the queue.
3. Keep the action menu visible but disabled while messages are pending. Focus a visible Continue button, then restore action-menu focus after the queue empties.
4. Pause battle turn/state progression—not only input—until required messages are acknowledged.
5. Do not queue routine “Your turn” / “Enemy’s turn” messages; retain the existing turn indicator for that information.
6. Condition application messages explain the mechanic once; later ticks are concise and name the condition. Example: “Void Wraith was Frozen! It will skip its next action.” then “Void Wraith takes 5 damage from Burning.”

The domain decision is captured in `CONTEXT.md`; do not duplicate it elsewhere unless the plan needs an explicit requirement reference.

## Repository evidence

- `Assets/Scripts/Battle/UI/StatusMessageQueue.cs` is currently a passive two-line rolling buffer.
- `Assets/Scripts/Battle/UI/StatusMessageUI.cs` immediately assigns `TMP_Text.text`; it has no reveal or acknowledgment state.
- `Assets/Scripts/Battle/UI/BattleHUD.cs` is the only battle message producer found. It posts state, damage, defeat, healing, shield, generic condition damage, rejection, immunity, Frozen action-skip, and item messages. `OnConditionsChanged` currently only refreshes badges, so condition application is not narrated.
- `Assets/Scripts/Battle/UI/ActionMenuUI.cs` already provides `SetInteractable(bool)` and clears/restores EventSystem selection.
- `Assets/Scripts/Core/TypewriterEffect.cs` is a tested plain C# typewriter service.
- `Assets/Scripts/Platformer/UI/DialogueBoxUI.cs` already implements the accepted two-stage input behavior: first input calls `SkipToEnd()`, next advances.
- `docs/game-mechanics/chemistry-spell-combat-system.md` defines Frozen as a one-turn action skip and lists the condition lifecycle/resolver invariants.
- `Assets/Scripts/Battle/Battle.asmdef` already references `Axiom.Core`, TextMeshPro, Unity UI, and Input System. Reuse the existing Battle test assembly rather than adding an asmdef.
- CodeGraph is healthy: 264 C# files indexed. Context/explore calls were already made; use exact-file reads for remaining details.
- Unity official docs/Context7 and Exa were consulted. Relevant API direction: reuse TextMeshPro and uGUI/EventSystem rather than adding packages.

## Remaining work

1. Continue `grill-with-docs` one question at a time. Likely remaining decisions:
   - Confirm dedicated panel layout: reuse current MessageLog area as a wrapped two-line narration panel with visible Continue button.
   - Obtain the Jira `DEV-###` key; it was not supplied and is required for exact UVCS check-in messages.
2. Trace the exact BattleController/BattleTurnProcessor continuation boundaries and relevant tests before plan authoring. Preserve existing animation completion sequencing; do not let unread messages overlap subsequent turn processing.
3. Decide exact file map. Prefer adapting `StatusMessageQueue`, `StatusMessageUI`, `BattleHUD`, and narrowly modifying the controller/turn orchestration. Reuse `TypewriterEffect`; avoid a second implementation.
4. Plan TDD coverage for queue ordering, reveal/advance semantics, empty-to-busy/busy-to-empty events, action lock, condition wording, Frozen explanation, and player/enemy sequencing.
5. Separate every Unity Editor action from code checkboxes. Likely scene work: resize/reconfigure MessageLog, add Continue Button, assign serialized references, and verify EventSystem navigation.
6. UVCS-only check-ins using `<type>(DEV-###): ...`; list every modified/created file and corresponding Unity-generated `.meta` files. Do not instruct manual `.meta` creation.
7. Save the plan, read it back, and run the mandatory Unity-specific self-review from the planning skill.

## Constraints

- Planning only; no feature implementation.
- MonoBehaviours handle lifecycle/wiring; queue/reveal/flow state remains plain C# and Edit Mode testable.
- Surgical changes; no unrelated UI refactor.
- No persistent message history unless the user reverses the confirmed decision.
