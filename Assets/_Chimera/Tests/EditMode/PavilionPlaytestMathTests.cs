using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class PavilionPlaytestMathTests
    {
        [Test]
        public void StepClip_ShrinksAtOnce_GrowsSmoothly()
        {
            Assert.AreEqual(0.3f, CameraFollow.StepClip(1f, 0.3f, 0.016f, 1f), 1e-5f, "преграда — втянулась сразу");
            Assert.AreEqual(0.31f, CameraFollow.StepClip(0.3f, 1f, 0.01f, 1f), 1e-5f, "преграды нет — выезжает плавно");
            Assert.AreEqual(1f, CameraFollow.StepClip(0.99f, 1f, 1f, 1f), 1e-5f, "и не дальше полного пути");
        }

        [Test]
        public void CompassText_DistanceAndRose()
        {
            Assert.AreEqual("лаборатория: 100 м, С", DebugHud.CompassText(Vector3.zero, new Vector3(0f, 5f, 100f)));
            Assert.AreEqual("лаборатория: 50 м, В", DebugHud.CompassText(Vector3.zero, new Vector3(50f, 0f, 0f)));
            Assert.AreEqual("лаборатория: 100 м, ЮЗ", DebugHud.CompassText(Vector3.zero, new Vector3(-70.7f, 0f, -70.7f)));
        }

        [Test]
        public void LabCompass_NoExit_NoLine()
        {
            var was = BossChimeraSpawner.ExitProvider;
            BossChimeraSpawner.ExitProvider = null;
            try { Assert.IsNull(DebugHud.LabCompass(Vector3.zero)); }
            finally { BossChimeraSpawner.ExitProvider = was; }
        }

        [Test]
        public void RisePoint_NoNavMesh_StaysAtDeathPoint()
        {
            var p = new Vector3(5f, 2f, 7f);
            Assert.AreEqual(p, PlayerDeath.RisePoint(p, 1f));
        }
    }
}
