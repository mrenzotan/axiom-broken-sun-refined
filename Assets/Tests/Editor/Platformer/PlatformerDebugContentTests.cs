using System.IO;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PlatformerTests
{
    public class PlatformerDebugContentTests
    {
        [Test]
        public void ProductionPrefabs_DoNotContainDebugSpellCasterComponents()
        {
            GameObject iceWall = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Platformer/P_IceWall.prefab");
            GameObject waterPlatform = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Platformer/P_WaterPlatform_Long.prefab");

            Assert.IsNotNull(iceWall);
            Assert.IsNotNull(waterPlatform);
            Assert.IsNull(iceWall.GetComponent<MeltableObstacleDebugCaster>());
            Assert.IsNull(waterPlatform.GetComponent<FreezablePlatformDebugCaster>());
        }

        [Test]
        public void SavePointPrefab_IsNotTaggedPlayer()
        {
            GameObject savePoint = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Platformer/SavePoint.prefab");

            Assert.IsNotNull(savePoint);
            Assert.AreNotEqual("Player", savePoint.tag,
                "Only the real Player prefab should use the Player tag; enemy combat triggers treat Player-tagged colliders as battle starts.");
        }

        [Test]
        public void InputActionsAsset_DoesNotContainDebugSpellBindings()
        {
            string json = File.ReadAllText("Assets/InputSystem_Actions.inputactions");

            StringAssert.DoesNotContain("\"DebugMeltCast\"", json);
            StringAssert.DoesNotContain("\"DebugFreezeCast\"", json);
        }
    }
}
