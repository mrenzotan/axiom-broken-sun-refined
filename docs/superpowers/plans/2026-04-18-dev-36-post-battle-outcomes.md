# DEV-36: Post-Battle Outcomes (XP, Loot, Level-Up, Defeat Flow) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder "straight-to-scene-transition" Victory/Defeat handling in `BattleController` with a proper post-battle sequence — compute XP + loot, apply them to `GameManager`, show a Victory screen summarising gains, chain into the existing `LevelUpPromptUI` if thresholds were crossed, then return to the Platformer. On Defeat, show a defeat screen and continue from the last save (or fall back to Main Menu when no save exists).

**Architecture:** A new pure-C# `PostBattleOutcomeService` in `Axiom.Battle` computes a `PostBattleResult` (XP total + rolled loot) from `EnemyData` and a `System.Random`. A new `PostBattleFlowController` MonoBehaviour owns the coroutine-free sequencing: it calls the service, awards XP via `GameManager.AwardXp`, grants items via `PlayerState.Inventory.Add`, shows `VictoryScreenUI`, awaits dismissal, chains into `LevelUpPromptUI.ShowIfPending`, then executes the defeat-enemy-marking + persist + scene-transition steps currently inlined in `BattleController`. On Defeat it shows `DefeatScreenUI` and calls `GameManager.TryContinueGame` (which routes to MainMenu when no save exists). Fled keeps its existing behaviour — no XP, no loot, no prompts.

**Tech Stack:** Unity 6.0.4 LTS, C# (.NET Standard 2.1), NUnit via Unity Test Framework (Edit Mode — service logic is plain C#), TextMeshPro + Unity UI (uGUI) for panels, `System.Random` for deterministic loot rolls.

---

## Project context (read before coding)

| Source | What applies to this ticket |
|--------|-----------------------------|
| Jira DEV-36 AC | Defeated enemies removed from world (already done via DEV-61); XP awarded on win; level-up prompt if threshold crossed; loot drops from `EnemyData` loot table added to `PlayerState.Inventory`; victory screen summarises XP + items; Flee grants nothing; Defeat routes to Game Over / checkpoint flow |
| `CLAUDE.md` — Non-Negotiable Code Standards | MonoBehaviours handle lifecycle/wiring only; logic lives in plain C# classes; no singletons except `GameManager`; ScriptableObject-driven data; dead code deleted (no commented-out code) |
| `docs/GAME_PLAN.md` §Phase 4 — "Post-Battle Outcomes" | This exact ticket. AC here aligns with the design doc |
| `docs/GAME_PLAN.md` §Sprite Flipping | Any new sprite child must use `Transform.localScale.x = -1` — not relevant to this ticket (UI-only) but listed for completeness |
| `docs/VERSION_CONTROL.md` | UVCS is source of truth. Commit format: `<type>(DEV-##): <short description>`. Types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test` |
| `Assets/Scripts/Battle/BattleController.cs` lines 662–683 | Current Victory/Defeat branches. DEV-36 replaces the Victory body and the Defeat body; Fled stays unchanged |
| `Assets/Scripts/Core/GameManager.cs` lines 86–94, 156–220, 285–316 | `AwardXp(int)`, `MarkEnemyDefeated`, `ClearDamagedEnemyHp`, `PersistToDisk`, `TryContinueGame` — all already exist and are exactly what the flow controller calls |
| `Assets/Scripts/Core/Inventory.cs` lines 12–21 | `Add(string itemId, int quantity)` — silently ignores null/empty IDs and non-positive quantities, so loot rolls can be passed through untransformed |
| `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs` lines 71–85 | `ShowIfPending()` + `OnDismissed` event — exposed precisely so DEV-36 can chain UI |
| `Assets/Scripts/Data/EnemyData.cs` lines 23–30 | `xpReward` and `List<LootEntry> loot` — already populated on DEV-40 asset work |
| `Assets/Scripts/Data/LootEntry.cs` | `ItemData item` + `float dropChance` (0–1) — one item per entry, rolled independently |

---

## Current state (repository)

**Already implemented:**

- `BattleState` terminal states `Victory`, `Defeat`, `Fled` are reached and handled in `BattleController.HandleStateChanged`.
- Defeated enemy removal (DEV-61): `GameManager.MarkEnemyDefeated` is called on Victory; `PlatformerWorldRestoreController.DestroyDefeatedEnemies()` runs on return and destroys them.
- XP plumbing (DEV-40): `GameManager.AwardXp(int)` applies XP, fires `OnLevelUp`, which `LevelUpPromptUI` queues per level gained. `LevelUpPromptUI.ShowIfPending()` + `OnDismissed` are shipped.
- `EnemyData.xpReward` (int, `[Min(0)]`) and `EnemyData.loot` (`List<LootEntry>`) exist on assets.
- `Inventory.Add(string itemId, int quantity = 1)` + `PlayerState.Inventory` are wired into save/load.
- `GameManager.TryContinueGame()` loads the save and transitions to the saved scene — exactly what Defeat needs.
- `BattleController.Fled` branch already skips XP/loot and transitions back to Platformer — **not touched by DEV-36**.
- A debug helper `_DevLevelUpTrigger` exists in `Assets/Scripts/Battle/UI/` (keypress L awards 100 XP, P shows pending prompt). Deleting it is noted as a DEV-40 clean-up item and is out of scope here.

**Missing (scope of DEV-36):**

- No one calls `GameManager.AwardXp` on Victory. `EnemyData.xpReward` is unread.
- No one rolls `EnemyData.loot` or calls `Inventory.Add` with the results.
- No victory screen — the Battle scene goes straight to a fade-out.
- No defeat screen — the Battle scene stalls on Defeat with the Battle HUD still visible.
- `LevelUpPromptUI.ShowIfPending()` is never called (verified by grep — only `_DevLevelUpTrigger` calls it manually).
- The scene transition on Victory runs immediately inside `BattleController.HandleStateChanged`, which leaves nowhere to insert the UI flow.

**Out of scope:**

- Multi-enemy battles. The codebase has one player vs one enemy — scope stays there.
- Post-battle music / SFX polish (Phase 7).
- Art-pass on Victory / Defeat screens — placeholder panels using the same styling patterns as `LevelUpPromptUI` are acceptable.
- Spell-unlock ceremony separate from level-up. Spells unlock inside `LevelUpPromptUI` today and that continues.
- Deleting `_DevLevelUpTrigger`. Left for a DEV-40 follow-up ticket.

**Dependencies from the Jira ticket:**

- DEV-39 Inventory API (`Inventory.Add`) — shipped.
- DEV-37 `ItemData` ScriptableObject types — shipped.

---

## Design decisions (lock these in before Task 1)

1. **XP and loot are computed separately from the UI.** A pure C# `PostBattleOutcomeService` takes `EnemyData` + `System.Random` and returns a `PostBattleResult` value type. This keeps loot-roll logic Edit-Mode testable with deterministic seeds and keeps the MonoBehaviour thin.

2. **Loot rolling is per-entry independent Bernoulli.** Each `LootEntry` rolls `random.NextDouble() < dropChance`. No weighted picks, no "exactly one drop" logic. Matches `LootEntry.dropChance` tooltip. Quantity is always 1 per dropped entry — the schema has no quantity field, adding one is out of scope.

3. **`System.Random` over `UnityEngine.Random`.** `UnityEngine.Random` is a global static that is hard to seed deterministically from Edit Mode tests and leaks state across tests. `System.Random` instances are test-friendly and already used by `PlayerActionHandler` for crit rolls — matches existing patterns.

4. **Event order on Victory:** (1) compute result → (2) award XP (fires level-up events synchronously → `LevelUpPromptUI` queues entries) → (3) grant items → (4) show Victory screen displaying XP and items → (5) on dismiss, show `LevelUpPromptUI.ShowIfPending()` → (6) on dismiss, mark enemy defeated + clear damaged-HP + persist → (7) transition to Platformer. Reasoning: the victory screen shows *what you gained in this fight*, the level-up prompt shows *the cumulative effect on your character*. Pairing XP display with the fight outcome is the classic JRPG read (FF1, Pokémon).

5. **Persist fires AFTER all UI is dismissed,** so the save captures the granted XP/items. If the player quits while the Victory screen is open, the save still reflects the pre-battle state — this is intentional: "you didn't get the loot unless you actually close the prompt" is easier to reason about than partial states.

6. **Defeat: Continue reloads last save via `GameManager.TryContinueGame()`.** If no save exists (new game, no checkpoint yet, player dies before saving), `TryContinueGame` returns false — fall back to loading the `MainMenu` scene via `SceneTransition.BeginTransition`. This is the simplest handling that keeps the player unstuck.

7. **On Defeat the battle scene does NOT persist anything.** Existing code sets `_damagedEnemyHp` in `SetDamagedEnemyHp` but does not `PersistToDisk`. DEV-36 keeps this: on Continue, `TryContinueGame → ApplySaveData` discards the in-memory damaged-HP override and restores the saved set. This is correct: dying should revert the world to the last save.

8. **Orchestration lives in a dedicated MonoBehaviour, `PostBattleFlowController`,** not inside `BattleController`. Rationale: `BattleController` is already 840+ lines coordinating animation, voice, items, and state. Post-battle sequencing is a separate concern with its own inspector wiring (Victory UI, Defeat UI, Level-Up UI refs). Separation keeps both classes below the mental-hold threshold.

9. **No feature flag.** This ticket directly replaces placeholder behaviour referenced by a `// Placeholder — DEV-37 will insert…` comment in `BattleController.cs:665`. Per CLAUDE.md §10 ("No premature abstraction") we don't ship both paths.

