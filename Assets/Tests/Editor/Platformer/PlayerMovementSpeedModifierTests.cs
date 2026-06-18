using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class PlayerMovementSpeedModifierTests
    {
        private GameObject _playerGo;
        private Rigidbody2D _rb;
        private Transform _groundCheck;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("Player");
            _rb = _playerGo.AddComponent<Rigidbody2D>();

            var groundCheckGo = new GameObject("GroundCheck");
            _groundCheck = groundCheckGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_groundCheck.gameObject);
            Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void Move_RequestedExternalMultiplier_ScalesHorizontalVelocity()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.Move(1f);

            Assert.AreEqual(5.6f, _rb.linearVelocity.x, 0.001f);
        }

        [Test]
        public void Move_MultipleRequestedMultipliers_UsesSmallestMultiplier()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.9f);
            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.Move(1f);

            Assert.AreEqual(5.6f, _rb.linearVelocity.x, 0.001f);
        }

        [Test]
        public void Move_AfterResetExternalMultiplier_RestoresFullSpeed()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.ResetExternalMoveSpeedMultiplier();
            movement.Move(1f);

            Assert.AreEqual(8f, _rb.linearVelocity.x, 0.001f);
        }

        [Test]
        public void Move_WhenMovementLocked_StopsEvenWithExternalMultiplier()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.SetMovementLocked(true);
            movement.Move(1f);

            Assert.AreEqual(0f, _rb.linearVelocity.x, 0.001f);
        }

        private PlayerMovement CreateMovement(float moveSpeed)
        {
            return new PlayerMovement(
                _rb,
                _groundCheck,
                groundLayer: 0,
                oneWayLayer: 0,
                playerLayerIndex: _playerGo.layer,
                moveSpeed: moveSpeed,
                jumpForce: 16f,
                coyoteTime: 0.15f,
                jumpBufferTime: 0.15f,
                fallGravityMultiplier: 2.5f,
                groundCheckRadius: 0.1f,
                dropThroughDuration: 0.2f);
        }
    }
}
