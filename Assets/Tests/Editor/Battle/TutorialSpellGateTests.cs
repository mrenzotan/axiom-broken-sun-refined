using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Battle;

namespace Axiom.Battle.Tests
{
    public class TutorialSpellGateTests
    {
        [Test]
        public void Unrestricted_AllowsAnySpell()
        {
            TutorialSpellGate gate = TutorialSpellGate.Unrestricted;
            Assert.IsTrue(gate.IsAllowed("combust"));
            Assert.IsTrue(gate.IsAllowed("neutralize"));
        }

        [Test]
        public void Unrestricted_AllowsNullName_BecauseRestrictionIsAbsent()
        {
            // An unrestricted gate does not care about the parameter — the empty
            // allow-set short-circuits before the null check.
            Assert.IsTrue(TutorialSpellGate.Unrestricted.IsAllowed(null));
        }

        [Test]
        public void EmptyAllowList_IsTreatedAsUnrestricted()
        {
            var gate = new TutorialSpellGate(new List<string>(), "msg");
            Assert.IsTrue(gate.IsAllowed("combust"));
        }

        [Test]
        public void RestrictedToFreeze_AllowsFreeze_CaseInsensitive()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "msg");
            Assert.IsTrue(gate.IsAllowed("freeze"));
            Assert.IsTrue(gate.IsAllowed("Freeze"));
            Assert.IsTrue(gate.IsAllowed("FREEZE"));
        }

        [Test]
        public void RestrictedToFreeze_RejectsOtherSpells()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "msg");
            Assert.IsFalse(gate.IsAllowed("combust"));
            Assert.IsFalse(gate.IsAllowed("neutralize"));
        }

        [Test]
        public void RestrictedGate_RejectsNullOrEmptyName()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "msg");
            Assert.IsFalse(gate.IsAllowed(null));
            Assert.IsFalse(gate.IsAllowed(""));
        }

        [Test]
        public void RejectionMessage_IsExposed()
        {
            var gate = new TutorialSpellGate(new[] { "freeze" }, "say Freeze");
            Assert.AreEqual("say Freeze", gate.RejectionMessage);
        }

        [Test]
        public void IsRestricting_TrueWhenAllowListNonEmpty()
        {
            Assert.IsTrue(new TutorialSpellGate(new[] { "freeze" }, "msg").IsRestricting);
        }

        [Test]
        public void IsRestricting_FalseWhenUnrestrictedOrEmpty()
        {
            Assert.IsFalse(TutorialSpellGate.Unrestricted.IsRestricting);
            Assert.IsFalse(new TutorialSpellGate(new List<string>(), "msg").IsRestricting);
        }
    }
}
