using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// G7 шаг 2: станция — место в рельефе, а не коробка на сыром поле.
    /// После форм (<c>ShapeGround</c>) она подтверждается ВНУТРИ `stationReserve`,
    /// `platformY` читается из того же пола, `Bastion` и станционный холм в v2
    /// не строятся, вход `Auto` разрешается по рельефу.
    /// </summary>
    public class StationOnReliefTests
    {
        GameObject host;
        ForestBuilder builder;

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        [UnityTest]
        public IEnumerator Station_OnShapeGround_InsideReserve_NotBastion()
        {
            host = new GameObject("~StationOnReliefProbe");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            host.SetActive(true);
            builder.Build();
            yield return null;

            Assert.IsNotNull(builder.BuiltRoot, "v2-мир не собран");
            Assert.IsTrue(builder.Terrain.TargetOrder, "v2 обязан дать целевой порядок слоёв");
            Assert.IsTrue(builder.HasStation, "станция подтверждена на ShapeGround");
            Assert.IsNotNull(builder.LabComplex, "комплекс станции не построен");

            Assert.AreNotEqual(LabStyle.Bastion, builder.ResolvedLabStyle,
                "v2: Auto не разрешается в Bastion");
            Assert.That(builder.ResolvedLabEntrance, Is.EqualTo(LabEntrance.Rock)
                .Or.EqualTo(LabEntrance.Underground).Or.EqualTo(LabEntrance.Buried),
                "вход Auto — Rock/Underground/Buried по рельефу");

            Assert.IsTrue(builder.Masks.Has("stationReserve"),
                "провизорный регион stationReserve существует");
            Assert.IsTrue(builder.Masks.Has("station"),
                "финальная маска station появляется после поиска");
            Assert.IsTrue(builder.Masks.ContainsPoint(builder.LabCenter, "stationReserve"),
                "станция стоит только внутри stationReserve");

            Vector2 entry = new Vector2(builder.Entry.x, builder.Entry.z);
            Vector2 door = new Vector2(builder.LabDoor.x, builder.LabDoor.z);
            float doorChord = Vector2.Distance(entry, door);
            Assert.AreEqual(909f, doorChord, 2f,
                "1A: LabDoor — неподвижный якорь на хорде 909 м");
            Assert.GreaterOrEqual(doorChord, 150f, "LabDoor ≥150 м от Entry");
            float centerChord = Vector2.Distance(entry, builder.LabCenter);
            Assert.That(centerChord, Is.InRange(900f, 950f),
                "Entry → LabCenter — факт якоря, не порог");

            float shape = builder.ShapeGround(builder.LabCenter.x, builder.LabCenter.y);
            float depth = DomeLabComplex.DepthFor(builder.ResolvedLabEntrance);
            float expected = Mathf.Max(shape - depth, builder.Hydro.lakeLevel + 0.5f);
            Assert.AreEqual(expected, builder.PlatformY, 0.05f,
                "platformY читается из ShapeGround уже найденного пятна");

            Assert.IsTrue(builder.Masks.ContainsPoint(builder.LabCenter, "station"),
                "станция лежит в своей финальной маске");
            Assert.LessOrEqual(Vector2.Distance(
                    new Vector2(builder.LabCenter.x, builder.LabCenter.y),
                    new Vector2(builder.Entry.x, builder.Entry.z)),
                builder.mapDiameter, "станция внутри мира");
            Assert.IsTrue(builder.Trace.Text.Contains("station"),
                "GenTrace пишет узел station");
            Assert.IsFalse(builder.Trace.Text.Contains("station=none"),
                "станция подтверждена, а не «станции нет»");

            // Плато станции — форма landform, не DomeHill.
            bool plateau = false;
            if (builder.Composition != null)
                foreach (var p in builder.Composition.declared)
                    if (p.role == DomeComposition.Role.StationPlateau) plateau = p.applied;
            Assert.IsTrue(plateau, "плато станции применено слоем landform");
            Assert.IsFalse(builder.Hill.valid, "в v2 станционный холм DomeHill не строится");
            Assert.IsNull(GameObject.Find("StationHill"), "нет видимого StationHill в v2");

            Assert.Pass($"station: style={builder.ResolvedLabStyle}, " +
                $"entrance={builder.ResolvedLabEntrance}, doorChord={doorChord:F1}, " +
                $"centerChord={centerChord:F1}, platformY={builder.PlatformY:F2}");
        }
    }
}
