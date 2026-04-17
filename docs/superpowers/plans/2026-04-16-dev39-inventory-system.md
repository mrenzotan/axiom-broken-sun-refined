# DEV-39: Inventory System with Item Collection and In-Battle Item Use

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a proper `Inventory` class that tracks items by quantity, wire it through `PlayerState` and `GameManager` save/load, and replace the placeholder Item action in battle with a functional item menu that lets the player select and use consumable items (potions, ethers) during their turn.

**Architecture:**
- `Inventory` is a plain C# class in `Axiom.Core` — tracks `Dictionary<string, int>` (itemId → quantity). Replaces the raw `List<string> InventoryItemIds` on `PlayerState`.
- `ItemCatalog` is a ScriptableObject in `Axiom.Data` — maps item IDs to `ItemData` assets. BattleController holds a reference via `[SerializeField]`.
- `ItemEffectResolver` is a plain C# class in `Axiom.Battle` — applies an item's effect to `CharacterStats`. Analogous to `SpellEffectResolver`.
- `ItemMenuUI` / `ItemSlotUI` are MonoBehaviours in `Axiom.Battle` (UI lifecycle only) — display the item list and fire selection events.

**Tech Stack:** Unity 6 LTS · C# · ScriptableObjects · NUnit Edit Mode via Unity Test Framework

**Depends on:** `ItemData`, `ItemType`, `ItemEffectType`, `InventorySaveEntry` (all created in DEV-37, already present in the codebase).

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Core/Inventory.cs` | Plain C# class: tracks items by ID + quantity, import/export for save/load |
| Create | `Assets/Tests/Editor/Core/InventoryTests.cs` | Edit Mode tests for Inventory |
| Modify | `Assets/Scripts/Core/PlayerState.cs` | Replace `InventoryItemIds` with `Inventory` property |
| Modify | `Assets/Tests/Editor/Core/PlayerStateTests.cs` | Update inventory-related assertions |
| Modify | `Assets/Scripts/Core/GameManager.cs` | Use `Inventory.ToSaveEntries()` / `LoadFromSaveEntries()` in save/load |
| Modify | `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` | Update inventory-related assertions |
| Create | `Assets/Scripts/Data/ItemCatalog.cs` | ScriptableObject: holds `ItemData[]`, lookup by ID |
| Create | `Assets/Tests/Editor/Data/ItemCatalogTests.cs` | Edit Mode tests for ItemCatalog |
| Create | `Assets/Scripts/Battle/ItemUseResult.cs` | Struct: return value of ItemEffectResolver |
| Create | `Assets/Scripts/Battle/ItemEffectResolver.cs` | Plain C#: resolves item effects onto CharacterStats |
| Create | `Assets/Tests/Editor/Battle/ItemEffectResolverTests.cs` | Edit Mode tests for ItemEffectResolver |
| Create | `Assets/Scripts/Battle/UI/ItemMenuUI.cs` | MonoBehaviour: manages item list panel and slot spawning |
| Create | `Assets/Scripts/Battle/UI/ItemSlotUI.cs` | MonoBehaviour: single item button in the item list |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Replace placeholder `PlayerItem()` with item menu + use flow |
| Modify | `Assets/Scripts/Battle/UI/BattleHUD.cs` | Subscribe to item-use events |

**No new `.asmdef` files.** All code lands in existing assemblies: `Axiom.Core`, `Axiom.Data`, `Axiom.Battle`, `CoreTests`, `DataTests`, `BattleTests`.

**No new folders.** All files land in existing directories.

---

## Task 1: Inventory class (plain C# in Core)

**Files:**
- Create: `Assets/Scripts/Core/Inventory.cs`
- Create: `Assets/Tests/Editor/Core/InventoryTests.cs`

### Step 1: Write the failing tests

- [ ] **Create `Assets/Tests/Editor/Core/InventoryTests.cs`**

```csharp
using System;
using NUnit.Framework;
using Axiom.Core;
using Axiom.Data;

namespace CoreTests
{
    public class InventoryTests
    {
        // ── Add ─────────────────────────────────────────────────────────────

        [Test]
        public void Add_SingleItem_QuantityIsOne()
        {
            var inv = new Inventory();
            inv.Add("potion");
            Assert.AreEqual(1, inv.GetQuantity("potion"));
        }

        [Test]
        public void Add_SameItemTwice_QuantityIsTwo()
        {
            var inv = new Inventory();
            inv.Add("potion");
            inv.Add("potion");
            Assert.AreEqual(2, inv.GetQuantity("potion"));
        }

        [Test]
        public void Add_ExplicitQuantity_AddsCorrectAmount()
        {
            var inv = new Inventory();
            inv.Add("potion", 5);
            Assert.AreEqual(5, inv.GetQuantity("potion"));
        }

        [Test]
        public void Add_NullId_IsNoOp()
        {
            var inv = new Inventory();
            inv.Add(null);
            Assert.AreEqual(0, inv.GetAll().Count);
        }

        [Test]
        public void Add_EmptyId_IsNoOp()
        {
            var inv = new Inventory();
            inv.Add(string.Empty);
            Assert.AreEqual(0, inv.GetAll().Count);
        }

        [Test]
        public void Add_ZeroQuantity_IsNoOp()
        {
            var inv = new Inventory();
            inv.Add("potion", 0);
            Assert.AreEqual(0, inv.GetQuantity("potion"));
        }

        [Test]
        public void Add_NegativeQuantity_IsNoOp()
        {
            var inv = new Inventory();
            inv.Add("potion", -3);
            Assert.AreEqual(0, inv.GetQuantity("potion"));
        }

        [Test]
        public void Add_WhitespaceId_IsNoOp()
        {
            var inv = new Inventory();
            inv.Add("   ");
            Assert.AreEqual(0, inv.GetAll().Count);
        }

        // ── Remove ──────────────────────────────────────────────────────────

