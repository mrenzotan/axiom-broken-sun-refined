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