10. **Button input binding.** `VictoryScreenUI` and `DefeatScreenUI` use the same uGUI `Button.onClick` pattern as `LevelUpPromptUI` — no Input System action maps, no keyboard shortcuts in v1. Consistent with the existing UI layer. Mouse-click / touch-on-button only.

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Battle/ItemGrant.cs` | Plain-C# `readonly struct` — `string ItemId`, `int Quantity` |
| Create | `Assets/Scripts/Battle/PostBattleResult.cs` | Plain-C# `readonly struct` — `int Xp`, `IReadOnlyList<ItemGrant> Items` |
| Create | `Assets/Scripts/Battle/PostBattleOutcomeService.cs` | Plain-C# service — `ResolveVictory(EnemyData enemy, System.Random random) → PostBattleResult`; independent Bernoulli per loot entry; null guards |
| Create | `Assets/Scripts/Battle/UI/VictoryScreenUI.cs` | MonoBehaviour — shows a panel with "XP: +N" + item lines; exposes `Show(PostBattleResult)` and `event Action OnDismissed` |
| Create | `Assets/Scripts/Battle/UI/DefeatScreenUI.cs` | MonoBehaviour — shows a panel with "Defeated…" label + Continue button; exposes `Show()` and `event Action OnContinueClicked` |
| Create | `Assets/Scripts/Battle/PostBattleFlowController.cs` | MonoBehaviour — orchestrates the Victory / Defeat sequences described in Design Decision 4 and 6 |
| Create | `Assets/Tests/Editor/Battle/PostBattleOutcomeServiceTests.cs` | Edit Mode tests: null enemy throws; zero XP preserved; empty loot → no items; guaranteed drop returns the item; zero-chance drop → no items; mixed entries with a seeded `System.Random` produce stable output; null `LootEntry.item` is skipped; null `random` throws |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Replace the Victory / Defeat bodies in `HandleStateChanged` (~lines 662–683) with delegation to `PostBattleFlowController`; add a serialized `PostBattleFlowController` reference field; keep Fled unchanged |

**No new asmdefs.** All runtime code slots into `Axiom.Battle` (already references `Axiom.Core`, `Axiom.Data`, `Unity.TextMeshPro`, `UnityEngine.UI`, `Unity.InputSystem`). Tests go in `BattleTests` (already references `Axiom.Battle` + `Axiom.Data`).

**No new scenes.** All UI GameObjects are children of the existing Battle scene's Canvas, authored in the Unity Editor task at the end.

---

## Task 1: Create `ItemGrant` and `PostBattleResult` DTOs

**Files:**
- Create: `Assets/Scripts/Battle/ItemGrant.cs`
- Create: `Assets/Scripts/Battle/PostBattleResult.cs`

- [x] **Step 1: Create `ItemGrant.cs`**

Create `Assets/Scripts/Battle/ItemGrant.cs`:

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// A single item awarded by a post-battle loot roll.
    /// Quantity is currently fixed at 1 per rolled <see cref="Axiom.Data.LootEntry"/>;
    /// the field exists so <see cref="Axiom.Core.Inventory.Add(string, int)"/> can be
    /// called directly without a cast.
    /// </summary>
    public readonly struct ItemGrant
    {
        public string ItemId { get; }
        public int    Quantity { get; }

        public ItemGrant(string itemId, int quantity)
        {
            ItemId   = itemId;
            Quantity = quantity;
        }
    }
}
```

- [x] **Step 2: Create `PostBattleResult.cs`**

Create `Assets/Scripts/Battle/PostBattleResult.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Axiom.Battle
{
    /// <summary>
    /// Immutable output of <see cref="PostBattleOutcomeService.ResolveVictory"/>.
    /// Populated by the service from <see cref="Axiom.Data.EnemyData"/>; consumed by
    /// <see cref="PostBattleFlowController"/> to apply XP and items and drive the
    /// Victory screen.
    /// </summary>
    public readonly struct PostBattleResult
    {
        public int Xp { get; }
        public IReadOnlyList<ItemGrant> Items { get; }

        public PostBattleResult(int xp, IReadOnlyList<ItemGrant> items)
        {
            Xp    = xp;
            Items = items ?? Array.Empty<ItemGrant>();
        }
    }
}
```

- [x] **Step 3: Confirm Unity compiles cleanly**

> **Unity Editor task (user):** Switch to Unity. Wait for script compilation. Open **Window → General → Console**. Expected: zero errors, zero warnings related to the two new files.

