using NUnit.Framework;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Data.Tests
{
    public class BattleEnvironmentDataTests
    {
        [Test]
        public void DefaultTint_IsWhite()
        {
            var data = ScriptableObject.CreateInstance<BattleEnvironmentData>();
            Assert.AreEqual(Color.white, data.ambientTint);
        }

        [Test]
        public void DefaultBackgroundSprite_IsNull()
        {
            var data = ScriptableObject.CreateInstance<BattleEnvironmentData>();
            Assert.IsNull(data.backgroundSprite);
        }

        [Test]
        public void CreateAssetMenuAttribute_IsPresent()
        {
            var attrs = typeof(BattleEnvironmentData).GetCustomAttributes(
                typeof(CreateAssetMenuAttribute), false);
            Assert.IsNotEmpty(attrs);
            var attr = (CreateAssetMenuAttribute)attrs[0];
            Assert.AreEqual("Axiom/Data/Battle Environment Data", attr.menuName);
        }
    }
}
