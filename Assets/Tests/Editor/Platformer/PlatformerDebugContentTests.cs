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
        public void InputActionsAsset_DoesNotContainDebugSpellBindings()
        {
            string json = File.ReadAllText("Assets/InputSystem_Actions.inputactions");

            StringAssert.DoesNotContain("\"DebugMeltCast\"", json);
            StringAssert.DoesNotContain("\"DebugFreezeCast\"", json);
        }
    }
}