        [Test]
        public void Remove_ExistingItem_DecreasesQuantity()
        {
            var inv = new Inventory();
            inv.Add("potion", 3);
            bool removed = inv.Remove("potion");
            Assert.IsTrue(removed);
            Assert.AreEqual(2, inv.GetQuantity("potion"));
        }

        [Test]
        public void Remove_LastUnit_RemovesEntryEntirely()
        {
            var inv = new Inventory();
            inv.Add("potion");
            inv.Remove("potion");
            Assert.AreEqual(0, inv.GetQuantity("potion"));
            Assert.IsFalse(inv.HasItem("potion"));
            Assert.AreEqual(0, inv.GetAll().Count);
        }

        [Test]
        public void Remove_ItemNotPresent_ReturnsFalse()
        {
            var inv = new Inventory();
            bool removed = inv.Remove("potion");
            Assert.IsFalse(removed);
        }

        [Test]
        public void Remove_InsufficientQuantity_ReturnsFalse_NoChange()
        {
            var inv = new Inventory();
            inv.Add("potion", 2);
            bool removed = inv.Remove("potion", 5);
            Assert.IsFalse(removed);
            Assert.AreEqual(2, inv.GetQuantity("potion"));
        }

        [Test]
        public void Remove_NullId_ReturnsFalse()
        {
            var inv = new Inventory();
            Assert.IsFalse(inv.Remove(null));
        }

        [Test]
        public void Remove_ZeroQuantity_ReturnsFalse()
        {
            var inv = new Inventory();
            inv.Add("potion", 3);
            Assert.IsFalse(inv.Remove("potion", 0));
            Assert.AreEqual(3, inv.GetQuantity("potion"));
        }

        [Test]
        public void Remove_NegativeQuantity_ReturnsFalse()
        {
            var inv = new Inventory();
            inv.Add("potion", 3);
            Assert.IsFalse(inv.Remove("potion", -2));
            Assert.AreEqual(3, inv.GetQuantity("potion"));
        }

        // ── GetQuantity / HasItem ───────────────────────────────────────────

        [Test]
        public void GetQuantity_UnknownItem_ReturnsZero()
        {
            var inv = new Inventory();
            Assert.AreEqual(0, inv.GetQuantity("nonexistent"));
        }

        [Test]
        public void GetQuantity_NullId_ReturnsZero()
        {
            var inv = new Inventory();
            Assert.AreEqual(0, inv.GetQuantity(null));
        }

        [Test]
        public void HasItem_ReturnsTrueWhenPresent()
        {
            var inv = new Inventory();
            inv.Add("potion");
            Assert.IsTrue(inv.HasItem("potion"));
        }

        [Test]
        public void HasItem_ReturnsFalseWhenAbsent()
        {
            var inv = new Inventory();
            Assert.IsFalse(inv.HasItem("potion"));
        }

        // ── Clear ───────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllItems()
        {
            var inv = new Inventory();
            inv.Add("potion", 3);
            inv.Add("ether", 2);
            inv.Clear();
            Assert.AreEqual(0, inv.GetAll().Count);
            Assert.AreEqual(0, inv.GetQuantity("potion"));
        }

        // ── Save/Load ───────────────────────────────────────────────────────

        [Test]
        public void ToSaveEntries_ReturnsCorrectEntries()
        {
            var inv = new Inventory();
            inv.Add("potion", 3);
            inv.Add("ether", 1);

            InventorySaveEntry[] entries = inv.ToSaveEntries();

            Assert.AreEqual(2, entries.Length);
            var lookup = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var e in entries) lookup[e.itemId] = e.quantity;
            Assert.AreEqual(3, lookup["potion"]);
            Assert.AreEqual(1, lookup["ether"]);
        }

        [Test]
        public void ToSaveEntries_EmptyInventory_ReturnsEmptyArray()
        {
            var inv = new Inventory();
            InventorySaveEntry[] entries = inv.ToSaveEntries();
            Assert.IsNotNull(entries);
            Assert.AreEqual(0, entries.Length);
        }

        [Test]
        public void LoadFromSaveEntries_RestoresItems()
        {
            var inv = new Inventory();
            inv.Add("stale_item"); // should be cleared
            inv.LoadFromSaveEntries(new[]
            {
                new InventorySaveEntry { itemId = "potion", quantity = 2 },
                new InventorySaveEntry { itemId = "ether",  quantity = 1 }
            });

            Assert.AreEqual(0, inv.GetQuantity("stale_item"));
            Assert.AreEqual(2, inv.GetQuantity("potion"));
            Assert.AreEqual(1, inv.GetQuantity("ether"));
        }

        [Test]
        public void LoadFromSaveEntries_NullInput_ClearsInventory()
        {
            var inv = new Inventory();
            inv.Add("potion", 5);
            inv.LoadFromSaveEntries(null);
            Assert.AreEqual(0, inv.GetAll().Count);
        }

        [Test]
        public void LoadFromSaveEntries_SkipsInvalidEntries()
        {
            var inv = new Inventory();
            inv.LoadFromSaveEntries(new[]
            {
                new InventorySaveEntry { itemId = "potion", quantity = 2 },
                new InventorySaveEntry { itemId = null,     quantity = 1 },
                new InventorySaveEntry { itemId = "",       quantity = 1 },
                new InventorySaveEntry { itemId = "ether",  quantity = 0 },
                new InventorySaveEntry { itemId = "revive", quantity = -1 }
            });

            Assert.AreEqual(1, inv.GetAll().Count);
            Assert.AreEqual(2, inv.GetQuantity("potion"));
        }

        // ── Round-trip ──────────────────────────────────────────────────────

