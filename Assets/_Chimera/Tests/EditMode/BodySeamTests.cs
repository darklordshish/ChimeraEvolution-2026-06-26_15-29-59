using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СТЫКИ КАРТЫ ТЕЛ ПОСЛЕ КУСКОВ НА УЗЛАХ (17.09). Лапы волка стали брусками и каплями на концах узлов, и карта
    /// закричала «ЩЕЛЬ 434 %» на здоровых лапах. Причин две, обе в пробе, и обе ломаются молча:
    /// 1) оболочка парного места одна на обе стороны, а сторону детали решал ЗНАК центра — центр оболочки −0.0003 уводил
    ///    её влево, и правой стороне доставались одни бруски, висящие в 30 см под хребтом;
    /// 2) «ближайшей» деталью считалась та, у которой МЕНЬШЕ МОДУЛЬ расстояния — и верхняя бусина уха в 8 мм над черепом
    ///    обходила нижнюю, вросшую в череп на 5 см. Место держится за родителя той деталью, что КАСАЕТСЯ.</summary>
    public class BodySeamTests
    {
        static BodyProbe.Part P(string name, Vector3 c, Vector3 s) =>
            new BodyProbe.Part { name = name, center = c, size = s, hasRenderer = true };

        [Test]
        public void MirroredPlace_ShellOnMidline_BelongsToBothSides()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.sockets = new[]
                {
                    new BodySocket { name = "хребет" },
                    new BodySocket { name = "Руки", parent = "хребет", mirrorX = true },
                };
                var parts = new List<BodyProbe.Part>
                {
                    P("Руки", new Vector3(-0.0003f, 0.553f, 0.446f), new Vector3(0.292f, 0.795f, 0.462f)),   // оболочка поля
                    P("Руки", new Vector3(0.09f, 0.157f, 0.482f), new Vector3(0.072f, 0.223f, 0.128f)),      // брусок справа
                    P("Руки", new Vector3(-0.09f, 0.157f, 0.482f), new Vector3(0.072f, 0.223f, 0.128f)),     // брусок слева
                };
                var pl = BodyProbe.Group(so, parts);

                Assert.AreEqual(2, pl.pieces["Руки (пр)"].Count, "правой стороне не досталась оболочка — бруски висят в пустоте");
                Assert.AreEqual(2, pl.pieces["Руки (лев)"].Count);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ClosestSeam_TouchingPieceBeatsNearerGap()
        {
            var skull = new List<Bounds> { new Bounds(new Vector3(0f, 1.183f, 0.873f), new Vector3(0.238f, 0.396f, 0.425f)) };
            var ear = new List<Bounds>
            {
                new Bounds(new Vector3(0.082f, 1.321f, 0.878f), new Vector3(0.086f, 0.083f, 0.090f)),   // нижняя бусина, вросла
                new Bounds(new Vector3(0.069f, 1.421f, 0.859f), new Vector3(0.044f, 0.064f, 0.051f)),   // верхняя, 8 мм над черепом
            };
            Vector3 seam = BodyProbe.ClosestSeam(ear, skull, out _);
            float gap = Mathf.Max(seam.x, Mathf.Max(seam.y, seam.z));
            Assert.LessOrEqual(gap, 0f, "ухо касается черепа нижней бусиной, а шов взят по верхней — ложная ЩЕЛЬ");
        }
    }
}