- [x] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-36): add PostBattleResult and ItemGrant DTOs`
- `Assets/Scripts/Battle/ItemGrant.cs`
- `Assets/Scripts/Battle/ItemGrant.cs.meta`
- `Assets/Scripts/Battle/PostBattleResult.cs`
- `Assets/Scripts/Battle/PostBattleResult.cs.meta`

---

## Task 2: Create `PostBattleOutcomeService` with Edit Mode tests (TDD)

**Files:**
- Create: `Assets/Tests/Editor/Battle/PostBattleOutcomeServiceTests.cs`
- Create: `Assets/Scripts/Battle/PostBattleOutcomeService.cs`

- [x] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/PostBattleOutcomeServiceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Axiom.Battle;
using Axiom.Data;

namespace BattleTests
{
    public class PostBattleOutcomeServiceTests
    {
        private static EnemyData NewEnemy(int xp, List<LootEntry> loot = null)
        {
            var e = ScriptableObject.CreateInstance<EnemyData>();
            e.enemyName = "TestEnemy";
            e.maxHP = 10; e.maxMP = 0; e.atk = 1; e.def = 0; e.spd = 1;
            e.xpReward = xp;
            e.loot = loot ?? new List<LootEntry>();
            return e;
        }

        private static ItemData NewItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.displayName = id;
            return item;
        }

        private static LootEntry Entry(ItemData item, float dropChance) =>
            new LootEntry { item = item, dropChance = dropChance };

        [Test]
        public void ResolveVictory_NullEnemy_Throws()
        {
            var service = new PostBattleOutcomeService();
            Assert.Throws<ArgumentNullException>(
                () => service.ResolveVictory(null, new System.Random(0)));
        }

        [Test]
        public void ResolveVictory_NullRandom_Throws()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 10);
            Assert.Throws<ArgumentNullException>(
                () => service.ResolveVictory(enemy, null));
        }

        [Test]
        public void ResolveVictory_PassesThroughXpReward()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 42);

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(42, result.Xp);
        }

        [Test]
        public void ResolveVictory_ZeroXpReward_ReturnsZeroXp()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 0);

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Xp);
        }

        [Test]
        public void ResolveVictory_EmptyLoot_ReturnsEmptyItems()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 10, loot: new List<LootEntry>());

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_GuaranteedDrop_ReturnsItem()
        {
            var potion = NewItem("potion");
            var loot   = new List<LootEntry> { Entry(potion, 1f) };
            var enemy  = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("potion", result.Items[0].ItemId);
            Assert.AreEqual(1, result.Items[0].Quantity);
        }

        [Test]
        public void ResolveVictory_ZeroChanceDrop_ReturnsNothing()
        {
            var potion = NewItem("potion");
            var loot   = new List<LootEntry> { Entry(potion, 0f) };
            var enemy  = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_NullItemInEntry_IsSkipped()
        {
            var loot  = new List<LootEntry> { Entry(null, 1f) };
            var enemy = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_EntryWithEmptyItemId_IsSkipped()
        {
            var item  = NewItem(id: string.Empty);
            var loot  = new List<LootEntry> { Entry(item, 1f) };
            var enemy = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_IsDeterministicForSeed()
        {
            var potion = NewItem("potion");
            var ether  = NewItem("ether");
            var loot   = new List<LootEntry>
            {
                Entry(potion, 0.5f),
                Entry(ether,  0.5f),
            };
            var enemy  = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var a = service.ResolveVictory(enemy, new System.Random(1234));
            var b = service.ResolveVictory(enemy, new System.Random(1234));

            Assert.AreEqual(a.Items.Count, b.Items.Count);
            for (int i = 0; i < a.Items.Count; i++)
            {
                Assert.AreEqual(a.Items[i].ItemId, b.Items[i].ItemId);
                Assert.AreEqual(a.Items[i].Quantity, b.Items[i].Quantity);
            }
        }

        [Test]
        public void ResolveVictory_NullLootList_ReturnsEmptyItems()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 10);
            enemy.loot  = null;

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }
    }
}
```

- [x] **Step 2: Run the tests — confirm they fail**

> **Unity Editor task (user):** Open **Window → General → Test Runner → EditMode**, locate `PostBattleOutcomeServiceTests`, click **Run Selected**. Expected: all 11 tests fail with a `CS0246` or `The type 'PostBattleOutcomeService' could not be found` error, because the service does not exist yet.

- [x] **Step 3: Implement the service**

Create `Assets/Scripts/Battle/PostBattleOutcomeService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Pure C# service that computes post-battle rewards for a Victory outcome.
    /// Rolls <see cref="EnemyData.loot"/> entries independently using the supplied
    /// <see cref="System.Random"/> so Edit Mode tests stay deterministic.
    /// </summary>
    public sealed class PostBattleOutcomeService
    {
        /// <summary>
        /// Builds a <see cref="PostBattleResult"/> from the enemy's XP reward and loot table.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="enemy"/> or <paramref name="random"/> is null.</exception>
        public PostBattleResult ResolveVictory(EnemyData enemy, System.Random random)
        {
            if (enemy == null)  throw new ArgumentNullException(nameof(enemy));
            if (random == null) throw new ArgumentNullException(nameof(random));

            int xp = enemy.xpReward;

            var items = new List<ItemGrant>();
            List<LootEntry> loot = enemy.loot;
            if (loot == null)
                return new PostBattleResult(xp, items);

            for (int i = 0; i < loot.Count; i++)
            {
                LootEntry entry = loot[i];
                if (entry == null) continue;
                if (entry.item == null) continue;
                if (string.IsNullOrWhiteSpace(entry.item.itemId)) continue;
                if (entry.dropChance <= 0f) continue;

                if (random.NextDouble() < entry.dropChance)
                    items.Add(new ItemGrant(entry.item.itemId, 1));
            }

            return new PostBattleResult(xp, items);
        }
    }
}
```

- [x] **Step 4: Run the tests — confirm they pass**

> **Unity Editor task (user):** In Test Runner, click **Run Selected** on `PostBattleOutcomeServiceTests`. Expected: all 11 tests pass (green). If `ResolveVictory_IsDeterministicForSeed` fails, the implementation is allocating or skipping entries out of order — do not "fix" by sorting; match the iteration in the code above.

- [x] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-36): add PostBattleOutcomeService with loot roll + tests`
- `Assets/Scripts/Battle/PostBattleOutcomeService.cs`
- `Assets/Scripts/Battle/PostBattleOutcomeService.cs.meta`
- `Assets/Tests/Editor/Battle/PostBattleOutcomeServiceTests.cs`
- `Assets/Tests/Editor/Battle/PostBattleOutcomeServiceTests.cs.meta`

---

## Task 3: Create `VictoryScreenUI` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`

- [x] **Step 1: Create the MonoBehaviour**

Create `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`:

