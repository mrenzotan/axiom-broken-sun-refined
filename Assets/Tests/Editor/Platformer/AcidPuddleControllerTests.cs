using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Axiom.Data;
using Axiom.Platformer;
using UnityEngine;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class AcidPuddleControllerTests
    {
        [Test]
        public void CanNeutralizeWith_PlayerNotInRange_ReturnsFalse()
        {
            // WHY: the edge-zone gate — a puddle the player isn't near must ignore the cast.
            var (go, controller, spell) = MakePuddle();
            // _isPlayerInRange defaults to false.
            Assert.IsFalse(controller.CanNeutralizeWith("neutralize"));
            Cleanup(go, spell);
        }

        [Test]
        public void CanNeutralizeWith_InRangeAndSpellMatches_ReturnsTrue()
        {
            var (go, controller, spell) = MakePuddle();
            controller.SetPlayerInRange(true);
            Assert.IsTrue(controller.CanNeutralizeWith("neutralize"));
            Cleanup(go, spell);
        }

        [Test]
        public void CanNeutralizeWith_WrongSpell_ReturnsFalse()
        {
            var (go, controller, spell) = MakePuddle();
            controller.SetPlayerInRange(true);
            Assert.IsFalse(controller.CanNeutralizeWith("combust"));
            Cleanup(go, spell);
        }

        [Test]
        public void ApplySolvedImmediate_DisablesDamageAndHidesSprite()
        {
            // WHY: a puddle dissolved before a battle must come back already-gone and
            // non-damaging on scene reload — no DoT, no visible acid.
            var (go, controller, spell) = MakePuddle();
            var renderer = go.GetComponent<SpriteRenderer>();
            var damage = go.GetComponent<BoxCollider2D>();
            SetPrivateField(controller, "_spriteRenderer", renderer);
            SetPrivateField(controller, "_damageCollider", damage);

            controller.ApplySolvedImmediate();

            Assert.IsTrue(controller.IsNeutralized);
            Assert.IsFalse(renderer.enabled);
            Assert.IsFalse(damage.enabled);
            Cleanup(go, spell);
        }

        [Test]
        public void CanNeutralizeWith_AfterSolved_ReturnsFalse()
        {
            // WHY: a dissolved puddle must not be re-castable (and must not re-spend MP).
            var (go, controller, spell) = MakePuddle();
            controller.SetPlayerInRange(true);
            controller.ApplySolvedImmediate();
            Assert.IsFalse(controller.CanNeutralizeWith("neutralize"));
            Cleanup(go, spell);
        }

        private static (GameObject, AcidPuddleController, SpellData) MakePuddle()
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = "neutralize";
            spell.mpCost = 6;

            GameObject go = new GameObject("AcidPuddle");
            go.AddComponent<BoxCollider2D>();              // satisfies [RequireComponent]
            go.AddComponent<SpriteRenderer>();
            var controller = go.AddComponent<AcidPuddleController>();
            SetPrivateField(controller, "_neutralizeSpells", new List<SpellData> { spell });
            return (go, controller, spell);
        }

        private static void Cleanup(GameObject go, SpellData spell)
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(spell);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"field {fieldName} not found");
            field.SetValue(target, value);
        }
    }
}
