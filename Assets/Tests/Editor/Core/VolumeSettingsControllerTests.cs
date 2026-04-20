using NUnit.Framework;
using Axiom.Core;

namespace CoreTests
{
    [TestFixture]
    public class VolumeSettingsControllerTests
    {
        [Test]
        public void GetMasterVolume_NullManager_Returns1()
        {
            var controller = new VolumeSettingsController(null);
            Assert.AreEqual(1f, controller.GetMasterVolume());
        }

        [Test]
        public void GetMusicVolume_NullManager_Returns1()
        {
            var controller = new VolumeSettingsController(null);
            Assert.AreEqual(1f, controller.GetMusicVolume());
        }

        [Test]
        public void GetSfxVolume_NullManager_Returns1()
        {
            var controller = new VolumeSettingsController(null);
            Assert.AreEqual(1f, controller.GetSfxVolume());
        }

        [Test]
        public void SetMasterVolume_NullManager_DoesNotThrow()
        {
            var controller = new VolumeSettingsController(null);
            Assert.DoesNotThrow(() => controller.SetMasterVolume(0.5f));
        }

        [Test]
        public void SetMusicVolume_NullManager_DoesNotThrow()
        {
            var controller = new VolumeSettingsController(null);
            Assert.DoesNotThrow(() => controller.SetMusicVolume(0.5f));
        }

        [Test]
        public void SetSfxVolume_NullManager_DoesNotThrow()
        {
            var controller = new VolumeSettingsController(null);
            Assert.DoesNotThrow(() => controller.SetSfxVolume(0.5f));
        }
    }
}