```csharp
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene panel shown after Victory. Displays XP gained and any items
    /// dropped, then fires <see cref="OnDismissed"/> when the player clicks Confirm.
    /// Driven by <see cref="PostBattleFlowController"/> — this class owns the view only.
    /// </summary>
    public class VictoryScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _xpText;
        [SerializeField] private TextMeshProUGUI _lootText;
        [SerializeField] private Button _confirmButton;

        [SerializeField]
        [Tooltip("Optional: ItemCatalog used to resolve itemId → displayName in the loot list. " +
                 "If unassigned, the raw itemId is shown.")]
        private ItemCatalog _itemCatalog;

        /// <summary>Fires exactly once when the player clicks the Confirm button.</summary>
        public event Action OnDismissed;

        public bool IsShowing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            HidePanel();
        }

        private void OnEnable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDisable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        /// <summary>
        /// Reveals the panel and renders <paramref name="result"/>. Call once per battle.
        /// </summary>
        public void Show(PostBattleResult result)
        {
            if (_titleText != null)
                _titleText.text = "VICTORY!";

            if (_xpText != null)
                _xpText.text = $"XP  +{result.Xp}";

            if (_lootText != null)
            {
                if (result.Items == null || result.Items.Count == 0)
                {
                    _lootText.text = "No items dropped.";
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Items:");
                    foreach (ItemGrant grant in result.Items)
                    {
                        string display = ResolveDisplayName(grant.ItemId);
                        sb.AppendLine($"  {display} x{grant.Quantity}");
                    }
                    _lootText.text = sb.ToString().TrimEnd();
                }
            }

            ShowPanel();
        }

        private string ResolveDisplayName(string itemId)
        {
            if (_itemCatalog != null && _itemCatalog.TryGetItem(itemId, out ItemData data))
                return string.IsNullOrEmpty(data.displayName) ? itemId : data.displayName;
            return itemId;
        }

        private void OnConfirmClicked()
        {
            HidePanel();
            OnDismissed?.Invoke();
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
```

- [x] **Step 2: Confirm Unity compiles cleanly**

> **Unity Editor task (user):** Return to Unity, wait for reload. Expected: zero errors. Warnings are not expected; fix any before moving on.

- [x] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-36): add VictoryScreenUI panel`
- `Assets/Scripts/Battle/UI/VictoryScreenUI.cs`
- `Assets/Scripts/Battle/UI/VictoryScreenUI.cs.meta`

---

## Task 4: Create `DefeatScreenUI` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Battle/UI/DefeatScreenUI.cs`

- [x] **Step 1: Create the MonoBehaviour**

Create `Assets/Scripts/Battle/UI/DefeatScreenUI.cs`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene panel shown on Defeat. Displays a "Defeated…" message and a
    /// Continue button. Fires <see cref="OnContinueClicked"/> when the player accepts.
    /// <see cref="PostBattleFlowController"/> routes the click to
    /// <see cref="Axiom.Core.GameManager.TryContinueGame"/>.
    /// </summary>
    public class DefeatScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _continueButton;

        /// <summary>Fires exactly once when the player clicks Continue.</summary>
        public event Action OnContinueClicked;

        public bool IsShowing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            HidePanel();
        }

        private void OnEnable()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClickedInternal);
        }

        private void OnDisable()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinueClickedInternal);
        }

        /// <summary>
        /// Reveals the panel. Call once per battle on Defeat.
        /// </summary>
        public void Show()
        {
            if (_titleText != null)
                _titleText.text = "DEFEATED";
            ShowPanel();
        }

        private void OnContinueClickedInternal()
        {
            HidePanel();
            OnContinueClicked?.Invoke();
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
```

- [x] **Step 2: Confirm Unity compiles cleanly**

> **Unity Editor task (user):** Return to Unity, wait for reload. Expected: zero errors.

- [x] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-36): add DefeatScreenUI panel`
- `Assets/Scripts/Battle/UI/DefeatScreenUI.cs`
- `Assets/Scripts/Battle/UI/DefeatScreenUI.cs.meta`

---

## Task 5: Create `PostBattleFlowController` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Battle/PostBattleFlowController.cs`

- [x] **Step 1: Create the orchestrator**

Create `Assets/Scripts/Battle/PostBattleFlowController.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Battle.UI;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Orchestrates the post-battle UI sequence. Sits on the BattleController
    /// GameObject (or a sibling) in the Battle scene.
    ///
    /// Victory flow:
    ///   1. Compute <see cref="PostBattleResult"/>.
    ///   2. Award XP — level-up events fire synchronously and queue in <see cref="LevelUpPromptUI"/>.
    ///   3. Grant loot items to <see cref="PlayerState.Inventory"/>.
    ///   4. Show <see cref="VictoryScreenUI"/>.
    ///   5. On dismissal, show <see cref="LevelUpPromptUI.ShowIfPending"/>.
    ///   6. On dismissal, mark enemy defeated, clear damaged-HP override, persist, transition to Platformer.
    ///
    /// Defeat flow:
    ///   1. Show <see cref="DefeatScreenUI"/>.
    ///   2. On continue, call <see cref="GameManager.TryContinueGame"/>.
    ///   3. If no save exists, transition to MainMenu.
    /// </summary>
    public class PostBattleFlowController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Assign the VictoryScreenUI from the Battle Canvas.")]
        private VictoryScreenUI _victoryScreenUI;

        [SerializeField]
        [Tooltip("Assign the DefeatScreenUI from the Battle Canvas.")]
        private DefeatScreenUI _defeatScreenUI;

        [SerializeField]
        [Tooltip("Assign the LevelUpPromptUI from the Battle Canvas (already present — DEV-40).")]
        private LevelUpPromptUI _levelUpPromptUI;

        [SerializeField]
        [Tooltip("Scene to load after Victory and after Continue. Usually Platformer.")]
        private string _returnScene = "Platformer";

        [SerializeField]
        [Tooltip("Scene to fall back to on Defeat if no save file exists. Usually MainMenu.")]
        private string _noSaveFallbackScene = "MainMenu";

        private readonly PostBattleOutcomeService _service = new PostBattleOutcomeService();
        private readonly System.Random _random = new System.Random();

        // Context snapshotted when the flow begins so the Victory branch can mark
        // the exact enemy defeated after the UI resolves.
        private EnemyData _pendingEnemy;
        private string _pendingEnemyId;

        /// <summary>
        /// Called by <see cref="BattleController"/> when <see cref="BattleState.Victory"/> is entered.
        /// </summary>
        public void BeginVictoryFlow(EnemyData enemy, string battleEnemyId)
        {
            _pendingEnemy   = enemy;
            _pendingEnemyId = battleEnemyId;

            PostBattleResult result = enemy != null
                ? _service.ResolveVictory(enemy, _random)
                : new PostBattleResult(0, Array.Empty<ItemGrant>());

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (result.Xp > 0)
                    gm.AwardXp(result.Xp);

                for (int i = 0; i < result.Items.Count; i++)
                    gm.PlayerState.Inventory.Add(result.Items[i].ItemId, result.Items[i].Quantity);
            }

            if (_victoryScreenUI == null)
            {
                Debug.LogWarning(
                    "[PostBattleFlow] _victoryScreenUI is not assigned — skipping Victory UI.", this);
                HandleVictoryScreenDismissed();
                return;
            }

            _victoryScreenUI.OnDismissed += HandleVictoryScreenDismissed;
            _victoryScreenUI.Show(result);
        }

        private void HandleVictoryScreenDismissed()
        {
            if (_victoryScreenUI != null)
                _victoryScreenUI.OnDismissed -= HandleVictoryScreenDismissed;

            if (_levelUpPromptUI == null)
            {
                HandleLevelUpPromptDismissed();
                return;
            }

            _levelUpPromptUI.OnDismissed += HandleLevelUpPromptDismissed;
            _levelUpPromptUI.ShowIfPending();
        }

        private void HandleLevelUpPromptDismissed()
        {
            if (_levelUpPromptUI != null)
                _levelUpPromptUI.OnDismissed -= HandleLevelUpPromptDismissed;

            GameManager gm = GameManager.Instance;
            if (gm != null && !string.IsNullOrEmpty(_pendingEnemyId))
            {
                gm.MarkEnemyDefeated(_pendingEnemyId);
                gm.ClearDamagedEnemyHp(_pendingEnemyId);
            }
            gm?.PersistToDisk();

            _pendingEnemy   = null;
            _pendingEnemyId = null;

            if (gm?.SceneTransition != null)
                gm.SceneTransition.BeginTransition(_returnScene, TransitionStyle.BlackFade);
            else
                SceneManager.LoadScene(_returnScene);
        }

        /// <summary>
        /// Called by <see cref="BattleController"/> when <see cref="BattleState.Defeat"/> is entered.
        /// </summary>
        public void BeginDefeatFlow()
        {
            if (_defeatScreenUI == null)
            {
                Debug.LogWarning(
                    "[PostBattleFlow] _defeatScreenUI is not assigned — continuing immediately.", this);
                HandleDefeatContinue();
                return;
            }

            _defeatScreenUI.OnContinueClicked += HandleDefeatContinue;
            _defeatScreenUI.Show();
        }

        private void HandleDefeatContinue()
        {
            if (_defeatScreenUI != null)
                _defeatScreenUI.OnContinueClicked -= HandleDefeatContinue;

            GameManager gm = GameManager.Instance;
            if (gm != null && gm.HasSaveFile())
            {
                gm.TryContinueGame();
                return;
            }

            // No save — fall back to MainMenu.
            if (gm?.SceneTransition != null)
                gm.SceneTransition.BeginTransition(_noSaveFallbackScene, TransitionStyle.BlackFade);
            else
                SceneManager.LoadScene(_noSaveFallbackScene);
        }

        private void OnDestroy()
        {
            if (_victoryScreenUI != null)
                _victoryScreenUI.OnDismissed -= HandleVictoryScreenDismissed;
            if (_levelUpPromptUI != null)
                _levelUpPromptUI.OnDismissed -= HandleLevelUpPromptDismissed;
            if (_defeatScreenUI != null)
                _defeatScreenUI.OnContinueClicked -= HandleDefeatContinue;
        }
    }
}
```

- [x] **Step 2: Confirm Unity compiles cleanly**

> **Unity Editor task (user):** Return to Unity, wait for reload. Expected: zero errors.

- [x] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-36): add PostBattleFlowController orchestrator`
- `Assets/Scripts/Battle/PostBattleFlowController.cs`
- `Assets/Scripts/Battle/PostBattleFlowController.cs.meta`

---

## Task 6: Wire `BattleController` to delegate Victory / Defeat sequencing

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [x] **Step 1: Add a serialized `PostBattleFlowController` reference field**

Open `Assets/Scripts/Battle/BattleController.cs`. Locate the block of `[SerializeField]` private fields near the top (around the existing `_itemMenuUI` field, circa line 70). After `_itemMenuUI`, add:

```csharp
        [SerializeField]
        [Tooltip("Assign the PostBattleFlowController component that owns the Victory/Defeat UI flow.")]
        private PostBattleFlowController _postBattleFlow;
