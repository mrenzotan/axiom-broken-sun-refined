using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class EnemyPatrolBehaviorTests
    {
        private static Vector2[] TwoWaypoints() =>
            new[] { new Vector2(-3f, 0f), new Vector2(3f, 0f) };

        // ── Initial state ─────────────────────────────────────────────────────

        [Test]
        public void InitialState_IsPatrol()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            Assert.AreEqual(EnemyPatrolBehavior.State.Patrol, behavior.CurrentState);
        }

        // ── Patrol → Aggro ────────────────────────────────────────────────────

        [Test]
        public void Patrol_PlayerDetected_TransitionsToAggro()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, playerDetected: true, playerPosition: new Vector2(2f, 0f), deltaTime: 0.016f);
            Assert.AreEqual(EnemyPatrolBehavior.State.Aggro, behavior.CurrentState);
        }

        // ── Aggro grace period ────────────────────────────────────────────────

        [Test]
        public void Aggro_PlayerNotDetected_StaysAggroDuringGracePeriod()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.5f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);  // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.1f);           // within grace period
            Assert.AreEqual(EnemyPatrolBehavior.State.Aggro, behavior.CurrentState);
        }

        [Test]
        public void Aggro_PlayerNotDetected_TransitionsToReturningAfterGracePeriod()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.3f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);  // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.4f);           // grace expired (0.4 > 0.3)
            Assert.AreEqual(EnemyPatrolBehavior.State.Returning, behavior.CurrentState);
        }

        // ── Returning → Patrol ────────────────────────────────────────────────

        [Test]
        public void Returning_WaypointReached_TransitionsToPatrol()
        {
            // waypoints[0] = (-3, 0), waypoints[1] = (3, 0)
            // Enemy starts at (0, 0). Index = 0, so target is (-3, 0).
            // After aggro/returning, position -2.8 is within threshold 0.5 of (-3, 0).
            var waypoints = new[] { new Vector2(-3f, 0f), new Vector2(3f, 0f) };
            var behavior = new EnemyPatrolBehavior(waypoints, patrolSpeed: 3f, chaseSpeed: 5f,
                waypointThreshold: 0.5f, deaggroGracePeriod: 0.1f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);          // → Aggro
            behavior.Tick(new Vector2(0.5f, 0f), false, Vector2.zero, 0.2f);          // grace expired → Returning
            behavior.Tick(new Vector2(-2.8f, 0f), false, Vector2.zero, 0.016f);       // reach waypoints[0] → Patrol
            Assert.AreEqual(EnemyPatrolBehavior.State.Patrol, behavior.CurrentState);
        }

        // ── Re-aggro during Returning ─────────────────────────────────────────

        [Test]
        public void Returning_PlayerDetected_TransitionsBackToAggro()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.1f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);   // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.2f);            // → Returning
            behavior.Tick(Vector2.zero, true, new Vector2(-2f, 0f), 0.016f);  // player back → Aggro
            Assert.AreEqual(EnemyPatrolBehavior.State.Aggro, behavior.CurrentState);
        }

        // ── Patrol velocity / facing ──────────────────────────────────────────

        [Test]
        public void Patrol_NoWaypoints_ReturnsZeroVelocity()
        {
            var behavior = new EnemyPatrolBehavior(new Vector2[0], patrolSpeed: 3f, chaseSpeed: 5f);
            float vel = behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);
            Assert.AreEqual(0f, vel);
        }

        [Test]
        public void Patrol_WaypointToRight_FacingDirectionIsPositive()
        {
            var behavior = new EnemyPatrolBehavior(new[] { new Vector2(5f, 0f) }, patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);
            Assert.AreEqual(1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Patrol_WaypointToLeft_FacingDirectionIsNegative()
        {
            var behavior = new EnemyPatrolBehavior(new[] { new Vector2(-5f, 0f) }, patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);
            Assert.AreEqual(-1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Patrol_WaypointReached_AdvancesToNextWaypoint()
        {
            // waypoints[0] at (0.1, 0) — within default threshold 0.2 of spawn (0, 0)
            // After advancing, waypoints[1] at (5, 0) — should produce positive velocity
            var waypoints = new[] { new Vector2(0.1f, 0f), new Vector2(5f, 0f) };
            var behavior = new EnemyPatrolBehavior(waypoints, patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f); // reach waypoints[0], index advances to 1
            float vel = behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f); // now heading to waypoints[1]
            Assert.Greater(vel, 0f);
        }

        // ── Aggro velocity / facing ───────────────────────────────────────────

        [Test]
        public void Aggro_ReturnsChaseSpeedTowardPlayer()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            float vel = behavior.Tick(Vector2.zero, true, new Vector2(3f, 0f), 0.016f);
            Assert.AreEqual(5f, vel);
        }

        [Test]
        public void Aggro_PlayerToRight_FacingDirectionIsPositive()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, true, new Vector2(3f, 0f), 0.016f);
            Assert.AreEqual(1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Aggro_PlayerToLeft_FacingDirectionIsNegative()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, true, new Vector2(-3f, 0f), 0.016f);
            Assert.AreEqual(-1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Aggro_PlayerAtSamePosition_ReturnsZeroVelocity()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f); // enter Aggro from a distance
            // Player now at exact same X as enemy — dx == 0, within waypointThreshold
            float vel = behavior.Tick(Vector2.zero, true, Vector2.zero, 0.016f);
            Assert.AreEqual(0f, vel);
        }

        // ── Returning edge cases ──────────────────────────────────────────────

        [Test]
        public void Returning_NoWaypoints_TransitionsToPatrol()
        {
            // Even with no waypoints, Patrol can still transition to Aggro (player detected),
            // then to Returning (grace expired). TickReturning with no waypoints must go straight back to Patrol.
            var behavior = new EnemyPatrolBehavior(new Vector2[0], patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.1f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);  // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.2f);           // grace expired → Returning
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);         // no waypoints → Patrol
            Assert.AreEqual(EnemyPatrolBehavior.State.Patrol, behavior.CurrentState);
        }
    }
}
