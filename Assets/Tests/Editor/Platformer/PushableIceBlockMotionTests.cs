using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class PushableIceBlockMotionTests
    {
        [Test]
        public void CanPush_GroundedSideContactAndPlayerMovingIntoBlock_ReturnsTrue()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: 5.6f,
                contactNormal: Vector2.left,
                blockGrounded: true,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsTrue(canPush);
        }

        [Test]
        public void CanPush_WhenBlockAirborne_ReturnsFalse()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: 5.6f,
                contactNormal: Vector2.left,
                blockGrounded: false,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsFalse(canPush);
        }

        [Test]
        public void CanPush_TopContact_ReturnsFalse()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: 5.6f,
                contactNormal: Vector2.up,
                blockGrounded: true,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsFalse(canPush);
        }

        [Test]
        public void CanPush_PlayerMovingAwayFromBlock_ReturnsFalse()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: -5.6f,
                contactNormal: Vector2.left,
                blockGrounded: true,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsFalse(canPush);
        }

        [Test]
        public void GetPushVelocityX_ClampsToMaxPushSpeed()
        {
            float velocity = PushableIceBlockMotion.GetPushVelocityX(
                playerVelocityX: 8f,
                maxPushSpeed: 5.6f);

            Assert.AreEqual(5.6f, velocity, 0.001f);
        }

        [Test]
        public void GetPushVelocityX_PreservesDirection()
        {
            float velocity = PushableIceBlockMotion.GetPushVelocityX(
                playerVelocityX: -8f,
                maxPushSpeed: 5.6f);

            Assert.AreEqual(-5.6f, velocity, 0.001f);
        }
    }
}
