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
