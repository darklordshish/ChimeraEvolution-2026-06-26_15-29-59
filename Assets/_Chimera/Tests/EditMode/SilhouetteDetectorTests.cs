using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>ДЕТЕКТОР СИЛУЭТА (консилиум формы, 01.10) различает формы: тело против самого себя — IoU 1, волк против
    /// лося — заметно меньше. Без этого зелёный IoU мог бы значить «детектор ничего не видит».</summary>
    public class SilhouetteDetectorTests
    {
        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        [Test]
        public void SelfIsOne_WolfVsMooseIsNot()
        {
            var wolf = SilhouetteDetector.BodyMask(Load("Волк"), UnityEngine.Vector3.right);
            var moose = SilhouetteDetector.BodyMask(Load("Лось"), UnityEngine.Vector3.right);
            Assert.Greater(SilhouetteDetector.Compare(wolf, wolf).iou, 0.99f, "тело против себя — не 1: маска шумит");
            float wm = SilhouetteDetector.Compare(wolf, moose).iou;
            Assert.Less(wm, 0.85f, $"волк против лося {wm:F2} — детектор не различает виды");
            Assert.Greater(wm, 0.2f, $"волк против лося {wm:F2} — маска пустая или кадр мимо");
        }
    }
}
