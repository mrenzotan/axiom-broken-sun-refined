using System.Collections.Generic;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Battle.Tests
{
    [TestFixture]
    public class EnemyDataFormConditionsTests
    {
        // A Liquid (form 0) / Solid (form 1) enemy, mirroring the canonical frozen-Liquid case.
        private static EnemyData MakeTwoFormEnemy()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.innateConditions = new List<ChemicalCondition> { ChemicalCondition.Liquid };
            enemy.formDefinitions = new List<EnemyFormData>
            {
                new EnemyFormData { formIndex = 0, formName = "Liquid",
                    innateConditions = new List<ChemicalCondition> { ChemicalCondition.Liquid } },
                new EnemyFormData { formIndex = 1, formName = "Ice",
                    innateConditions = new List<ChemicalCondition> { ChemicalCondition.Solid } },
            };
            return enemy;
        }

        [Test]
        public void FormResolution_LiquidActive_ReturnsForm0InnateLiquid()
        {
            var enemy = MakeTwoFormEnemy();
            var active = new List<ChemicalCondition> { ChemicalCondition.Liquid };

            int form = enemy.GetFormIndexForConditions(active);
            var innate = enemy.GetInnateConditionsForForm(form);

            Assert.That(form, Is.EqualTo(0));
            Assert.That(innate, Is.EqualTo(new[] { ChemicalCondition.Liquid }));
        }

        [Test]
        public void FormResolution_SolidActive_ReturnsForm1InnateSolid()
        {
            var enemy = MakeTwoFormEnemy();
            var active = new List<ChemicalCondition> { ChemicalCondition.Solid };

            int form = enemy.GetFormIndexForConditions(active);
            var innate = enemy.GetInnateConditionsForForm(form);

            Assert.That(form, Is.EqualTo(1));
            Assert.That(innate, Is.EqualTo(new[] { ChemicalCondition.Solid }));
        }
    }
}
