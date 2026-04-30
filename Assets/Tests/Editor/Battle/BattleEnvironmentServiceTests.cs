using NUnit.Framework;
using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class BattleEnvironmentServiceTests
    {
        private BattleEnvironmentService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new BattleEnvironmentService();
        }

        [Test]
        public void Apply_WithNullData_DoesNotThrow()
        {
            var go = new GameObject("TestBackground", typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            sr.color = Color.red;

            Assert.DoesNotThrow(() => _service.Apply(null, sr));

            // Renderer untouched
            Assert.AreEqual(Color.red, sr.color);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Apply_WithNullRenderer_DoesNotThrow()
        {
            var data = ScriptableObject.CreateInstance<BattleEnvironmentData>();
            data.backgroundSprite = null;
            data.ambientTint = Color.blue;

            Assert.DoesNotThrow(() => _service.Apply(data, null));

            Object.DestroyImmediate(data);
        }

        [Test]
        public void Apply_WithValidData_AssignsSpriteAndTint()
        {
            var expectedSprite = Sprite.Create(
                Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);

            var data = ScriptableObject.CreateInstance<BattleEnvironmentData>();
            data.backgroundSprite = expectedSprite;
            data.ambientTint = new Color(0.5f, 0.6f, 0.7f);

            var go = new GameObject("TestBackground", typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();

            _service.Apply(data, sr);

            Assert.AreSame(expectedSprite, sr.sprite);
            Assert.AreEqual(new Color(0.5f, 0.6f, 0.7f), sr.color);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Apply_NullSpriteInData_AssignsNullToRenderer()
        {
            var data = ScriptableObject.CreateInstance<BattleEnvironmentData>();
            data.backgroundSprite = null;
            data.ambientTint = Color.green;

            var go = new GameObject("TestBackground", typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);

            _service.Apply(data, sr);

            Assert.IsNull(sr.sprite);
            Assert.AreEqual(Color.green, sr.color);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }
    }
}