```

- [x] **Step 2: Replace the Victory branch in `HandleStateChanged`**

Locate the Victory branch in `HandleStateChanged` (circa lines 662–676 — the branch beginning `else if (state == BattleState.Victory)`). Replace the **entire** Victory branch body with:

```csharp
            else if (state == BattleState.Victory)
            {
                SyncBattleHpToPlayerState();

                if (_postBattleFlow != null)
                {
                    _postBattleFlow.BeginVictoryFlow(_enemyData, _battleEnemyId);
                }
                else
                {
                    // Standalone Battle scene fallback — no orchestrator in the scene.
                    // Preserves the pre-DEV-36 direct-transition behaviour so isolated
                    // scene testing still completes.
                    if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                    {
                        GameManager.Instance.MarkEnemyDefeated(_battleEnemyId);
                        GameManager.Instance.ClearDamagedEnemyHp(_battleEnemyId);
                    }
                    GameManager.Instance?.PersistToDisk();
                    if (GameManager.Instance?.SceneTransition != null)
                        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
                    else
                        SceneManager.LoadScene("Platformer");
                }
            }
```

- [x] **Step 3: Replace the Defeat branch in `HandleStateChanged`**

Locate the Defeat branch in `HandleStateChanged` (circa lines 677–682 — the branch beginning `else if (state == BattleState.Defeat)`). Replace the **entire** Defeat branch body with:

```csharp
            else if (state == BattleState.Defeat)
            {
                SyncBattleHpToPlayerState();
                if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                    GameManager.Instance.SetDamagedEnemyHp(_battleEnemyId, _enemyStats.CurrentHP);

                if (_postBattleFlow != null)
                    _postBattleFlow.BeginDefeatFlow();
            }
```

- [x] **Step 4: Confirm Unity compiles cleanly**

> **Unity Editor task (user):** Return to Unity, wait for reload. Expected: zero errors.

- [x] **Step 5: Confirm existing Battle tests still pass**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode. Run the full `BattleTests` assembly. Expected: every test green. `BattleManagerTests`, `PlayerActionHandlerTests`, `EnemyActionHandlerTests`, `BattleAnimationServiceTests`, `ItemEffectResolverTests`, `SpellEffectResolverTests`, `CharacterStatsTests`, and the new `PostBattleOutcomeServiceTests` must all pass — the controller edits are additive and should not affect them.

- [x] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-36): delegate Victory/Defeat to PostBattleFlowController`
- `Assets/Scripts/Battle/BattleController.cs`

---

## Task 7: Scene wiring — Battle scene UI GameObjects

**Files:**
- Modify: `Assets/Scenes/Battle.unity` (done via Unity Editor)

> This task is 100% Unity Editor work. No C# edits.

### Visual design reference

Match the existing `LevelUpPromptPanel` (already in `Assets/Scenes/Battle.unity`) for font family, sort order, and overall style. Both new panels are modal overlays on top of the Battle HUD.

**Shared structure (both panels):**

```
Battle Canvas (existing — Screen Space Overlay)
└── VictoryScreenPanel   (or DefeatScreenPanel)
    ├── DimOverlay       — Image, full-screen dim
    └── PanelFrame       — Image, centered card
        ├── TitleText
        ├── <body widgets>
        └── ConfirmButton (or ContinueButton)
```

