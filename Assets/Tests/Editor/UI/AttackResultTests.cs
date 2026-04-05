using NUnit.Framework;
using Axiom.Battle;

namespace Axiom.Tests.UI
{
    public class AttackResultTests
    {
        // ── PlayerActionHandler ──────────────────────────────────────────────

        [Test]
        public void ExecuteAttack_DealsDamageToEnemy()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 12, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 0,  DEF = 4, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.9f); // 0.9 > 0.2 → no crit
            AttackResult result = handler.ExecuteAttack();

            Assert.AreEqual(8, result.Damage);           // max(1, 12 - 4) = 8
            Assert.IsFalse(result.IsCrit);
            Assert.IsFalse(result.TargetDefeated);
            Assert.AreEqual(52, enemy.CurrentHP);        // 60 - 8 = 52
        }

        [Test]
        public void ExecuteAttack_CritHit_WhenRandomBelowThreshold()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 10, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 0,  DEF = 0, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.05f); // 0.05 < 0.2 → crit
            AttackResult result = handler.ExecuteAttack();

            Assert.IsTrue(result.IsCrit);
            Assert.AreEqual(15, result.Damage); // (int)(10 * 1.5f) = 15
        }

        [Test]
        public void ExecuteAttack_ReturnsTargetDefeated_WhenEnemyHPReachesZero()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 100, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 10,  MaxMP = 0, ATK = 0,   DEF = 0, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.9f);
            AttackResult result = handler.ExecuteAttack();

            Assert.IsTrue(result.TargetDefeated);
            Assert.AreEqual(0, enemy.CurrentHP);
        }

        [Test]
        public void ExecuteAttack_MinimumDamageIsOne()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 1, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 0, DEF = 100, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.9f);
            AttackResult result = handler.ExecuteAttack();

            Assert.AreEqual(1, result.Damage); // max(1, 1 - 100) = 1
        }

        // ── EnemyActionHandler ───────────────────────────────────────────────

        [Test]
        public void EnemyExecuteAttack_DealsDamageToPlayer()
        {
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 8, DEF = 0, SPD = 0 };
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 0, DEF = 6, SPD = 0 };
            enemy.Initialize();
            player.Initialize();

            var handler = new EnemyActionHandler(enemy, player, () => 0.9f);
            AttackResult result = handler.ExecuteAttack();

            Assert.AreEqual(2, result.Damage);            // max(1, 8 - 6) = 2
            Assert.IsFalse(result.TargetDefeated);
            Assert.AreEqual(98, player.CurrentHP);
        }
    }
}
