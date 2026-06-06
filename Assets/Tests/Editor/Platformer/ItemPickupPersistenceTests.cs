using System.IO;
using System.Reflection;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlatformerTests
{
    public class ItemPickupPersistenceTests
    {
        private GameObject _gameManagerGo;
        private GameManager _gameManager;
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _gameManagerGo = new GameObject("GameManager");
            _gameManager = _gameManagerGo.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateCharacterData());
            _gameManager.SetSaveServiceForTests(new SaveService(_tempDirectory));

            if (GameManager.Instance == null)
            {
                typeof(GameManager)
                    .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(_gameManager, null);
            }

            Assert.AreSame(_gameManager, GameManager.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameManagerGo);
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
        }

        [Test]
        public void OnTriggerEnter2D_PlayerCollectsPickup_PersistsSaveImmediately()
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = "potion_hp";

            GameObject pickupGo = new GameObject("Pickup");
            pickupGo.AddComponent<BoxCollider2D>().isTrigger = true;
            var pickup = pickupGo.AddComponent<ItemPickup>();

            typeof(ItemPickup).GetField("_itemData", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pickup, item);
            typeof(ItemPickup).GetField("_pickupId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pickup, "pickup_01");

            typeof(ItemPickup).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pickup, null);

            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            Collider2D playerCollider = playerGo.AddComponent<BoxCollider2D>();

            LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");

            typeof(ItemPickup).GetMethod("OnTriggerEnter2D", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pickup, new object[] { playerCollider });

            var reloaded = new SaveService(_tempDirectory);
            Assert.IsTrue(reloaded.TryLoad(out SaveData saveData));
            CollectionAssert.Contains(saveData.collectedPickupIds, "pickup_01");
            Assert.AreEqual(1, _gameManager.PlayerState.Inventory.GetQuantity("potion_hp"));

            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(item);
        }

        private static CharacterData CreateCharacterData()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.baseMaxHP = 100;
            data.baseMaxMP = 50;
            data.baseATK = 10;
            data.baseDEF = 5;
            data.baseSPD = 8;
            return data;
        }
    }
}
