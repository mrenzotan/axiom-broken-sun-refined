using System;
using Axiom.Data;
using Axiom.Platformer;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class PlatformerSpellWorldCasterTests
    {
        private SpellData MakeSpell(string name)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            return spell;
        }

        private SpellData MakeSpell(string name, int mpCost)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            spell.mpCost = mpCost;
            return spell;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"field {fieldName} not found");
            field.SetValue(target, value);
        }

        // An in-range meltable obstacle that accepts `accepts` — the minimal HasResolvableTarget hit.
        private static MeltableObstacleController MakeInRangeMeltable(SpellData accepts)
        {
            var go = new GameObject("MeltableObstacle");
            var obstacle = go.AddComponent<MeltableObstacleController>();
            obstacle.SetPlayerInRange(true);
            SetPrivateField(obstacle, "_meltSpells",
                new System.Collections.Generic.List<SpellData> { accepts });
            return obstacle;
        }

        [Test]
        public void HasResolvableTarget_NullSpell_ReturnsFalse()
        {
            bool result = PlatformerSpellWorldCaster.HasResolvableTarget(
                null,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.IsFalse(result);
        }

        [Test]
        public void HasResolvableTarget_AllListsNull_ReturnsFalse()
        {
            SpellData spell = MakeSpell("melt");

            bool result = PlatformerSpellWorldCaster.HasResolvableTarget(
                spell, null, null, null, null, null);

            Assert.IsFalse(result, "An empty world must never report a castable target.");
        }

        [Test]
        public void HasResolvableTarget_AllListsEmpty_ReturnsFalse()
        {
            SpellData spell = MakeSpell("melt");

            bool result = PlatformerSpellWorldCaster.HasResolvableTarget(
                spell,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.IsFalse(result);
        }

        [Test]
        public void EvaluateCast_NoInRangeTarget_ReturnsNoTarget()
        {
            // WHY: with nothing nearby to resolve, the controller must stay silent —
            // no cast AND no "not enough MP" cue — even if MP is plentiful.
            SpellData spell = MakeSpell("melt", 5);

            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                spell, 999,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.NoTarget, result);

            UnityEngine.Object.DestroyImmediate(spell);
        }

        [Test]
        public void EvaluateCast_InRangeTargetButInsufficientMp_ReturnsInsufficientMana()
        {
            // WHY: this is the exact fail-feedback trigger — a resolvable puzzle in range
            // that the player cannot currently afford.
            SpellData spell = MakeSpell("melt", 8);
            MeltableObstacleController obstacle = MakeInRangeMeltable(spell);

            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                spell, 5,
                new[] { obstacle },
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.InsufficientMana, result);

            UnityEngine.Object.DestroyImmediate(obstacle.gameObject);
            UnityEngine.Object.DestroyImmediate(spell);
        }

        [Test]
        public void EvaluateCast_InRangeTargetAndEnoughMp_ReturnsCastable()
        {
            SpellData spell = MakeSpell("melt", 8);
            MeltableObstacleController obstacle = MakeInRangeMeltable(spell);

            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                spell, 8,
                new[] { obstacle },
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.Castable, result);

            UnityEngine.Object.DestroyImmediate(obstacle.gameObject);
            UnityEngine.Object.DestroyImmediate(spell);
        }

        [Test]
        public void EvaluateCast_NullSpell_ReturnsNoTarget()
        {
            // WHY: a null spell must short-circuit to NoTarget before mpCost is ever read.
            CastEvaluation result = PlatformerSpellWorldCaster.EvaluateCast(
                null, 0,
                Array.Empty<MeltableObstacleController>(),
                Array.Empty<FreezablePlatformController>(),
                Array.Empty<BurnableObstacleController>(),
                Array.Empty<SteamVentController>(),
                Array.Empty<AcidPuddleController>());

            Assert.AreEqual(CastEvaluation.NoTarget, result);
        }
    }
}