        [Test]
        public void SaveAndLoad_RoundTrip_PreservesData()
        {
            var original = new Inventory();
            original.Add("potion", 3);
            original.Add("ether", 1);

            InventorySaveEntry[] entries = original.ToSaveEntries();

            var restored = new Inventory();
            restored.LoadFromSaveEntries(entries);

            Assert.AreEqual(3, restored.GetQuantity("potion"));
            Assert.AreEqual(1, restored.GetQuantity("ether"));
            Assert.AreEqual(2, restored.GetAll().Count);
        }
    }
}
```

### Step 2: Run tests to verify they fail

Run: Unity Test Runner → Edit Mode → `CoreTests.InventoryTests`
Expected: All tests fail with `Axiom.Core.Inventory` not found (compile error).

### Step 3: Implement Inventory

- [ ] **Create `Assets/Scripts/Core/Inventory.cs`**

```csharp
using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Core
{
    public sealed class Inventory
    {
        private readonly Dictionary<string, int> _items =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public void Add(string itemId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (quantity <= 0) return;

            if (_items.TryGetValue(itemId, out int existing))
                _items[itemId] = existing + quantity;
            else
                _items[itemId] = quantity;
        }

        public bool Remove(string itemId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (quantity <= 0) return false;
            if (!_items.TryGetValue(itemId, out int existing)) return false;
            if (existing < quantity) return false;

            int remaining = existing - quantity;
            if (remaining <= 0)
                _items.Remove(itemId);
            else
                _items[itemId] = remaining;
            return true;
        }

        public int GetQuantity(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return 0;
            return _items.TryGetValue(itemId, out int qty) ? qty : 0;
        }

        public bool HasItem(string itemId) => GetQuantity(itemId) > 0;

        public IReadOnlyDictionary<string, int> GetAll() => _items;

        public void Clear() => _items.Clear();

        public InventorySaveEntry[] ToSaveEntries()
        {
            if (_items.Count == 0) return Array.Empty<InventorySaveEntry>();

            var entries = new InventorySaveEntry[_items.Count];
            int i = 0;
            foreach (KeyValuePair<string, int> kvp in _items)
                entries[i++] = new InventorySaveEntry { itemId = kvp.Key, quantity = kvp.Value };
            return entries;
        }

        public void LoadFromSaveEntries(InventorySaveEntry[] entries)
        {
            _items.Clear();
            if (entries == null) return;

            foreach (InventorySaveEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.itemId)) continue;
                if (entry.quantity <= 0) continue;
                _items[entry.itemId] = entry.quantity;
            }
        }
    }
}
```

### Step 4: Run tests to verify they pass

Run: Unity Test Runner → Edit Mode → `CoreTests.InventoryTests`
Expected: All 26 tests pass.

### Step 5: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-39): add Inventory class with quantity tracking and save/load`
  - `Assets/Scripts/Core/Inventory.cs`
  - `Assets/Scripts/Core/Inventory.cs.meta`
  - `Assets/Tests/Editor/Core/InventoryTests.cs`
  - `Assets/Tests/Editor/Core/InventoryTests.cs.meta`

---

## Task 2: Integrate Inventory into PlayerState

Replace the raw `List<string> InventoryItemIds` on `PlayerState` with the new `Inventory` object.

**Files:**
- Modify: `Assets/Scripts/Core/PlayerState.cs`
- Modify: `Assets/Tests/Editor/Core/PlayerStateTests.cs`

### Step 1: Update PlayerState

- [ ] **Modify `Assets/Scripts/Core/PlayerState.cs`**

Replace the `InventoryItemIds` property and `SetInventoryItemIds` method with an `Inventory` property:

**Remove these lines:**
```csharp
// Phase 5 (Data Layer) will replace List<string> with proper ItemData references.
public List<string> InventoryItemIds { get; }
```

**Replace with:**
```csharp
public Inventory Inventory { get; }
```

**In the constructor,** remove:
```csharp
InventoryItemIds = new List<string>();
```

**Replace with:**
```csharp
Inventory = new Inventory();
```

**Remove the entire `SetInventoryItemIds` method** (lines 111–124).

### Step 2: Update PlayerStateTests

- [ ] **Modify `Assets/Tests/Editor/Core/PlayerStateTests.cs`**

**Replace the `Constructor_InventoryIsEmpty` test:**
```csharp
[Test]
public void Constructor_InventoryIsEmpty()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    Assert.AreEqual(0, state.Inventory.GetAll().Count);
}
```

**Replace the `SetInventoryItemIds_ReplacesList` test with two new tests:**
```csharp
[Test]
public void Inventory_AddAndGetQuantity_TracksItems()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    state.Inventory.Add("potion", 2);
    state.Inventory.Add("ether");
    Assert.AreEqual(2, state.Inventory.GetQuantity("potion"));
    Assert.AreEqual(1, state.Inventory.GetQuantity("ether"));
}

[Test]
public void Inventory_Remove_DecrementsQuantity()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    state.Inventory.Add("potion", 3);
    bool removed = state.Inventory.Remove("potion");
    Assert.IsTrue(removed);
    Assert.AreEqual(2, state.Inventory.GetQuantity("potion"));
}
```

### Step 3: Run tests to verify they pass

Run: Unity Test Runner → Edit Mode → `CoreTests.PlayerStateTests`
Expected: All tests pass (including the two new inventory tests).

### Step 4: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-39): replace InventoryItemIds with Inventory on PlayerState`
  - `Assets/Scripts/Core/PlayerState.cs`
  - `Assets/Tests/Editor/Core/PlayerStateTests.cs`

---

## Task 3: Update GameManager save/load for Inventory

Replace the `BuildInventoryEntries` / `ExpandInventory` helper methods in `GameManager` with direct calls to `Inventory.ToSaveEntries()` / `LoadFromSaveEntries()`.

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

### Step 1: Update GameManager

- [ ] **Modify `Assets/Scripts/Core/GameManager.cs`**

**In `BuildSaveData()`,** replace:
```csharp
inventory = BuildInventoryEntries(PlayerState.InventoryItemIds),
```
with:
```csharp
inventory = PlayerState.Inventory.ToSaveEntries(),
```

**In `ApplySaveData()`,** replace:
```csharp
PlayerState.SetInventoryItemIds(ExpandInventory(data.inventory));
```
with:
```csharp
PlayerState.Inventory.LoadFromSaveEntries(data.inventory);
```

**Delete these two helper methods entirely** (they are no longer used):
- `BuildInventoryEntries(List<string> itemIds)` (the entire method)
- `ExpandInventory(InventorySaveEntry[] entries)` (the entire method)

