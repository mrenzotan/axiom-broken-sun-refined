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
