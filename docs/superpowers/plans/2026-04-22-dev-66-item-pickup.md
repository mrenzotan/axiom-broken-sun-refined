# DEV-66 Item Pickup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** Platformer-scene pickup entity that adds an item to `PlayerState.Inventory` on collect and despawns per session (and across saves).

**Architecture:** Mirror the existing `_defeatedEnemyIds` world-state preservation pattern exactly. A plain C# `ItemPickupController` handles inventory grant logic; an `ItemPickup` MonoBehaviour handles trigger detection, guards, and self-destruction. Save/load round-trips through `SaveData.collectedPickupIds`.

**Tech Stack:** Unity 6 LTS, C#, URP 2D, Unity Test Framework (Edit Mode)

---

## Context

- Jira: **DEV-66** — `Implement world pickup interactables that grant items to the player inventory`
- Labels: `phase-6-world`, `unity`
- User has already created animation clips + animator controllers at `Assets/Animations/Items/Round Potion/`
- Existing pattern to mirror: `GameManager._defeatedEnemyIds` (DEV-35)

---

### Task 1: Extend GameManager with pickup collection tracking

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

**Step 1: Add `_collectedPickupIds` and public API**

Add the following immediately after the `_damagedEnemyHp` dictionary declaration in `GameManager.cs`:

```csharp
        private readonly HashSet<string> _collectedPickupIds =
            new HashSet<string>(StringComparer.Ordinal);
```

Add the following public API immediately after `ClearAllDamagedEnemyHp()`:

```csharp
        public bool IsPickupCollected(string pickupId) =>
            !string.IsNullOrEmpty(pickupId) && _collectedPickupIds.Contains(pickupId);

        public void MarkPickupCollected(string pickupId)
        {
            if (!string.IsNullOrEmpty(pickupId))
                _collectedPickupIds.Add(pickupId);
        }

        public void ClearCollectedPickups() => _collectedPickupIds.Clear();

        public IEnumerable<string> CollectedPickupIds => _collectedPickupIds;

        public void RestoreCollectedPickups(IEnumerable<string> pickupIds)
        {
            _collectedPickupIds.Clear();
            if (pickupIds == null) return;

            foreach (string id in pickupIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _collectedPickupIds.Add(id);
            }
        }
```

**Step 2: Clear pickups on new game**

In `StartNewGame()`, add `ClearCollectedPickups();` after `ClearAllDamagedEnemyHp();`.

**Step 3: Verify compilation**

> **Unity Editor task (user):** Open Unity Editor and confirm no compile errors in the Console.

---

### Task 2: Extend SaveData with collected pickup IDs

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`

**Step 1: Add `collectedPickupIds` field**

Add the following after `damagedEnemyHp`:

```csharp
        public string[] collectedPickupIds = Array.Empty<string>();
```

**Step 2: Verify compilation**

> **Unity Editor task (user):** Confirm no compile errors.

---

### Task 3: Wire save/load round-trip for pickup IDs in GameManager

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

**Step 1: Include pickup IDs in `BuildSaveData()`**

In the `SaveData` initializer inside `BuildSaveData()`, add after `damagedEnemyHp`:

```csharp
                collectedPickupIds = CopyHashSet(_collectedPickupIds),
```

**Step 2: Restore pickup IDs in `ApplySaveData()`**

In `ApplySaveData()`, add after `RestoreDamagedEnemyHp(data.damagedEnemyHp);`:

```csharp
            RestoreCollectedPickups(data.collectedPickupIds);
```

**Step 3: Verify compilation**

> **Unity Editor task (user):** Confirm no compile errors.

---

### Task 4: Create `ItemPickupController` plain C# logic class

**Files:**
- Create: `Assets/Scripts/Platformer/ItemPickupController.cs`

**Step 1: Write the class**

```csharp
using System;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// Plain C# logic for granting an item to the player's inventory.
    /// </summary>
    public sealed class ItemPickupController
    {
        private readonly ItemData _itemData;
        private readonly int _quantity;

        public ItemPickupController(ItemData itemData, int quantity)
        {
            _itemData = itemData ?? throw new ArgumentNullException(nameof(itemData));
            _quantity = quantity > 0 ? quantity : 1;
        }

        public string ItemId => _itemData.itemId;
        public string DisplayName => _itemData.displayName;
        public int Quantity => _quantity;

        public void GrantTo(Inventory inventory)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));

            inventory.Add(_itemData.itemId, _quantity);
        }
    }
}
```

**Step 2: Verify compilation**

> **Unity Editor task (user):** Confirm no compile errors.

---

### Task 5: Create `ItemPickup` MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Platformer/ItemPickup.cs`

**Step 1: Write the MonoBehaviour**

