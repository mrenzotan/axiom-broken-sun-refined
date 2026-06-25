using System.Collections.Generic;
using System.Linq;
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Battle.Tests
{
    [TestFixture]
    public class ConditionBadgeLogicTests
    {
        private static CharacterStats MakeStats()
            => new CharacterStats { MaxHP = 100, MaxMP = 30, ATK = 10, DEF = 5, SPD = 8 };

        [Test]
        public void BuildBadges_NullStats_ReturnsEmpty()
        {
            var badges = ConditionBadgeLogic.BuildBadges(
                null, new List<ChemicalCondition> { ChemicalCondition.Liquid });
            Assert.That(badges, Is.Empty);
        }

        [Test]
        public void BuildBadges_InnateOnly_ShowsInnateBadgeWithNoCounter()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });

            var badges = ConditionBadgeLogic.BuildBadges(stats, stats.InnateConditions);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
            Assert.That(badges[0].IsInnate, Is.True);
            Assert.That(badges[0].TurnsRemaining, Is.EqualTo(0));
        }

        [Test]
        public void BuildBadges_NullInnateList_ShowsNoInnateBadges()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });

            var badges = ConditionBadgeLogic.BuildBadges(stats, null);

            Assert.That(badges, Is.Empty);
        }

        [Test]
        public void BuildBadges_InnatePlusStatus_InnateFirstThenTimedWithCounter()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
            stats.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5, duration: 2);

            var badges = ConditionBadgeLogic.BuildBadges(stats, stats.InnateConditions);

            Assert.That(badges.Count, Is.EqualTo(2));
            // Innate first
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
            Assert.That(badges[0].IsInnate, Is.True);
            // Then time-limited with its turn count
            Assert.That(badges[1].Condition, Is.EqualTo(ChemicalCondition.Burning));
            Assert.That(badges[1].IsInnate, Is.False);
            Assert.That(badges[1].TurnsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void BuildBadges_FrozenLiquid_HidesSuppressedInnate_ShowsOnlyTransform()
        {
            // Liquid enemy frozen into Solid: the suppressed Liquid is consumed and Solid
            // added as a temporary transformation. The current-form innate (computed by the
            // caller) is [Solid], which must be deduped by the time-limited Solid badge.
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
            stats.ConsumeCondition(ChemicalCondition.Liquid);
            stats.ApplyMaterialTransformation(
                transformsTo: ChemicalCondition.Solid,
                suppressedCondition: ChemicalCondition.Liquid,
                duration: 2);

            var currentFormInnate = new List<ChemicalCondition> { ChemicalCondition.Solid };
            var badges = ConditionBadgeLogic.BuildBadges(stats, currentFormInnate);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Solid));
            Assert.That(badges[0].IsInnate, Is.False);          // time-limited wins
            Assert.That(badges[0].TurnsRemaining, Is.EqualTo(2));
            Assert.That(badges.Any(b => b.Condition == ChemicalCondition.Liquid), Is.False);
        }

        [Test]
        public void BuildBadges_TwoInnateConditions_ShowsBoth()
        {
            var stats = MakeStats();
            stats.Initialize(new List<ChemicalCondition>
                { ChemicalCondition.Liquid, ChemicalCondition.Vapor });

            var badges = ConditionBadgeLogic.BuildBadges(stats, stats.InnateConditions);

            Assert.That(badges.Select(b => b.Condition),
                Is.EquivalentTo(new[] { ChemicalCondition.Liquid, ChemicalCondition.Vapor }));
            Assert.That(badges.All(b => b.IsInnate && b.TurnsRemaining == 0), Is.True);
        }

        [Test]
        public void BuildBadges_NoneInnateCondition_ProducesNoBadge()
        {
            // A designer can add an innate row in EnemyData and leave it at the default None.
            // None is "no condition" — it must never render as a badge (it has no label/color).
            var stats = MakeStats();
            stats.Initialize();

            var innate = new List<ChemicalCondition>
                { ChemicalCondition.None, ChemicalCondition.Liquid };
            var badges = ConditionBadgeLogic.BuildBadges(stats, innate);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
            Assert.That(badges.Any(b => b.Condition == ChemicalCondition.None), Is.False);
        }

        [Test]
        public void BuildBadges_DuplicateInnateEntries_RenderedOnce()
        {
            var stats = MakeStats();
            stats.Initialize();

            var innate = new List<ChemicalCondition>
                { ChemicalCondition.Liquid, ChemicalCondition.Liquid };
            var badges = ConditionBadgeLogic.BuildBadges(stats, innate);

            Assert.That(badges.Count, Is.EqualTo(1));
            Assert.That(badges[0].Condition, Is.EqualTo(ChemicalCondition.Liquid));
        }
    }
}
