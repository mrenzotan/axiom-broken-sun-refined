using System.Reflection;
using Axiom.Battle;
using NUnit.Framework;
using UnityEngine;

namespace BattleTests
{
    public class EnemyBattleAnimatorTests
    {
        private GameObject _go;
        private EnemyBattleAnimator _battleAnimator;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EnemyBattleAnimatorTest");
            Animator animator = _go.AddComponent<Animator>();
            _battleAnimator = _go.AddComponent<EnemyBattleAnimator>();

            typeof(EnemyBattleAnimator)
                .GetField("_animator", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_battleAnimator, animator);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void OptionalAnimatorParameters_Missing_DoNotThrow()
        {
            Assert.DoesNotThrow(() => _battleAnimator.SetAttackIndex(1),
                "Simple enemy Animator Controllers may not define AttackIndex.");
            Assert.DoesNotThrow(() => _battleAnimator.SetPhase(2),
                "Only boss/morph-capable Animator Controllers need Phase.");
            Assert.DoesNotThrow(() => _battleAnimator.TriggerFormChange(),
                "Only boss/morph-capable Animator Controllers need PhaseChange.");
            Assert.DoesNotThrow(() => _battleAnimator.TriggerHurt(),
                "Placeholder enemy Animator Controllers should not break combat if Hurt is absent.");
            Assert.DoesNotThrow(() => _battleAnimator.TriggerDefeat(),
                "Placeholder enemy Animator Controllers should not break combat if Defeat is absent.");
        }
    }
}