```csharp
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to a world pickup GameObject with a Trigger Collider2D.
    /// On player contact, grants the configured item to inventory and despawns.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Item data asset to grant on collect.")]
        private ItemData _itemData;

        [SerializeField]
        [Tooltip("Quantity to grant.")]
        private int _quantity = 1;

        [SerializeField]
        [Tooltip("Unique pickup ID for session persistence. Must be unique per pickup instance in the scene.")]
        private string _pickupId;

        [SerializeField]
        [Tooltip("Optional Animator to play a collect animation before despawning.")]
        private Animator _animator;

        private ItemPickupController _controller;

        private void Awake()
        {
            if (_itemData == null)
            {
                Debug.LogWarning($"[ItemPickup] itemData is not assigned on '{gameObject.name}'.", this);
                return;
            }

            _controller = new ItemPickupController(_itemData, _quantity);
        }

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            if (string.IsNullOrWhiteSpace(_pickupId)) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.IsPickupCollected(_pickupId)) return;

            _controller.GrantTo(GameManager.Instance.PlayerState.Inventory);
            GameManager.Instance.MarkPickupCollected(_pickupId);

            if (_animator != null)
                _animator.SetTrigger("Collect");

            Destroy(gameObject);
        }
    }
}
```

**Step 2: Verify compilation**

> **Unity Editor task (user):** Confirm no compile errors.

---

### Task 6: Write Edit Mode tests for GameManager pickup methods

**Files:**
- Create: `Assets/Tests/Editor/Core/GameManagerPickupTests.cs`

**Step 1: Write the test class**

```csharp
using System;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class GameManagerPickupTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerObject != null)
                UnityEngine.Object.DestroyImmediate(_gameManagerObject);
        }

        [Test]
        public void IsPickupCollected_ReturnsFalse_ByDefault()
        {
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_01"));
        }

        [Test]
        public void IsPickupCollected_ReturnsFalse_ForNullOrEmpty()
        {
            Assert.IsFalse(_gameManager.IsPickupCollected(null));
            Assert.IsFalse(_gameManager.IsPickupCollected(string.Empty));
        }

        [Test]
        public void MarkPickupCollected_ThenIsPickupCollected_ReturnsTrue()
        {
            _gameManager.MarkPickupCollected("pickup_01");
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_01"));
        }

        [Test]
        public void MarkPickupCollected_NullOrEmpty_IsNoOp()
        {
            _gameManager.MarkPickupCollected(null);
            _gameManager.MarkPickupCollected(string.Empty);
            Assert.IsFalse(_gameManager.IsPickupCollected("any"));
        }

        [Test]
        public void CollectedPickupIds_IsEmpty_ByDefault()
        {
            using var enumerator = _gameManager.CollectedPickupIds.GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());
        }

        [Test]
        public void CollectedPickupIds_ReflectsMarkPickupCollected()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.MarkPickupCollected("pickup_b");
            var ids = new List<string>(_gameManager.CollectedPickupIds);
            CollectionAssert.AreEquivalent(new[] { "pickup_a", "pickup_b" }, ids);
        }

        [Test]
        public void RestoreCollectedPickups_ReplacesExistingSet()
        {
            _gameManager.MarkPickupCollected("stale");
            _gameManager.RestoreCollectedPickups(new[] { "pickup_x", "pickup_y" });
            Assert.IsFalse(_gameManager.IsPickupCollected("stale"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_x"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_y"));
        }

        [Test]
        public void RestoreCollectedPickups_WithNull_ClearsSet()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.RestoreCollectedPickups(null);
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_a"));
        }

        [Test]
        public void RestoreCollectedPickups_SkipsNullAndEmptyIds()
        {
            _gameManager.RestoreCollectedPickups(new[] { "pickup_a", null, string.Empty, "pickup_b" });
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_a"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_b"));
            var ids = new List<string>(_gameManager.CollectedPickupIds);
            Assert.AreEqual(2, ids.Count);
        }

        [Test]
        public void ClearCollectedPickups_RemovesAll()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.ClearCollectedPickups();
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_a"));
        }

        [Test]
        public void BuildSaveData_IncludesCollectedPickupIds()
        {
            _gameManager.MarkPickupCollected("pickup_01");
            _gameManager.MarkPickupCollected("pickup_02");
            SaveData data = _gameManager.BuildSaveData();
            Assert.IsNotNull(data.collectedPickupIds);
            Assert.AreEqual(2, data.collectedPickupIds.Length);
            CollectionAssert.AreEquivalent(new[] { "pickup_01", "pickup_02" }, data.collectedPickupIds);
        }

        [Test]
        public void BuildSaveData_CollectedPickupIds_IsEmptyArray_WhenNoneCollected()
        {
            SaveData data = _gameManager.BuildSaveData();
            Assert.IsNotNull(data.collectedPickupIds);
            Assert.AreEqual(0, data.collectedPickupIds.Length);
        }

        [Test]
        public void ApplySaveData_RestoresCollectedPickupIds()
        {
            _gameManager.MarkPickupCollected("stale");
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                collectedPickupIds = new[] { "pickup_a", "pickup_b" }
            };
            _gameManager.ApplySaveData(saveData);
            Assert.IsFalse(_gameManager.IsPickupCollected("stale"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_a"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_b"));
        }

        [Test]
        public void ApplySaveData_NullCollectedPickupIds_ClearsSet()
        {
            _gameManager.MarkPickupCollected("stale");
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                collectedPickupIds = null
            };
            _gameManager.ApplySaveData(saveData);
            Assert.IsFalse(_gameManager.IsPickupCollected("stale"));
        }

        [Test]
        public void StartNewGame_ClearsCollectedPickups()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.StartNewGame();
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_a"));
        }

        private CharacterData CreateTestCharacterData(
            int maxHp = 100, int maxMp = 50, int atk = 10, int def = 5, int spd = 8)
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK = atk;
            cd.baseDEF = def;
            cd.baseSPD = spd;
            return cd;
        }
    }
}
```

