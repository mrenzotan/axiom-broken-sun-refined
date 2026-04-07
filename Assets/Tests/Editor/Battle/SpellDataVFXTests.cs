using NUnit.Framework;
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
    }
}
