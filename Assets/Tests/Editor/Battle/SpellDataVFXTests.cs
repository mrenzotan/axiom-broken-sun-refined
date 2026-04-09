using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Tests.Battle
{
    public class SpellDataVFXTests
    {
        [Test]
        public void SpellData_CastVfxClip_IsNullByDefault()
        {
            var spell = ScriptableObject.CreateInstance<SpellData>();
            Assert.IsNull(spell.castVfxClip);
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void SpellData_CastSfxVariants_IsNullOrEmptyByDefault()
        {
            var spell = ScriptableObject.CreateInstance<SpellData>();
            Assert.IsTrue(spell.castSfxVariants == null || spell.castSfxVariants.Length == 0);
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void SpellVFXController_PlayWithPosition_DoesNotThrowWhenOptionalRefsAreNull()
        {
            // Arrange — _animator, _spriteRenderer, _audioSource are all null (unassigned
            // Inspector refs). PlaySequence's null-guards must prevent any NullReferenceException.
            var go = new GameObject();
            var controller = go.AddComponent<SpellVFXController>();
            var spell = ScriptableObject.CreateInstance<SpellData>();
            spell.castVfxClip = null;
            spell.castSfxVariants = null;

            // Act & Assert
            Assert.DoesNotThrow(() => controller.Play(spell, new Vector3(3f, 1f, 0f)));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(spell);
        }
    }
}
