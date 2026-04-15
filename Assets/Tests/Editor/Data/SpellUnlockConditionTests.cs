using System.Collections.Generic;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Data
{
    public class SpellUnlockConditionTests
    {
        [Test]
        public void IsUnlockedFor_NoPrerequisite_ReturnsTrueWhenLevelMet()
        {
            var condition = new SpellUnlockCondition { requiredLevel = 3, prerequisiteSpell = null };
            Assert.IsTrue(condition.IsUnlockedFor(3, new HashSet<string>()));
            Assert.IsTrue(condition.IsUnlockedFor(10, null));
        }

        [Test]
        public void IsUnlockedFor_NoPrerequisite_ReturnsFalseWhenLevelBelow()
        {
            var condition = new SpellUnlockCondition { requiredLevel = 3, prerequisiteSpell = null };
            Assert.IsFalse(condition.IsUnlockedFor(2, new HashSet<string>()));
        }

        [Test]
        public void IsUnlockedFor_WithPrerequisite_RequiresBothLevelAndSpell()
        {
            SpellData prereq = ScriptableObject.CreateInstance<SpellData>();
            prereq.spellName = "combust";

            var condition = new SpellUnlockCondition { requiredLevel = 2, prerequisiteSpell = prereq };

            Assert.IsFalse(condition.IsUnlockedFor(2, new HashSet<string>()),
                "Level met but prerequisite spell missing — should be locked.");
            Assert.IsFalse(condition.IsUnlockedFor(1, new HashSet<string> { "combust" }),
                "Prerequisite met but level too low — should be locked.");
            Assert.IsTrue(condition.IsUnlockedFor(2, new HashSet<string> { "combust" }),
                "Both conditions satisfied — should be unlocked.");

            ScriptableObject.DestroyImmediate(prereq);
        }

        [Test]
        public void IsUnlockedFor_NullUnlockedSet_WithPrerequisite_ReturnsFalse()
        {
            SpellData prereq = ScriptableObject.CreateInstance<SpellData>();
            prereq.spellName = "freeze";

            var condition = new SpellUnlockCondition { requiredLevel = 1, prerequisiteSpell = prereq };

            Assert.IsFalse(condition.IsUnlockedFor(5, null));

            ScriptableObject.DestroyImmediate(prereq);
        }
    }
}