- **Root panel RectTransform:** anchor preset stretch-both, Left/Right/Top/Bottom = 0 (fills the canvas).
- **DimOverlay:** child Image, stretch-both with zero insets, Color `(0, 0, 0, 0.55)` alpha (`0.75` for Defeat), `raycastTarget = true` so clicks don't fall through to the Battle HUD.
- **PanelFrame:** anchor middle-center, pivot `(0.5, 0.5)`, size `640 × 420` for Victory, `480 × 260` for Defeat. Base color dark slate `(18, 22, 34, 235)`. Add an `Outline` component (1–2 px) or use a 9-sliced border sprite from `Assets/Art/UI/` if one exists.
- **Layout:** put a `Vertical Layout Group` on `PanelFrame` with padding 24/24/24/24, spacing 12, `childControlWidth = true`, `childForceExpandHeight = false`. Avoids pixel-tweaking each child.

#### VictoryScreenPanel (640 × 420)

| Child | Type | Font / Color | Size hint | Text |
|---|---|---|---|---|
| `TitleText` | TMP-Text | Bold, 56 pt, gold `#F4C542`, center-aligned | preferred height ~72 | `VICTORY!` |
| `XpText` | TMP-Text | SemiBold, 30 pt, white, center-aligned | preferred height ~40 | `XP +0` (script rewrites) |
| `Separator` *(optional)* | Image | line, `#3A4354`, 2 px tall | preferred height 2 | — |
| `LootText` | TMP-Text | Regular, 24 pt, `#CFD6E6`, left-aligned, line height 1.1 | `flexibleHeight = 1`, min height 120 | placeholder `Items:` (script rewrites) |
| `ConfirmButton` | TMP-Button | Bold, 24 pt, white on `#2E7D32` (hover `#388E3C`), rounded | fixed 180 × 48 | `Continue` |

- Add `Content Size Fitter` (Vertical = Preferred) on `LootText` so multi-item rewards don't clip.
- `ConfirmButton` Navigation = Automatic; keyboard Enter/Space is consistent with `LevelUpPromptUI`'s confirm pattern.
- No `ScrollRect` needed at this scope — DEV-36 enemies drop 1–3 items.

**Feel:** gold title + white body, JRPG standard. Keep margins generous — panel should not fill the screen; the dim backdrop does that work.

#### DefeatScreenPanel (480 × 260)

| Child | Type | Font / Color | Size hint | Text |
|---|---|---|---|---|
| `TitleText` | TMP-Text | Bold, 64 pt, crimson `#C23B22`, center-aligned, letter-spacing +4 | preferred height 90 | `DEFEATED` (script sets this) |
| `Subtitle` *(optional, static)* | TMP-Text | Regular, 20 pt, `#9BA3B1`, center-aligned | preferred height 28 | `Return to last checkpoint?` |
| `ContinueButton` | TMP-Button | SemiBold, 22 pt, white on `#37415A` (hover `#4A556F`) | fixed 180 × 48 | `Continue` |

- No `NewGame` / `Quit` buttons in v1 — scope stops at the Continue path. `TryContinueGame()` + MainMenu fallback covers every case.
- Omit `Subtitle` if you prefer the plan's literal minimalism.

**Feel:** somber single-beat. Heavier dim overlay + short message + one button.

#### Hierarchy / render-order notes

- Make both new panels **siblings of `LevelUpPromptPanel`** under the Battle Canvas, and place them **after** it in the sibling list so they render on top when active. Same `sortingOrder`.
- Both buttons rely on the Canvas's existing `GraphicRaycaster` (already present for Battle HUD).
- Deactivate each panel GameObject in the Inspector immediately after wiring. `Awake()` in each script defensively hides them too, but starting inactive prevents a 1-frame flash on scene load.

---

- [x] **Step 1: Open the Battle scene**

> **Unity Editor task (user):** In the Project window, double-click `Assets/Scenes/Battle.unity` so it becomes the active scene.

- [x] **Step 2: Create the VictoryScreenUI panel**

> **Unity Editor task (user):**
>
> **2a. Create the root + dim overlay + frame skeleton**
>
> 1. In the Hierarchy, locate the existing Battle Canvas (parent of `BattleHUD`, `ActionMenuUI`, `LevelUpPromptPanel`, etc.).
> 2. Right-click the Canvas → **UI → Panel**. Rename to `VictoryScreenPanel`. On its RectTransform: anchor preset **stretch-both**, Left/Right/Top/Bottom = 0. Delete the default Image component that Unity added, then re-add an empty `RectTransform`-only GameObject feel by removing the Image — or leave the Image and set its Color alpha to 0 (the panel root just needs to exist; dimming is done by the child below).
> 3. Right-click `VictoryScreenPanel` → **UI → Image**. Rename to `DimOverlay`. RectTransform: stretch-both, zero insets. Image Color = `(0, 0, 0, 0.55)`. `Raycast Target = true`.
> 4. Right-click `VictoryScreenPanel` → **UI → Image**. Rename to `PanelFrame`. RectTransform: anchor **middle-center**, pivot `(0.5, 0.5)`, **Width 640, Height 420**. Image Color = `(18, 22, 34, 235)`. *(Optional: Add Component → **Outline**, Effect Color dark grey, Distance `(1, -1)`; or replace the Image sprite with a 9-sliced border from `Assets/Art/UI/` if available.)*
> 5. Select `PanelFrame` → Add Component → **Vertical Layout Group**. Padding Left/Right/Top/Bottom = **24**, Spacing = **12**, `Child Control Width = true`, `Child Control Height = false`, `Child Force Expand Width = true`, `Child Force Expand Height = false`.
>
> **2b. Add the children of `PanelFrame` (in this sibling order — the Vertical Layout Group stacks them top-to-bottom):**
>
> 1. Right-click `PanelFrame` → **UI → Text - TextMeshPro**. Rename to `TitleText`. Text = `VICTORY!`. Font Style = **Bold**, Size = **56**, Color = `#F4C542` (gold), Alignment = **Center / Middle**. Add `Layout Element`: `Preferred Height = 72`.
> 2. Right-click `PanelFrame` → **UI → Text - TextMeshPro**. Rename to `XpText`. Text = `XP +0`. Font Style = **SemiBold**, Size = **30**, Color white, Alignment = **Center / Middle**. `Layout Element`: `Preferred Height = 40`.
> 3. *(Optional)* Right-click `PanelFrame` → **UI → Image**. Rename to `Separator`. Color = `#3A4354`. `Layout Element`: `Preferred Height = 2`.
> 4. Right-click `PanelFrame` → **UI → Text - TextMeshPro**. Rename to `LootText`. Text = `Items:`. Font Style = **Regular**, Size = **24**, Color = `#CFD6E6`, Alignment = **Left / Top**, Line Spacing = **10**. `Layout Element`: `Flexible Height = 1`, `Min Height = 120`. Add Component → **Content Size Fitter** (Vertical Fit = **Preferred Size**) so multi-item rewards don't clip.
> 5. Right-click `PanelFrame` → **UI → Button - TextMeshPro**. Rename to `ConfirmButton`. Set its child `Text (TMP)` to label `Continue`, Size **24**, Bold. Button Image Color = `#2E7D32` (Normal), `#388E3C` (Highlighted). `Layout Element`: `Preferred Width = 180`, `Preferred Height = 48`. On the `ConfirmButton` RectTransform, set `Horizontal Alignment` via the Vertical Layout Group's `Child Alignment` if you want it centered — or wrap it in an empty `ButtonRow` GameObject with `Horizontal Layout Group (Child Alignment = Middle Right)` and move the Button inside.
>
> **2c. Attach the component and wire references**
>
> 1. Select `VictoryScreenPanel`. Click **Add Component → Axiom.Battle.UI → VictoryScreenUI**.
> 2. In the Inspector on `VictoryScreenUI`:
>    - `_panel`        → drag `VictoryScreenPanel` itself
>    - `_titleText`    → drag `TitleText` *(skip if you replace Title with a sprite banner — see "Sprite title option" below)*
>    - `_xpText`       → drag `XpText`
>    - `_lootText`     → drag `LootText`
>    - `_confirmButton` → drag `ConfirmButton`
>    - `_itemCatalog`  → drag the `ItemCatalog` asset assigned on `BattleController` (typically `Assets/Data/Items/IC_Items.asset`). If unassigned, the panel falls back to raw item IDs.
> 3. Deactivate `VictoryScreenPanel` (uncheck its checkbox in the Inspector). `Awake()` hides it too, but starting inactive prevents a 1-frame flash on scene load.
>
> **Sprite title option (optional):** if you have a pre-authored `victory_banner.png` (Aseprite / Photoshop / Canva), replace Step 2b.1 — instead add `PanelFrame → UI → Image` named `TitleImage`, assign the sprite, and leave `VictoryScreenUI._titleText` **unassigned**. The script's `if (_titleText != null)` guard skips the text render, and the sprite shows unmodified. See *Visual design reference* above for the rationale.