Also delete the `CopyStringList` helper if it is no longer referenced anywhere else in the file. (Check: `CopyStringList` is also used for `UnlockedSpellIds` — keep it if so.)

### Step 2: Update GameManagerSaveDataTests

- [ ] **Modify `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`**

**In `ApplySaveData_UpdatesPlayerState`,** replace the inventory assertions (lines 71–74):
```csharp
Assert.AreEqual(3, _gameManager.PlayerState.InventoryItemIds.Count);
Assert.AreEqual("potion_hp", _gameManager.PlayerState.InventoryItemIds[0]);
Assert.AreEqual("potion_hp", _gameManager.PlayerState.InventoryItemIds[1]);
Assert.AreEqual("ether_mp", _gameManager.PlayerState.InventoryItemIds[2]);
```

with:
```csharp
Assert.AreEqual(2, _gameManager.PlayerState.Inventory.GetQuantity("potion_hp"));
Assert.AreEqual(1, _gameManager.PlayerState.Inventory.GetQuantity("ether_mp"));
```

**In `PersistToDisk_WritesLoadableSaveFile`,** replace:
```csharp
_gameManager.PlayerState.SetInventoryItemIds(new[] { "potion_hp", "potion_hp", "ether_mp" });
```

with:
```csharp
_gameManager.PlayerState.Inventory.Add("potion_hp", 2);
_gameManager.PlayerState.Inventory.Add("ether_mp", 1);
```

### Step 3: Run tests to verify they pass

Run: Unity Test Runner → Edit Mode → `CoreTests.GameManagerSaveDataTests`
Expected: All tests pass.

### Step 4: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-39): update GameManager save/load to use Inventory API`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

---

## Task 4: ItemCatalog ScriptableObject

A registry asset that maps item IDs to `ItemData` references. BattleController will hold a `[SerializeField]` reference to look up items when populating the item menu.

**Files:**
- Create: `Assets/Scripts/Data/ItemCatalog.cs`
- Create: `Assets/Tests/Editor/Data/ItemCatalogTests.cs`

### Step 1: Write the failing tests

- [ ] **Create `Assets/Tests/Editor/Data/ItemCatalogTests.cs`**

```csharp
using NUnit.Framework;
using Axiom.Data;
using UnityEngine;

namespace DataTests
{
    public class ItemCatalogTests
    {
        private ItemCatalog CreateCatalog(params ItemData[] items)
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.SetItemsForTests(items);
            return catalog;
        }

        private ItemData CreateItem(string itemId)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = itemId;
            item.displayName = itemId;
            return item;
        }

        [Test]
        public void TryGetItem_ExistingId_ReturnsTrue()
        {
            ItemData potion = CreateItem("potion");
            ItemCatalog catalog = CreateCatalog(potion);

            bool found = catalog.TryGetItem("potion", out ItemData result);

            Assert.IsTrue(found);
            Assert.AreEqual(potion, result);
        }

        [Test]
        public void TryGetItem_UnknownId_ReturnsFalse()
        {
            ItemCatalog catalog = CreateCatalog(CreateItem("potion"));

            bool found = catalog.TryGetItem("ether", out ItemData result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetItem_NullId_ReturnsFalse()
        {
            ItemCatalog catalog = CreateCatalog(CreateItem("potion"));

            bool found = catalog.TryGetItem(null, out ItemData result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetItem_EmptyId_ReturnsFalse()
        {
            ItemCatalog catalog = CreateCatalog(CreateItem("potion"));

            bool found = catalog.TryGetItem(string.Empty, out ItemData result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetItem_EmptyCatalog_ReturnsFalse()
        {
            ItemCatalog catalog = CreateCatalog();

            bool found = catalog.TryGetItem("anything", out ItemData result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetItem_NullItemInArray_DoesNotThrow()
        {
            ItemCatalog catalog = CreateCatalog(null, CreateItem("potion"), null);

            bool found = catalog.TryGetItem("potion", out ItemData result);

            Assert.IsTrue(found);
            Assert.IsNotNull(result);
        }

        [Test]
        public void AllItems_ReturnsAllEntries()
        {
            ItemData potion = CreateItem("potion");
            ItemData ether = CreateItem("ether");
            ItemCatalog catalog = CreateCatalog(potion, ether);

            Assert.AreEqual(2, catalog.AllItems.Count);
        }
    }
}
```

### Step 2: Run tests to verify they fail

Run: Unity Test Runner → Edit Mode → `DataTests.ItemCatalogTests`
Expected: Compile error — `ItemCatalog` does not exist.

### Step 3: Implement ItemCatalog

- [ ] **Create `Assets/Scripts/Data/ItemCatalog.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewItemCatalog", menuName = "Axiom/Data/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemData[] _items = Array.Empty<ItemData>();

        private Dictionary<string, ItemData> _lookup;

        public IReadOnlyList<ItemData> AllItems => _items;

        public bool TryGetItem(string itemId, out ItemData item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            EnsureLookup();
            return _lookup.TryGetValue(itemId, out item);
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            if (_items == null) return;
            foreach (ItemData data in _items)
            {
                if (data != null && !string.IsNullOrWhiteSpace(data.itemId))
                    _lookup[data.itemId] = data;
            }
        }

        private void OnEnable() => _lookup = null;

#if UNITY_INCLUDE_TESTS
        public void SetItemsForTests(ItemData[] items)
        {
            _items = items ?? Array.Empty<ItemData>();
            _lookup = null;
        }
#endif
    }
}
```

### Step 4: Run tests to verify they pass

Run: Unity Test Runner → Edit Mode → `DataTests.ItemCatalogTests`
Expected: All 7 tests pass.

### Step 5: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-39): add ItemCatalog ScriptableObject for item lookup by ID`
  - `Assets/Scripts/Data/ItemCatalog.cs`
  - `Assets/Scripts/Data/ItemCatalog.cs.meta`
  - `Assets/Tests/Editor/Data/ItemCatalogTests.cs`
  - `Assets/Tests/Editor/Data/ItemCatalogTests.cs.meta`

