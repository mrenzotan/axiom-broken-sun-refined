using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Data.Tests
{
    public class EnemyDataTests
    {
        // A two-form enemy mirroring ED_FrostMeltspawn: form 0 = Liquid, form 1 = Ice (Solid).
        private static EnemyData MakeTwoFormEnemy()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.formDefinitions = new List<EnemyFormData>
            {
                new EnemyFormData
                {
                    formIndex = 0, formName = "Liquid",
                    innateConditions = new List<ChemicalCondition> { ChemicalCondition.Liquid },
                },
                new EnemyFormData
                {
                    formIndex = 1, formName = "Ice",
                    innateConditions = new List<ChemicalCondition> { ChemicalCondition.Solid },
                },
            };
            return data;
        }

        // ---- GetFormIndexForConditions: the enemy's visual form must follow its chemistry ----
        // These guard the fix for the DEV-50/DEV-47 conflict where a random form-swap system
        // overwrote chemistry state. The sprite form is now derived from the active material
        // condition rather than a separate, non-deterministic timer.

        [Test]
        public void GetFormIndexForConditions_LiquidActive_ReturnsLiquidForm()
        {
            var data = MakeTwoFormEnemy();
            int form = data.GetFormIndexForConditions(
                new List<ChemicalCondition> { ChemicalCondition.Liquid });
            Assert.AreEqual(0, form,
                "A Liquid enemy must display its liquid form (0) — the visual follows chemistry.");
            Object.DestroyImmediate(data);
        }

        [Test]
        public void GetFormIndexForConditions_SolidActive_ReturnsIceForm()
        {
            var data = MakeTwoFormEnemy();
            int form = data.GetFormIndexForConditions(
                new List<ChemicalCondition> { ChemicalCondition.Solid });
            Assert.AreEqual(1, form,
                "When Freeze transforms the enemy to Solid, it must display its ice form (1) — " +
                "this is the deterministic Liquid→Freeze→Solid tutorial flow.");
            Object.DestroyImmediate(data);
        }

        [Test]
        public void GetFormIndexForConditions_NoFormDefinitions_ReturnsZero()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            int form = data.GetFormIndexForConditions(
                new List<ChemicalCondition> { ChemicalCondition.Solid });
            Assert.AreEqual(0, form,
                "Single-form enemies (no formDefinitions) have no alternate sprite; default to form 0.");
            Object.DestroyImmediate(data);
        }

        [Test]
        public void GetFormIndexForConditions_NoMatchingForm_ReturnsZero()
        {
            var data = MakeTwoFormEnemy();
            int form = data.GetFormIndexForConditions(
                new List<ChemicalCondition> { ChemicalCondition.Burning });
            Assert.AreEqual(0, form,
                "An active condition matching no form falls back to the default form (0) rather than guessing.");
            Object.DestroyImmediate(data);
        }

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

        [Test]
        public void DefaultSpellVfxOffset_IsZero()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            Assert.AreEqual(Vector2.zero, data.spellVfxOffset);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void SpellVfxOffset_RoundTripsAssignedValue()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.spellVfxOffset = new Vector2(0.5f, -0.25f);
            Assert.AreEqual(new Vector2(0.5f, -0.25f), data.spellVfxOffset);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void SpellVfxOffset_Field_HasTooltip()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "spellVfxOffset",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "EnemyData.spellVfxOffset field is missing.");

            var tooltips = field.GetCustomAttributes(typeof(TooltipAttribute), false);
            Assert.IsNotEmpty(tooltips,
                "spellVfxOffset must have a [Tooltip] explaining the per-enemy nudge semantics.");
        }

        [Test]
        public void SpellVfxOffset_Field_IsVector2Type()
        {
            FieldInfo field = typeof(EnemyData).GetField(
                "spellVfxOffset",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(Vector2), field.FieldType,
                "spellVfxOffset must be a Vector2 so BattleController can cast to Vector3 " +
                "while preserving the enemy transform's Z.");
        }
    }
}