**Step 2: Run tests**

> **Unity Editor task (user):** Open Test Runner (Window → General → Test Runner) → PlayMode / EditMode tab → Run `CoreTests.GameManagerPickupTests` → confirm all pass.

---

### Task 7: Write Edit Mode tests for `ItemPickupController`

**Files:**
- Create: `Assets/Tests/Editor/Platformer/ItemPickupControllerTests.cs`

**Step 1: Write the test class**

```csharp
using System;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class ItemPickupControllerTests
    {
        [Test]
        public void Constructor_NullItemData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ItemPickupController(null, 1));
        }

        [Test]
        public void GrantTo_NullInventory_ThrowsArgumentNullException()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "potion";
            var controller = new ItemPickupController(itemData, 1);

            Assert.Throws<ArgumentNullException>(() => controller.GrantTo(null));
        }

        [Test]
        public void GrantTo_AddsCorrectItemAndQuantity()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "potion";
            var controller = new ItemPickupController(itemData, 3);
            var inventory = new Inventory();

            controller.GrantTo(inventory);

            Assert.AreEqual(3, inventory.GetQuantity("potion"));
        }

        [Test]
        public void Constructor_ZeroQuantity_DefaultsToOne()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "potion";
            var controller = new ItemPickupController(itemData, 0);
            var inventory = new Inventory();

            controller.GrantTo(inventory);

            Assert.AreEqual(1, inventory.GetQuantity("potion"));
        }

        [Test]
        public void Constructor_NegativeQuantity_DefaultsToOne()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "potion";
            var controller = new ItemPickupController(itemData, -2);
            var inventory = new Inventory();

            controller.GrantTo(inventory);

            Assert.AreEqual(1, inventory.GetQuantity("potion"));
        }

        [Test]
        public void DisplayName_ReturnsItemDataDisplayName()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "potion";
            itemData.displayName = "Health Potion";
            var controller = new ItemPickupController(itemData, 1);

            Assert.AreEqual("Health Potion", controller.DisplayName);
        }

        [Test]
        public void ItemId_ReturnsItemDataItemId()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "ether_mp";
            var controller = new ItemPickupController(itemData, 2);

            Assert.AreEqual("ether_mp", controller.ItemId);
        }

        [Test]
        public void GrantTo_ItemDataWithNullItemId_IsNoOp()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = null;
            var controller = new ItemPickupController(itemData, 1);
            var inventory = new Inventory();

            controller.GrantTo(inventory);

            Assert.AreEqual(0, inventory.GetAll().Count);
        }

        [Test]
        public void GrantTo_ItemDataWithEmptyItemId_IsNoOp()
        {
            var itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = string.Empty;
            var controller = new ItemPickupController(itemData, 1);
            var inventory = new Inventory();

            controller.GrantTo(inventory);

            Assert.AreEqual(0, inventory.GetAll().Count);
        }
    }
}
```

**Step 2: Run tests**

> **Unity Editor task (user):** Test Runner → EditMode → Run `PlatformerTests.ItemPickupControllerTests` → confirm all pass.

---