---

## Task 5: ItemEffectResolver + ItemUseResult

Plain C# resolver that applies an `ItemData` consumable's effect (RestoreHP, RestoreMP, Revive) to a `CharacterStats` target and cures any listed conditions.

**Files:**
- Create: `Assets/Scripts/Battle/ItemUseResult.cs`
- Create: `Assets/Scripts/Battle/ItemEffectResolver.cs`
- Create: `Assets/Tests/Editor/Battle/ItemEffectResolverTests.cs`

### Step 1: Write the failing tests

- [ ] **Create `Assets/Tests/Editor/Battle/ItemEffectResolverTests.cs`**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;
using UnityEngine;

namespace BattleTests
{
    public class ItemEffectResolverTests
    {
        private static CharacterStats MakeStats(int maxHp, int maxMp = 30, int atk = 10, int def = 5)
        {
            var s = new CharacterStats { MaxHP = maxHp, MaxMP = maxMp, ATK = atk, DEF = def, SPD = 5 };
            s.Initialize();
            return s;
        }

        private static ItemData MakeItem(ItemEffectType effect, int power,
            List<ChemicalCondition> cures = null)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = "test_item";
            item.displayName = "Test Item";
            item.effectType = effect;
            item.effectPower = power;
            item.curesConditions = cures ?? new List<ChemicalCondition>();
            return item;
        }

        // ── Null guards ─────────────────────────────────────────────────────

        [Test]
        public void Resolve_NullItem_Throws()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null, target));
        }

        [Test]
        public void Resolve_NullTarget_Throws()
        {
            var resolver = new ItemEffectResolver();
            var item = MakeItem(ItemEffectType.RestoreHP, 50);
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(item, null));
        }

        // ── RestoreHP ───────────────────────────────────────────────────────

        [Test]
        public void Resolve_RestoreHP_HealsTarget()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(40); // HP = 60

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreHP, 30), target);

            Assert.AreEqual(90, target.CurrentHP);
            Assert.AreEqual(ItemEffectType.RestoreHP, result.EffectType);
            Assert.AreEqual(30, result.Amount);
        }

        [Test]
        public void Resolve_RestoreHP_ClampsToMax()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(10); // HP = 90

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreHP, 50), target);

            Assert.AreEqual(100, target.CurrentHP);
            Assert.AreEqual(10, result.Amount); // only 10 HP was actually restored
        }

        // ── RestoreMP ───────────────────────────────────────────────────────

        [Test]
        public void Resolve_RestoreMP_RestoresMP()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100, maxMp: 50);
            target.SpendMP(30); // MP = 20

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreMP, 25), target);

            Assert.AreEqual(45, target.CurrentMP);
            Assert.AreEqual(ItemEffectType.RestoreMP, result.EffectType);
            Assert.AreEqual(25, result.Amount);
        }

        [Test]
        public void Resolve_RestoreMP_ClampsToMax()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100, maxMp: 50);
            target.SpendMP(5); // MP = 45

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreMP, 50), target);

            Assert.AreEqual(50, target.CurrentMP);
            Assert.AreEqual(5, result.Amount);
        }

        // ── Revive ──────────────────────────────────────────────────────────

        [Test]
        public void Resolve_Revive_OnDefeatedTarget_RestoresHP()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(100); // HP = 0, defeated
            Assert.IsTrue(target.IsDefeated);

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.Revive, 50), target);

            Assert.AreEqual(50, target.CurrentHP);
            Assert.IsFalse(target.IsDefeated);
            Assert.AreEqual(ItemEffectType.Revive, result.EffectType);
            Assert.AreEqual(50, result.Amount);
        }

        [Test]
        public void Resolve_Revive_OnAliveTarget_IsNoOp()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(30); // HP = 70, alive

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.Revive, 50), target);

            Assert.AreEqual(70, target.CurrentHP); // unchanged
            Assert.AreEqual(0, result.Amount);
        }

        // ── None ────────────────────────────────────────────────────────────

        [Test]
        public void Resolve_None_DoesNotChangeStats()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100, maxMp: 50);
            target.TakeDamage(20);

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.None, 0), target);

            Assert.AreEqual(80, target.CurrentHP);
            Assert.AreEqual(50, target.CurrentMP);
            Assert.AreEqual(0, result.Amount);
        }

        // ── Condition curing ────────────────────────────────────────────────

        [Test]
        public void Resolve_CuresConditions_RemovesListedConditions()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5, duration: 3);
            Assert.IsTrue(target.HasCondition(ChemicalCondition.Burning));

            var cures = new List<ChemicalCondition> { ChemicalCondition.Burning };
            resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target);

            Assert.IsFalse(target.HasCondition(ChemicalCondition.Burning));
        }

        [Test]
        public void Resolve_CuresConditions_IgnoresNoneCondition()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);

            var cures = new List<ChemicalCondition> { ChemicalCondition.None };
            Assert.DoesNotThrow(() =>
                resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target));
        }

        [Test]
        public void Resolve_CuresConditions_NotPresent_IsNoOp()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);

            var cures = new List<ChemicalCondition> { ChemicalCondition.Frozen };
            Assert.DoesNotThrow(() =>
                resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target));

            Assert.IsFalse(target.HasCondition(ChemicalCondition.Frozen));
        }

        [Test]
        public void Resolve_CuresMultipleConditions_RemovesAll()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5, duration: 3);
            target.ApplyStatusCondition(ChemicalCondition.Frozen, baseDamage: 5, duration: 3);
            Assert.IsTrue(target.HasCondition(ChemicalCondition.Burning));
            Assert.IsTrue(target.HasCondition(ChemicalCondition.Frozen));

            var cures = new List<ChemicalCondition>
                { ChemicalCondition.Burning, ChemicalCondition.Frozen };
            resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target);

            Assert.IsFalse(target.HasCondition(ChemicalCondition.Burning));
            Assert.IsFalse(target.HasCondition(ChemicalCondition.Frozen));
        }
    }
}
```

### Step 2: Run tests to verify they fail

Run: Unity Test Runner → Edit Mode → `BattleTests.ItemEffectResolverTests`
Expected: Compile error — `ItemEffectResolver` and `ItemUseResult` not found.

### Step 3: Implement ItemUseResult and ItemEffectResolver

- [ ] **Create `Assets/Scripts/Battle/ItemUseResult.cs`**

```csharp
using Axiom.Data;

