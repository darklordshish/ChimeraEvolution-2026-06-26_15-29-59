using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>Режимы камеры (геймдизайнер 05.11): V идёт по кругу из трёх; близкая камера с нулём в сцене берёт значение по умолчанию.</summary>
    public class CameraViewTests
    {
        [Test]
        public void V_CyclesThroughThreeModes()
        {
            Assert.AreEqual(PlayerController.ViewMode.Near, PlayerController.Next(PlayerController.ViewMode.Far));
            Assert.AreEqual(PlayerController.ViewMode.First, PlayerController.Next(PlayerController.ViewMode.Near));
            Assert.AreEqual(PlayerController.ViewMode.Far, PlayerController.Next(PlayerController.ViewMode.First));
        }

        [Test]
        public void NearOffset_ZeroMeansDefault_SetValueStays()
        {
            Assert.AreEqual(CameraFollow.NearDefault, CameraFollow.NearOr(Vector3.zero));
            var set = new Vector3(0f, 3f, -4.5f);
            Assert.AreEqual(set, CameraFollow.NearOr(set));
        }
    }
}
