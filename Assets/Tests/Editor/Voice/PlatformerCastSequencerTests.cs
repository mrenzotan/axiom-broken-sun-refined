using System;
using Axiom.Data;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Voice.Tests
{
    public class PlatformerCastSequencerTests
    {
        private SpellData MakeSpell(string name = "melt")
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            return spell;
        }

        [Test]
        public void RequestCast_WhenIdle_BeginsCastWithSpell()
        {
            SpellData began = null;
            var seq = new PlatformerCastSequencer(s => began = s, _ => { }, () => { });
            SpellData spell = MakeSpell();

            bool started = seq.RequestCast(spell);

            Assert.IsTrue(started);
            Assert.AreSame(spell, began);
            Assert.IsTrue(seq.IsCasting);
        }

        [Test]
        public void RequestCast_NullSpell_ReturnsFalseAndDoesNotBegin()
        {
            int beginCount = 0;
            var seq = new PlatformerCastSequencer(_ => beginCount++, _ => { }, () => { });

            bool started = seq.RequestCast(null);

            Assert.IsFalse(started);
            Assert.AreEqual(0, beginCount);
            Assert.IsFalse(seq.IsCasting);
        }

        [Test]
        public void RequestCast_WhileCasting_IsIgnored()
        {
            int beginCount = 0;
            var seq = new PlatformerCastSequencer(_ => beginCount++, _ => { }, () => { });

            seq.RequestCast(MakeSpell());
            bool secondStarted = seq.RequestCast(MakeSpell());

            Assert.IsFalse(secondStarted);
            Assert.AreEqual(1, beginCount);
        }

        [Test]
        public void NotifyFireFrame_AfterRequest_ResolvesOnceThenEnds()
        {
            int resolveCount = 0;
            int endCount = 0;
            SpellData resolved = null;
            var seq = new PlatformerCastSequencer(
                _ => { },
                s => { resolved = s; resolveCount++; },
                () => endCount++);
            SpellData spell = MakeSpell();

            seq.RequestCast(spell);
            seq.NotifyFireFrame();

            Assert.AreEqual(1, resolveCount);
            Assert.AreEqual(1, endCount);
            Assert.AreSame(spell, resolved);
            Assert.IsFalse(seq.IsCasting);
        }

        [Test]
        public void NotifyFireFrame_CalledTwice_ResolvesOnlyOnce()
        {
            int resolveCount = 0;
            var seq = new PlatformerCastSequencer(_ => { }, _ => resolveCount++, () => { });

            seq.RequestCast(MakeSpell());
            seq.NotifyFireFrame(); // animation event
            seq.NotifyFireFrame(); // timeout fallback fires too

            Assert.AreEqual(1, resolveCount);
        }

        [Test]
        public void NotifyFireFrame_WithoutRequest_DoesNothing()
        {
            int resolveCount = 0;
            var seq = new PlatformerCastSequencer(_ => { }, _ => resolveCount++, () => { });

            seq.NotifyFireFrame();

            Assert.AreEqual(0, resolveCount);
        }

        [Test]
        public void Sequencer_CanCastAgainAfterResolving()
        {
            int resolveCount = 0;
            var seq = new PlatformerCastSequencer(_ => { }, _ => resolveCount++, () => { });

            seq.RequestCast(MakeSpell());
            seq.NotifyFireFrame();
            seq.RequestCast(MakeSpell());
            seq.NotifyFireFrame();

            Assert.AreEqual(2, resolveCount);
        }

        [Test]
        public void Constructor_NullCallback_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new PlatformerCastSequencer(null, _ => { }, () => { }));
            Assert.Throws<ArgumentNullException>(
                () => new PlatformerCastSequencer(_ => { }, null, () => { }));
            Assert.Throws<ArgumentNullException>(
                () => new PlatformerCastSequencer(_ => { }, _ => { }, null));
        }
    }
}