namespace Axiom.Battle
{
    public struct ItemUseResult
    {
        public ItemEffectType EffectType;
        public int Amount;
    }
}
```

- [ ] **Create `Assets/Scripts/Battle/ItemEffectResolver.cs`**

```csharp
using System;
using Axiom.Data;

namespace Axiom.Battle
{
    public class ItemEffectResolver
    {
        public ItemUseResult Resolve(ItemData item, CharacterStats target)
        {
            if (item   == null) throw new ArgumentNullException(nameof(item));
            if (target == null) throw new ArgumentNullException(nameof(target));

            int amount = 0;

            switch (item.effectType)
            {
                case ItemEffectType.RestoreHP:
                {
                    int before = target.CurrentHP;
                    target.Heal(item.effectPower);
                    amount = target.CurrentHP - before;
                    break;
                }
                case ItemEffectType.RestoreMP:
                {
                    int before = target.CurrentMP;
                    target.RestoreMP(item.effectPower);
                    amount = target.CurrentMP - before;
                    break;
                }
                case ItemEffectType.Revive:
                {
                    if (target.IsDefeated)
                    {
                        target.Heal(item.effectPower);
                        amount = target.CurrentHP;
                    }
                    break;
                }
            }

            if (item.curesConditions != null)
            {
                foreach (ChemicalCondition condition in item.curesConditions)
                {
                    if (condition != ChemicalCondition.None)
                        target.ConsumeCondition(condition);
                }
            }

            return new ItemUseResult
            {
                EffectType = item.effectType,
                Amount     = amount
            };
        }
    }
}
```

### Step 4: Run tests to verify they pass

Run: Unity Test Runner → Edit Mode → `BattleTests.ItemEffectResolverTests`
Expected: All 13 tests pass.

### Step 5: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-39): add ItemEffectResolver and ItemUseResult for in-battle item use`
  - `Assets/Scripts/Battle/ItemUseResult.cs`
  - `Assets/Scripts/Battle/ItemUseResult.cs.meta`
  - `Assets/Scripts/Battle/ItemEffectResolver.cs`
  - `Assets/Scripts/Battle/ItemEffectResolver.cs.meta`
  - `Assets/Tests/Editor/Battle/ItemEffectResolverTests.cs`
  - `Assets/Tests/Editor/Battle/ItemEffectResolverTests.cs.meta`

---

## Task 6: ItemMenuUI and ItemSlotUI (Battle UI MonoBehaviours)

UI components for the item selection submenu. `ItemMenuUI` manages a panel that spawns `ItemSlotUI` prefab instances for each available item. MonoBehaviours handle UI lifecycle only — no game logic.

**Files:**
- Create: `Assets/Scripts/Battle/UI/ItemMenuUI.cs`
- Create: `Assets/Scripts/Battle/UI/ItemSlotUI.cs`

### Step 1: Create ItemSlotUI

- [ ] **Create `Assets/Scripts/Battle/UI/ItemSlotUI.cs`**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private Button _button;

        private ItemData _item;
        private Action<ItemData> _onSelect;

        public void Setup(ItemData item, int quantity, Action<ItemData> onSelect)
        {
            _item = item;
            _onSelect = onSelect;
            _nameText.text = item.displayName;
            _quantityText.text = $"x{quantity}";
            _button.onClick.AddListener(HandleClick);
        }

        private void HandleClick() => _onSelect?.Invoke(_item);

        private void OnDestroy() => _button.onClick.RemoveAllListeners();
    }
}
```

### Step 2: Create ItemMenuUI

- [ ] **Create `Assets/Scripts/Battle/UI/ItemMenuUI.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    public class ItemMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private ItemSlotUI _slotPrefab;
        [SerializeField] private Button _backButton;

        public event Action<ItemData> OnItemSelected;
        public event Action OnCancelled;

        private readonly List<ItemSlotUI> _activeSlots = new List<ItemSlotUI>();

        private void Awake()
        {
            _backButton.onClick.AddListener(HandleBack);
            _panel.SetActive(false);
        }

        public void Show(IReadOnlyList<(ItemData item, int quantity)> items)
        {
            ClearSlots();
            foreach ((ItemData item, int quantity) in items)
            {
                ItemSlotUI slot = Instantiate(_slotPrefab, _contentParent);
                slot.Setup(item, quantity, HandleSlotClicked);
                _activeSlots.Add(slot);
            }
            _panel.SetActive(true);
        }

        public void Hide()
        {
            _panel.SetActive(false);
            ClearSlots();
        }

        private void HandleSlotClicked(ItemData item) => OnItemSelected?.Invoke(item);

        private void HandleBack() => OnCancelled?.Invoke();

        private void ClearSlots()
        {
            foreach (ItemSlotUI slot in _activeSlots)
                if (slot != null) Destroy(slot.gameObject);
            _activeSlots.Clear();
        }

        private void OnDestroy()
        {
            _backButton.onClick.RemoveAllListeners();
            ClearSlots();
        }
    }
}
```

### Step 3: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-39): add ItemMenuUI and ItemSlotUI for battle item selection`
  - `Assets/Scripts/Battle/UI/ItemMenuUI.cs`
  - `Assets/Scripts/Battle/UI/ItemMenuUI.cs.meta`
  - `Assets/Scripts/Battle/UI/ItemSlotUI.cs`
  - `Assets/Scripts/Battle/UI/ItemSlotUI.cs.meta`

---

## Task 7: Wire BattleController and BattleHUD for item use

