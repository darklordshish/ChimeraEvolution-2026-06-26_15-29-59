using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S3 §3.1–§3.3: «свободно» доказывается ПЕРЕСЕЧЕНИЕМ ГАБАРИТОВ, а не расстоянием
    /// до центра. Фикстуры синтетические: проверяется сама алгебра масок, а не карта
    /// (места расставит S2/S5). Отрицательные случаи — из плана S3 шаг 4.
    /// </summary>
    public class DomeMaskTests
    {
        [Test]
        public void CircleCircle_OverlapAndApart()
        {
            var a = DomeFootprint.Circle(Vector2.zero, 10f);
            var b = DomeFootprint.Circle(new Vector2(15f, 0f), 6f);
            var c = DomeFootprint.Circle(new Vector2(40f, 0f), 6f);
            Assert.IsTrue(DomeFootprints.Overlaps(a, b), "круги 10+6 при d=15 пересекаются");
            Assert.IsFalse(DomeFootprints.Overlaps(a, c), "круги при d=40 не пересекаются");
        }

        [Test]
        public void CircleObb_RespectsRotation()
        {
            var obb = DomeFootprint.Obb(Vector2.zero, 45f, 10f, 4f);
            // По оси X на 11 м: в локальном кадре 45° это вылет за полуширину.
            Assert.IsFalse(DomeFootprints.Overlaps(DomeFootprint.Circle(new Vector2(11f, 0f), 0.5f), obb),
                "точка по X на 11 м вне повёрнутого прямоугольника");
            // Вдоль локальной +Z (yaw 45° → направление (sin45, cos45)) — внутри.
            var inside = new Vector2(Mathf.Sin(Mathf.Deg2Rad * 45f), Mathf.Cos(Mathf.Deg2Rad * 45f)) * 3f;
            Assert.IsTrue(DomeFootprints.Overlaps(DomeFootprint.Circle(inside, 0.5f), obb),
                "точка вдоль локальной +Z внутри");
        }

        [Test]
        public void CapsuleCapsule_OverlapAndApart()
        {
            var a = DomeFootprint.Capsule(new Vector2(-20f, 0f), new Vector2(20f, 0f), 4f);
            var b = DomeFootprint.Capsule(new Vector2(-20f, 7f), new Vector2(20f, 7f), 4f);
            var c = DomeFootprint.Capsule(new Vector2(-20f, 20f), new Vector2(20f, 20f), 4f);
            Assert.IsTrue(DomeFootprints.Overlaps(a, b), "капсулы при зазоре 7−8 пересекаются");
            Assert.IsFalse(DomeFootprints.Overlaps(a, c), "капсулы при зазоре 20 не пересекаются");
        }

        [Test]
        public void Polygon_UsesRaster()
        {
            var square = DomeFootprint.Polygon(new[]
            {
                new Vector2(-10f, -10f), new Vector2(10f, -10f),
                new Vector2(10f, 10f), new Vector2(-10f, 10f),
            });
            Assert.IsTrue(DomeFootprints.Overlaps(square, DomeFootprint.Circle(Vector2.zero, 2f)),
                "круг в центре полигона пересекается");
            Assert.IsFalse(DomeFootprints.Overlaps(square, DomeFootprint.Circle(new Vector2(40f, 0f), 2f)),
                "круг снаружи не пересекается");
        }

        [Test]
        public void RingBand_RejectsZone_ThatCentreCheckMisses()
        {
            var masks = new DomeMasks().RingBand(90f, 150f);
            // Зона R30 → маска R+20=50 на r=0.78·rin=93.6: центр далеко от rin,
            // а габарит уже лежит на кольце — ровно дефект §1.4 п.5 на Ø400.
            var zone = DomeMasks.ZoneMask(new Vector2(93.6f, 0f), 30f);
            Assert.IsTrue(masks.BlocksForm(zone), "зона R+20 на кольце обязана быть отвергнута");
            var centre = DomeMasks.ZoneMask(Vector2.zero, 30f);
            Assert.IsFalse(masks.BlocksForm(centre), "зона в центре кольцо не задевает");
        }

        [Test]
        public void StationMask_RejectsHillByFootprint()
        {
            var masks = new DomeMasks()
                .Add("station", DomeFootprint.Obb(Vector2.zero, 0f, 23f, 18f));
            var hill = DomeFootprint.Circle(new Vector2(60f, 0f), 70f);
            Assert.IsTrue(masks.BlocksForm(hill),
                "холм R70 с центром в 60 м от станции пересекает плиту (центровая проверка это пропускала)");
            var far = DomeFootprint.Circle(new Vector2(120f, 0f), 70f);
            Assert.IsFalse(masks.BlocksForm(far), "холм R70 в 120 м станцию не задевает");
        }

        [Test]
        public void Shell_InsideZone_IsAllowed_AndAtCentreIsNot()
        {
            var shellEdge = DomeMasks.ShellMask(new Vector2(40f, 0f), 0f, 5f, 4f);
            var shellCentre = DomeMasks.ShellMask(Vector2.zero, 0f, 5f, 4f);
            Assert.IsTrue(DomeMasks.ShellFitsZone(shellEdge, Vector2.zero, 50f),
                "остов у кромки зоны — разрешённое вложение, а не пересечение");
            Assert.IsFalse(DomeMasks.ShellFitsZone(shellCentre, Vector2.zero, 50f),
                "остов в центре зоны — не «у кромки»");
            Assert.IsTrue(DomeFootprints.Overlaps(shellEdge,
                    DomeFootprint.Circle(Vector2.zero, 50f)),
                "при этом как габариты они пересекаются: это вложение, не запрет");
        }

        [Test]
        public void Common_OnPath_IsAllowed()
        {
            var common = DomeMasks.CommonMask(Vector2.zero, 10f);
            Assert.IsTrue(DomeMasks.CommonNearPath(common, new Vector2(-100f, 0f), new Vector2(100f, 0f)),
                "общая зона на тропе разрешена");
            var far = DomeMasks.CommonMask(new Vector2(0f, 100f), 10f);
            Assert.IsFalse(DomeMasks.CommonNearPath(far, new Vector2(-100f, 0f), new Vector2(100f, 0f)),
                "общая зона в 100 м от тропы — нет");
        }

        /// <summary>
        /// P0.6: минимум расстояния до OBB/полигона — точное расстояние до ребра,
        /// а не минимум вершин. Длинный тонкий прямоугольник через центр кольца
        /// не должен «проскакивать» между вершинами.
        /// </summary>
        [Test]
        public void DistanceRange_ObbAndPolygon_UseEdgesNotVertices()
        {
            var masks = new DomeMasks().RingBand(90f, 150f);
            var obb = DomeFootprint.Obb(Vector2.zero, 0f, 200f, 2f);
            obb.DistanceRangeFromOrigin(out float minObb, out float maxObb);
            Assert.AreEqual(0f, minObb, 1e-3f, "OBB накрывает центр — min = 0");
            Assert.Greater(maxObb, 150f, "дальний край OBB за кольцом");
            Assert.IsTrue(masks.BlocksForm(obb), "OBB через кольцо обязан пересекать полосу");

            var poly = DomeFootprint.Polygon(new[]
            {
                new Vector2(-200f, -2f), new Vector2(200f, -2f),
                new Vector2(200f, 2f), new Vector2(-200f, 2f),
            });
            poly.DistanceRangeFromOrigin(out float minPoly, out _);
            Assert.AreEqual(0f, minPoly, 1e-3f, "полигон накрывает центр — min = 0");
            Assert.IsTrue(masks.BlocksForm(poly), "полигон через кольцо обязан пересекать полосу");
        }

        [Test]
        public void GateAndCorridor_AreProtectedMasks()
        {
            var masks = new DomeMasks()
                .Add("gate", DomeFootprint.Circle(new Vector2(100f, 0f), 8f))
                .Add("corridor", DomeFootprint.Capsule(new Vector2(-100f, 0f),
                    new Vector2(100f, 0f), 14f));
            Assert.IsTrue(masks.BlocksForm(DomeFootprint.Circle(new Vector2(100f, 0f), 30f)),
                "landform на воротах обязан быть отвергнут");
            Assert.IsTrue(masks.BlocksForm(DomeFootprint.Circle(Vector2.zero, 20f)),
                "landform на коридоре обязан быть отвергнут");
        }

        [Test]
        public void FormFootprint_ConvertsDiscAndCapsule()
        {
            var disc = DomeMasks.ToFootprint(DomeForms.Disc(Vector2.zero, 120f, 9f));
            Assert.AreEqual(DomeFootKind.Circle, disc.kind, "диск — круг");
            Assert.AreEqual(60f, disc.radius, 1e-4f, "радиус = D/2");

            var capsule = DomeMasks.ToFootprint(
                DomeForms.Capsule(Vector2.zero, Vector2.right, 80f, 30f, 4f));
            Assert.AreEqual(DomeFootKind.Capsule, capsule.kind, "капсула — капсула");
            Assert.AreEqual(15f, capsule.radius, 1e-4f, "радиус = Run = (D−бровка)/2");
        }
    }
}