### Task 8: UVCS check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-66): add ItemPickup with inventory grant and save persistence`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Scripts/Core/GameManager.cs.meta`
  - `Assets/Scripts/Data/SaveData.cs`
  - `Assets/Scripts/Data/SaveData.cs.meta`
  - `Assets/Scripts/Platformer/ItemPickupController.cs`
  - `Assets/Scripts/Platformer/ItemPickupController.cs.meta`
  - `Assets/Scripts/Platformer/ItemPickup.cs`
  - `Assets/Scripts/Platformer/ItemPickup.cs.meta`
  - `Assets/Tests/Editor/Core/GameManagerPickupTests.cs`
  - `Assets/Tests/Editor/Core/GameManagerPickupTests.cs.meta`
  - `Assets/Tests/Editor/Platformer/ItemPickupControllerTests.cs`
  - `Assets/Tests/Editor/Platformer/ItemPickupControllerTests.cs.meta`

---

### Task 9: Create the pickup prefab and place in Platformer scene *(Unity Editor task — user)*

> **Unity Editor task (user):** This task wires the pre-created animation assets into a reusable prefab.

**Step 1: Create the pickup GameObject**

- In the **Platformer** scene, create an empty GameObject named `ItemPickup_RoundPotion`
- Add a **Sprite Renderer** and assign the first frame of the Round Potion spritesheet
- Add a **Collider 2D** (e.g. Circle Collider 2D or Box Collider 2D) and set **Is Trigger = true**
- Add the `ItemPickup` script (from `Assets/Scripts/Platformer/`)
- Assign values in the Inspector:
  - `_itemData`: drag in the existing `ID_Potion` (or equivalent) ItemData asset from `Assets/Data/Items/`
  - `_quantity`: `1`
  - `_pickupId`: a unique string for this instance, e.g. `platformer_potion_01`
  - `_animator`: drag in the Animator component on this GameObject (or child)

**Step 2: Set up the Animator**

- If not already done, add an **Animator** component to the GameObject (or its Visual child)
- Assign the existing animator controller from `Assets/Animations/Items/Round Potion/Round Potion - BLUE - Spritesheet_0.controller` (or the RED variant)
- Ensure the animator controller has a trigger parameter named `Collect` that transitions to the collect animation

**Step 3: Make it a prefab**

- Drag the configured GameObject into `Assets/Prefabs/Items/` (create the folder if needed)
- Delete the instance from the scene hierarchy — you will place prefab instances next

**Step 4: Place instances in the world**

- Drag prefab instances into the Platformer scene at desired locations
- Ensure each instance has a **unique `_pickupId`** (e.g. `platformer_potion_01`, `platformer_potion_02`)
- The Player GameObject must have the tag `Player` for trigger detection to work

**Step 5: Test in Play Mode**

- Enter Play Mode
- Walk the player into the pickup
- Verify:
  - The pickup GameObject is destroyed
  - Inventory contains the item (can verify via a debug inspector or by entering battle and checking the Item menu)
  - Save the game, restart Play Mode, load the save — the pickup does not respawn

---

### Task 10: Optional pickup floating text *(deferred / scope-flex)*

> **Acceptance Criteria note:** "Optional: brief pickup UI toast showing item name + quantity gained (scope-flex — defer if layout work blocks the core loop)"

If the user wants this now, it can be added as a follow-up task using the existing `PlatformerFloatingNumberSpawner` pattern from `SavePointTrigger`. Otherwise, defer to Phase 7 polish.

---

## Post-Plan Review Checklist (run before execution)

- [x] C# guard clause ordering: `OnTriggerEnter2D` exits on player tag → controller null → pickupId empty → GameManager null → already collected. Correct.
- [x] Test coverage:
  - Null/empty pickupId guards in GameManager: covered by `IsPickupCollected_ReturnsFalse_ForNullOrEmpty`, `MarkPickupCollected_NullOrEmpty_IsNoOp`
  - Empty collection (no pickups): covered by `CollectedPickupIds_IsEmpty_ByDefault`, `BuildSaveData_CollectedPickupIds_IsEmptyArray_WhenNoneCollected`
  - Happy path single + multi: covered by `MarkPickupCollected_ThenIsPickupCollected_ReturnsTrue`, `CollectedPickupIds_ReflectsMarkPickupCollected`
  - Null inventory in controller: covered by `GrantTo_NullInventory_ThrowsArgumentNullException`
  - Zero/negative quantity: covered by `Constructor_ZeroQuantity_DefaultsToOne`, `Constructor_NegativeQuantity_DefaultsToOne`
  - Null/empty itemId in controller: covered by `GrantTo_ItemDataWithNullItemId_IsNoOp`, `GrantTo_ItemDataWithEmptyItemId_IsNoOp`
- [x] UVCS staged file audit: every new `.cs` file and its `.meta` listed in Task 8
- [x] Method signature consistency: `GrantTo(Inventory inventory)` matches in both implementation and tests
- [x] Unity Editor task isolation: all editor steps are in separate `> **Unity Editor task (user):**` callouts, never mixed with code steps