- [x] **Step 3: Create the DefeatScreenUI panel**

> **Unity Editor task (user):**
>
> **3a. Create the root + dim overlay + frame skeleton**
>
> 1. On the same Canvas, right-click → **UI → Panel**. Rename to `DefeatScreenPanel`. RectTransform: stretch-both, zero insets. (Same root treatment as `VictoryScreenPanel`.)
> 2. Right-click `DefeatScreenPanel` → **UI → Image**. Rename to `DimOverlay`. Stretch-both, zero insets. Image Color = `(0, 0, 0, 0.75)` (heavier dim than Victory to sell the somber tone). `Raycast Target = true`.
> 3. Right-click `DefeatScreenPanel` → **UI → Image**. Rename to `PanelFrame`. Anchor middle-center, pivot `(0.5, 0.5)`, **Width 480, Height 260**. Color = `(18, 22, 34, 235)`, same outline/9-slice treatment as Victory.
> 4. Add Component → **Vertical Layout Group** on `PanelFrame`. Padding 24/24/24/24, Spacing 12, `Child Control Width = true`, `Child Force Expand Width = true`, `Child Force Expand Height = false`, `Child Alignment = Middle Center`.
>
> **3b. Add the children of `PanelFrame`:**
>
> 1. Right-click `PanelFrame` → **UI → Text - TextMeshPro**. Rename to `TitleText`. Text = `DEFEATED` (the script rewrites this on `Show()`). Font Style = **Bold**, Size = **64**, Color = `#C23B22` (crimson), Alignment = **Center / Middle**, Character Spacing = **+4**. `Layout Element`: `Preferred Height = 90`.
> 2. *(Optional)* Right-click `PanelFrame` → **UI → Text - TextMeshPro**. Rename to `Subtitle`. Static text `Return to last checkpoint?`. Regular, Size **20**, Color `#9BA3B1`, Alignment Center / Middle. `Layout Element`: `Preferred Height = 28`. Omit if you prefer the minimalist look.
> 3. Right-click `PanelFrame` → **UI → Button - TextMeshPro**. Rename to `ContinueButton`. Child Text label = `Continue`, Size **22**, SemiBold. Button Image Color = `#37415A` (Normal), `#4A556F` (Highlighted). `Layout Element`: `Preferred Width = 180`, `Preferred Height = 48`.
>
> **3c. Attach the component and wire references**
>
> 1. Select `DefeatScreenPanel`. Click **Add Component → Axiom.Battle.UI → DefeatScreenUI**.
> 2. In the Inspector on `DefeatScreenUI`:
>    - `_panel`          → drag `DefeatScreenPanel` itself
>    - `_titleText`      → drag `TitleText` *(skip if using a sprite `DEFEATED` banner — same optional pattern as Victory)*
>    - `_continueButton` → drag `ContinueButton`
> 3. Deactivate `DefeatScreenPanel`.
>
> **Sprite title option (optional):** same pattern as Victory — swap the `TitleText` TMP child for a `TitleImage` Image with a pre-authored `defeated_banner.png`, and leave `_titleText` unassigned.

- [x] **Step 4: Attach `PostBattleFlowController` to the BattleController GameObject**

> **Unity Editor task (user):**
> 1. Select the GameObject holding `BattleController` (usually `BattleController` at the scene root).
> 2. Click **Add Component → Axiom.Battle → PostBattleFlowController**.
> 3. Fill the new component's fields:
>    - `_victoryScreenUI`   → drag the `VictoryScreenPanel` (the component, not the GameObject — the drag picks up the `VictoryScreenUI` script automatically)
>    - `_defeatScreenUI`    → drag the `DefeatScreenPanel`
>    - `_levelUpPromptUI`   → drag the existing `LevelUpPromptPanel` (already present from DEV-40)
>    - `_returnScene`       → `Platformer`
>    - `_noSaveFallbackScene` → `MainMenu`
> 4. On the same GameObject, find the `BattleController` component. Its new `_postBattleFlow` field should autocomplete; drag the `PostBattleFlowController` component onto it.

- [x] **Step 5: Save the scene**

> **Unity Editor task (user):** Ctrl+S (or File → Save). Confirm `Battle.unity` now shows as a changed asset in UVCS Pending Changes.

- [x] **Step 6: Check in via UVCS**

> **Unity Version Control (via Unity):** Pending Changes → stage `Assets/Scenes/Battle.unity` → Check in with message: `feat(DEV-36): wire VictoryScreenUI, DefeatScreenUI, and PostBattleFlowController into Battle scene`

---

## Task 8: Populate loot on an existing EnemyData asset for smoke test

**Files:**
- Modify: a chosen `EnemyData` asset under `Assets/Data/Enemies/` (done via Unity Editor)

> Purpose: the victory screen and loot grant paths cannot be exercised end-to-end without at least one enemy that drops something. This task wires a deterministic "guaranteed drop" entry so the smoke test in Task 9 sees a predictable result.

- [x] **Step 1: Pick the test enemy and add a loot entry**

> **Unity Editor task (user):**
> 1. Project window → `Assets/Data/Enemies/` → pick whichever enemy data asset is currently used by the Battle scene's `BattleController._enemyData` inspector field (confirm by selecting `BattleController` first). If that field is currently unassigned, pick any enemy under that folder, then assign it to `BattleController._enemyData`.
> 2. With the asset selected, confirm `xpReward` is non-zero. If zero, set it to `25` for the smoke test.
> 3. In the **Loot** list, click `+` to add one entry. Set:
>    - `item` → any existing `ItemData` under `Assets/Data/Items/` (Potion is fine if present)
>    - `dropChance` → `1` (guaranteed)
> 4. Save the project (Ctrl+S).