Replace the placeholder `PlayerItem()` on `BattleController` with a real item flow. Add item-use events and subscribe to them in `BattleHUD`.

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`

### Step 1: Add SerializeFields and events to BattleController

- [ ] **Modify `Assets/Scripts/Battle/BattleController.cs`**

**Add these fields near the other `[SerializeField]` declarations:**

```csharp
[SerializeField]
[Tooltip("Assign the ItemCatalog ScriptableObject. Required for the Item action to function.")]
private Axiom.Data.ItemCatalog _itemCatalog;

[SerializeField]
[Tooltip("Assign the ItemMenuUI component from the Battle Canvas.")]
private ItemMenuUI _itemMenuUI;
```

**Add a private field near `_resolver`:**

```csharp
private ItemEffectResolver _itemResolver;
```

**Add this event near the other event declarations:**

```csharp
/// <summary>
/// Fires when an item is used in battle. Parameters: target CharacterStats, amount of effect, effect type.
/// BattleHUD subscribes to show floating numbers and status messages.
/// </summary>
public event Action<CharacterStats, int, Axiom.Data.ItemEffectType> OnItemUsed;
```

### Step 2: Initialize and subscribe in Initialize()

- [ ] **In `BattleController.Initialize()`, after `_resolver = new SpellEffectResolver();` add:**

```csharp
_itemResolver = new ItemEffectResolver();
```

**After `_spellInputUI?.Setup(this);` add:**

```csharp
if (_itemMenuUI != null)
{
    _itemMenuUI.OnItemSelected += HandleItemSelected;
    _itemMenuUI.OnCancelled    += HandleItemCancelled;
}
```

### Step 3: Replace PlayerItem() with real implementation

- [ ] **Replace the `PlayerItem()` method entirely:**

```csharp
/// <summary>
/// Opens the item selection menu. No-op outside PlayerTurn, while processing, or
/// when ItemCatalog/ItemMenuUI are not assigned (standalone testing).
/// If the player's inventory is empty, posts a status message and returns.
/// </summary>
public void PlayerItem()
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (_isProcessingAction) return;

    if (_itemCatalog == null || _itemMenuUI == null)
    {
        Debug.Log("[Battle] Item action unavailable — ItemCatalog or ItemMenuUI not assigned.");
        return;
    }

    var gm = Axiom.Core.GameManager.Instance;
    if (gm == null)
    {
        Debug.Log("[Battle] Item action unavailable — no GameManager.");
        return;
    }

    var availableItems = new System.Collections.Generic.List<(Axiom.Data.ItemData item, int quantity)>();
    foreach (var kvp in gm.PlayerState.Inventory.GetAll())
    {
        if (kvp.Value <= 0) continue;
        if (!_itemCatalog.TryGetItem(kvp.Key, out Axiom.Data.ItemData itemData)) continue;
        if (itemData.itemType != Axiom.Data.ItemType.Consumable) continue;
        availableItems.Add((itemData, kvp.Value));
    }

    if (availableItems.Count == 0)
    {
        Debug.Log("[Battle] No usable items in inventory.");
        return;
    }

    _isProcessingAction = true;
    _itemMenuUI.Show(availableItems);
}
```

### Step 4: Add item selection and cancellation handlers

- [ ] **Add these private methods to `BattleController`:**

```csharp
private void HandleItemSelected(Axiom.Data.ItemData item)
{
    _itemMenuUI.Hide();

    var gm = Axiom.Core.GameManager.Instance;
    if (gm == null || item == null)
    {
        _isProcessingAction = false;
        return;
    }

    gm.PlayerState.Inventory.Remove(item.itemId);

    ItemUseResult result = _itemResolver.Resolve(item, _playerStats);

    OnItemUsed?.Invoke(_playerStats, result.Amount, result.EffectType);

    OnConditionsChanged?.Invoke(_playerStats);

    // Zero-damage ping so BattleHUD refreshes HP/MP bars.
    OnDamageDealt?.Invoke(_playerStats, 0, false);

    Debug.Log($"[Battle] Item used: {item.displayName} → {result.EffectType} {result.Amount}");

    _playerDamageVisualsFired = true;
    StartCoroutine(CompletePlayerAction(targetDefeated: false));
}

