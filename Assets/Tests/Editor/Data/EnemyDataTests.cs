using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Data.Tests
{
    public class EnemyDataTests
    {
        [Test]
        public void BattleVisualPrefab_Default_IsNull()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            Assert.IsNull(data.battleVisualPrefab,
                "battleVisualPrefab should default to null so unconfigured EnemyData " +
                "falls through to the BattleController fallback path.");
            Object.DestroyImmediate(data);
        }

        [Test]
        public void BattleVisualPrefab_Field_HasTooltip()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "battleVisualPrefab",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "EnemyData.battleVisualPrefab field is missing.");

            var tooltips = field.GetCustomAttributes(typeof(TooltipAttribute), false);
            Assert.IsNotEmpty(tooltips,
                "battleVisualPrefab must have a [Tooltip] explaining the required prefab shape.");
        }

        [Test]
        public void BattleVisualPrefab_Field_IsGameObjectType()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "battleVisualPrefab",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(GameObject), field.FieldType,
                "battleVisualPrefab must be a GameObject (the prefab root) so the spawner " +
                "can Instantiate it and resolve EnemyBattleAnimator via GetComponentInChildren.");
        }
    }
}