- [x] **Step 2: Check in via UVCS**

> **Unity Version Control:** Pending Changes → stage the modified `.asset` file(s) under `Assets/Data/Enemies/` (and `Assets/Scenes/Battle.unity` if you reassigned `_enemyData`) → Check in with message: `chore(DEV-36): add guaranteed loot entry for post-battle smoke test`

---

## Task 9: Manual Play Mode smoke test

> Purpose: verify the Victory chain (XP → loot → Victory screen → Level-up prompt → scene transition) and the Defeat chain (Defeat screen → Continue → restore). No code edits.

- [x] **Step 1: Arm for a Victory run**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Platformer.unity`. Confirm a `GameManager` prefab is present in the scene root. (If not, open the Main Menu first and click "New Game" in Play Mode before returning — this instantiates the persistent GameManager.)
> 2. If you have an existing save, save a fresh one so Continue has a known state (trigger a Checkpoint in the Platformer).
> 3. Walk into an enemy on the map to trigger a battle — make sure the enemy you walk into is the one whose `EnemyData` you modified in Task 8.

- [x] **Step 2: Play through Victory**

> **Unity Editor task (user):**
> 1. In the battle, defeat the enemy (use Attack repeatedly until its HP reaches 0).
> 2. **Expected:** BattleController transitions to Victory → Victory panel appears with `XP +25` and the item you added in Task 8 (e.g. `Potion x1`).
> 3. Click Continue on the Victory panel.
> 4. **Expected:** if the XP crossed a level threshold, the Level-Up panel appears; dismiss each pending level. Otherwise the Level-Up panel does not appear.
> 5. **Expected:** the Battle scene fades out via `BlackFade` and the Platformer scene loads. The enemy you just defeated is **not** present in the world (it was destroyed by `PlatformerWorldRestoreController`).
> 6. Open the Inventory (or check `GameManager.Instance.PlayerState.Inventory` in the Console if an inspector is not wired): the rewarded item is present with the correct quantity.

- [x] **Step 3: Play through Defeat**

> **Unity Editor task (user):**
> 1. Re-enter Play Mode. Reduce player HP via a strong enemy or edit `CharacterData` baseMaxHP temporarily so you can lose on purpose. Trigger a battle.
> 2. Allow the enemy to defeat you.
> 3. **Expected:** BattleController transitions to Defeat → Defeat panel appears with "DEFEATED" and a Continue button.
> 4. Click Continue.
> 5. **Expected:** `GameManager.TryContinueGame()` loads the save, transitions back to the saved scene, and restores the saved HP/MP/position. The defeated-enemy override is discarded — the enemy still exists in the world at full HP since it pre-dates the save.
> 6. Exit Play Mode. Revert any temporary stat edits.

- [x] **Step 4: Smoke test — no-save Defeat fallback**

> **Unity Editor task (user):**
> 1. From the Main Menu, click New Game but do **not** activate any checkpoint yet. Immediately walk into an enemy and lose on purpose.
> 2. **Expected:** Defeat panel → Continue → the game falls back to the MainMenu scene (rather than crashing or stalling), because `GameManager.HasSaveFile()` returns false.

- [x] **Step 5: Commit any scene tweaks from smoke testing**

> If the scene was dirtied by incidental edits (sprite renderer toggled, panel size adjusted, etc.), **UVCS → Pending Changes → Check in** with message: `chore(DEV-36): smoke-test scene touch-ups`. If nothing was changed, skip this step.

---

## Self-review

After all tasks are complete, run this checklist against the spec:

1. **Spec coverage audit (AC-by-AC):**
   - ✔ Defeated enemies removed from Platformer world: no new code — relies on shipped `GameManager.MarkEnemyDefeated` + `PlatformerWorldRestoreController.DestroyDefeatedEnemies`. Task 5 + Task 6 ensure the mark-defeated call still fires after the new UI flow.
   - ✔ XP awarded after win: Task 5 `PostBattleFlowController.BeginVictoryFlow` calls `GameManager.AwardXp(result.Xp)`.
   - ✔ Level-up prompt on threshold crossing: Task 5 `HandleVictoryScreenDismissed` calls `LevelUpPromptUI.ShowIfPending()`.
   - ✔ Loot drops from `EnemyData` loot table (no hardcoded loot): Task 2 service iterates `EnemyData.loot`; Task 5 grants via `Inventory.Add`. No item IDs appear in `BattleController` or `PostBattleFlowController`.
   - ✔ Victory screen displays XP + items: Task 3 `VictoryScreenUI.Show(PostBattleResult)`.
   - ✔ Flee grants no XP or loot: Task 6 leaves the Fled branch untouched — existing behaviour preserved.
   - ✔ Defeat leads to Game Over / checkpoint flow: Task 4 + Task 5 `BeginDefeatFlow` + `HandleDefeatContinue` route through `GameManager.TryContinueGame` with MainMenu fallback.
   - ✔ Dependencies: DEV-37 (`ItemData`) and DEV-39 (`Inventory.Add`) are both already shipped; no changes needed.

2. **Placeholder scan:** no "TBD", no "similar to Task N", every code block is complete, every command explicit.

3. **Type consistency:**
   - `PostBattleResult(int xp, IReadOnlyList<ItemGrant> items)` — same signature in Task 1 and called with this exact shape in Task 2 and Task 5. ✔
   - `ItemGrant(string itemId, int quantity)` — consistent across Tasks 1, 2, 5. ✔
   - `VictoryScreenUI.Show(PostBattleResult)` + `event Action OnDismissed` — consistent between Tasks 3 and 5. ✔
   - `DefeatScreenUI.Show()` + `event Action OnContinueClicked` — consistent between Tasks 4 and 5. ✔
   - `PostBattleOutcomeService.ResolveVictory(EnemyData, System.Random)` — consistent between Tasks 2 and 5. ✔
   - `LevelUpPromptUI.ShowIfPending()` + `OnDismissed` — pre-existing from DEV-40, consumed unchanged in Task 5. ✔

4. **Guard-clause ordering (Task 2 `ResolveVictory`):** null enemy before null random — both are parameter guards, neither has an early-exit branch that bypasses the other. Inside the loop: null-entry → null-item → empty-itemId → zero-chance → roll. Each subsequent check depends on the previous being true, and no branch skips an earlier guard. ✔

5. **Test coverage audit (Task 2):**
   - Null enemy → throws ✔
   - Null random → throws ✔
   - Zero XP preserved ✔
   - Non-zero XP passed through ✔
   - Empty loot → no items ✔
   - Null loot list → no items ✔
   - Guaranteed drop → item granted ✔
   - Zero-chance drop → nothing ✔
   - Null item in entry → skipped ✔
   - Empty itemId → skipped ✔
   - Deterministic for seed ✔

6. **UVCS staged-file audit:** every `.cs` file paired with its `.cs.meta`. Task 7 stages `Battle.unity` (the only non-script asset touched). Task 8 stages the `.asset` enemy data. Task 6 modifies `BattleController.cs` only (no meta change). ✔

7. **Unity Editor task isolation:** every Editor action is inside a `> **Unity Editor task (user):**` callout and never mixed with a code step in the same checkbox. ✔