private void HandleItemCancelled()
{
    _itemMenuUI.Hide();
    _isProcessingAction = false;
}
```

### Step 5: Unsubscribe in OnDestroy and re-Initialize cleanup

- [ ] **In `BattleController.OnDestroy()`, add before the closing brace:**

```csharp
if (_itemMenuUI != null)
{
    _itemMenuUI.OnItemSelected -= HandleItemSelected;
    _itemMenuUI.OnCancelled    -= HandleItemCancelled;
}
```

**In `BattleController.Initialize()`, add to the cleanup block at the top (near the other unsubscription logic before re-init):**

```csharp
if (_itemMenuUI != null)
{
    _itemMenuUI.OnItemSelected -= HandleItemSelected;
    _itemMenuUI.OnCancelled    -= HandleItemCancelled;
}
```

### Step 6: Subscribe BattleHUD to item events

- [ ] **Modify `Assets/Scripts/Battle/UI/BattleHUD.cs`**

**In `Setup()`, after the existing event subscriptions, add:**

```csharp
_battleController.OnItemUsed += HandleItemUsed;
```

**In `Unsubscribe()`, add:**

```csharp
_battleController.OnItemUsed -= HandleItemUsed;
```

**Add the handler method:**

```csharp
private void HandleItemUsed(CharacterStats target, int amount, Axiom.Data.ItemEffectType effectType)
{
    if (target == _playerStats)
    {
        _partyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
        _partyHealthBar.SetMP(target.CurrentMP, target.MaxMP);
    }
    else if (target == _enemyStats)
    {
        _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
    }

    if (amount > 0 && _statToRect.TryGetValue(target, out RectTransform rect))
    {
        var numberType = effectType == Axiom.Data.ItemEffectType.RestoreMP
            ? FloatingNumberSpawner.NumberType.Shield  // blue number for MP restore
            : FloatingNumberSpawner.NumberType.Heal;   // green number for HP restore / Revive
        _floatingNumberSpawner.Spawn(rect, amount, numberType);
    }

    string effectText = effectType switch
    {
        Axiom.Data.ItemEffectType.RestoreHP => $"{target.Name} recovers {amount} HP.",
        Axiom.Data.ItemEffectType.RestoreMP => $"{target.Name} recovers {amount} MP.",
        Axiom.Data.ItemEffectType.Revive    => $"{target.Name} is revived with {amount} HP!",
        _                                   => $"{target.Name} used an item."
    };
    _statusMessageUI.Post(effectText);
}
```

### Step 7: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-39): wire BattleController and BattleHUD for in-battle item use`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/UI/BattleHUD.cs`

---

## Task 8: Unity Editor tasks — assets, prefab, and scene wiring

All steps in this task are Unity Editor work performed by the user.

### Step 1: Create ItemCatalog asset

> **Unity Editor task (user):** Right-click `Assets/Data/` → Create → Axiom → Data → Item Catalog. Name it `ItemCatalog` (matches `SpellCatalog.asset` naming). In the Inspector, drag all existing `ItemData` assets from `Assets/Data/Items/` into the `_items` array (currently: `ID_Potion`).

### Step 2: Create additional ItemData test assets

> **Unity Editor task (user):** In `Assets/Data/Items/`:
>
> 1. Right-click → Create → Axiom → Data → Item Data. Name: `ID_Ether`.
>    - `itemId`: `ether`
>    - `displayName`: `Ether`
>    - `description`: `Restores a moderate amount of MP.`
>    - `itemType`: `Consumable`
>    - `effectType`: `RestoreMP`
>    - `effectPower`: `25`
>    - `curesConditions`: (empty)
>
> 2. Add `ID_Ether` to `ItemCatalog`'s `_items` array.

### Step 3: Create ItemSlot prefab

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Battle.unity` (the Battle Canvas is already configured, so the prefab inherits matching scale / reference resolution / TMP settings).
> 2. Under the Battle Canvas, create: UI → Button (TextMeshPro). Name it `ItemSlotPrefab`.
> 3. Inside the button, add a second `TMP_Text` child for the quantity display.
> 4. Layout: name text on the left, quantity text on the right. Set the button to stretch horizontally.
> 5. Add the `ItemSlotUI` component to the root button GameObject.
> 6. Wire: `_nameText` → the name TMP_Text, `_quantityText` → the quantity TMP_Text, `_button` → the Button component.
> 7. Drag the completed GameObject into `Assets/Prefabs/UI/` to create the prefab.
> 8. Delete the instance from the Battle scene (do NOT save the scene with the temporary instance still in it).

### Step 4: Create ItemMenu panel in Battle scene

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Under the Battle Canvas, create an empty GameObject named `ItemMenuPanel`.
> 3. Add a `VerticalLayoutGroup` component. Set child alignment to Upper Center, spacing to 4.
> 4. Add a `ContentSizeFitter` (Vertical Fit: Preferred Size).
> 5. Inside `ItemMenuPanel`, create a `ScrollView` child with a Content area for item slots. Or simpler: just use `ItemMenuPanel` itself as the content parent (if you expect ≤ 10 items, scrolling is optional).
> 6. Add a "Back" button (UI → Button - TextMeshPro) as a child of `ItemMenuPanel`. Label: "Back".
> 7. Add the `ItemMenuUI` component to `ItemMenuPanel`.
> 8. Wire: `_panel` → `ItemMenuPanel` itself, `_contentParent` → the Content transform (or `ItemMenuPanel`), `_slotPrefab` → `ItemSlotPrefab` from `Assets/Prefabs/UI/`, `_backButton` → the Back button.
> 9. Set `ItemMenuPanel` inactive by default (the script activates it on show).

### Step 5: Wire BattleController references

> **Unity Editor task (user):**
>
> 1. Select the `BattleController` GameObject in the Battle scene.
> 2. Assign `ItemCatalog` to the `_itemCatalog` field.
> 3. Assign the `ItemMenuPanel` (with `ItemMenuUI` component) to the `_itemMenuUI` field.

### Step 6: Give the player starting items for testing

> **Unity Editor task (user):** Temporarily add items to the player's inventory for testing. The simplest way: after the Battle scene loads, in `BattleController.Start()` or via a debug script, call:
> ```csharp
> GameManager.Instance.PlayerState.Inventory.Add("potion", 3);
> GameManager.Instance.PlayerState.Inventory.Add("ether", 2);
> ```
> Or manually add them via the MainMenu scene's `StartNewGame()` flow. A proper item grant system (pickups, loot) is a separate story.

### Step 7: Verify end-to-end

> **Unity Editor task (user):**
>
> 1. Enter Play Mode in the Battle scene (or start from MainMenu and trigger a battle).
> 2. On PlayerTurn, click the "Item" button.
> 3. Verify: item menu appears with Potion ×3, Ether ×2.
> 4. Click "Potion". Verify: HP increases (floating green number), status message shows "Kael recovers X HP.", item menu disappears, turn advances to EnemyTurn.
> 5. On next PlayerTurn, click "Item" again. Verify: Potion now shows ×2.
> 6. Click "Back". Verify: item menu closes, action menu is usable again.
> 7. Use all potions. Verify: Potion disappears from the list when quantity reaches 0.
> 8. Use all ethers. Verify: clicking "Item" with empty inventory shows nothing (action returns immediately).

### Step 8: Final check-in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage all scene, prefab, asset, and meta files changed → Check in with message: `feat(DEV-39): wire item system in Battle scene with ItemCatalog and test assets`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Data/ItemCatalog.asset`
  - `Assets/Data/ItemCatalog.asset.meta`
  - `Assets/Data/Items/ID_Ether.asset`
  - `Assets/Data/Items/ID_Ether.asset.meta`
  - `Assets/Prefabs/UI/ItemSlotPrefab.prefab`
  - `Assets/Prefabs/UI/ItemSlotPrefab.prefab.meta`
  - Any other changed `.meta` files
