using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// B0.1: лаборатория-бункер. Один закрытый интерьер, два внешних облика,
    /// случайное место по сиду не ближе 150 м от входа. Закрытый зал, а не атриум.
    /// </summary>
    public class ForestLabComplexTests
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

        ForestBuilder BuildLab(LabStyle style, bool complex = true, long seed = 4,
            LabEntrance entrance = LabEntrance.Rock, bool ash = false, bool ravines = false)
        {
            host = new GameObject("~ForestLabComplexProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = seed;
            builder.mapDiameter = 400f;
            builder.chunkSize = 100f;
            builder.terrainQuads = 24;
            builder.buildAsh = ash;
            builder.buildRavines = ravines;
            builder.spawnAtEntry = true;
            builder.buildLabComplex = complex;
            builder.labStyle = style;
            builder.labEntrance = entrance;
            builder.minLabDistance = 150f;
            builder.speciesPool = new List<SpeciesSO>();
            host.SetActive(true);
            builder.Build();
            return builder;
        }

        [UnityTest]
        public IEnumerator Bastion_ClosedBunker_Builds()
        {
            var b = BuildLab(LabStyle.Bastion);
            yield return null;

            Assert.IsNotNull(b.LabComplex, "комплекс собран");
            Assert.AreEqual(LabStyle.Bastion, b.ResolvedLabStyle, "стиль разрешён в Bastion");
            Assert.AreNotEqual(Vector2.zero, b.LabCenter, "место лаборатории выбрано");
            Assert.AreNotEqual(Vector3.zero, b.LabDoor, "портал LabDoor есть");
            Assert.GreaterOrEqual(Vector2.Distance(b.LabCenter, new Vector2(b.Entry.x, b.Entry.z)), 149f,
                "лаборатория не ближе 150 м от входа");
            Assert.Greater(b.LabComplex.interiorParts, 30, "интерьер собран");
            Assert.AreEqual(10, b.LabComplex.lights, "холодные + тёплые лампы");
            Assert.AreEqual(12, b.Chunks, "12 чанков профиля B0");

            NavMeshHit hit;
            Assert.IsTrue(NavMesh.SamplePosition(b.LabDoor, out hit, 20f, NavMesh.AllAreas),
                "NavMesh у портала");
            Assert.Less(Vector3.Distance(hit.position, b.LabDoor), 2f, "портал достижим");
            Assert.IsTrue(NavMesh.SamplePosition(b.LabCenter, out hit, 20f, NavMesh.AllAreas),
                "NavMesh в закрытом зале");
            Assert.Less(Vector3.Distance(hit.position, b.LabCenter), 3f,
                "центр зала ходибельный, а не двор за стеной");
        }

        [UnityTest]
        public IEnumerator InSlope_SameInterior_DifferentShell()
        {
            var b = BuildLab(LabStyle.Bastion);
            yield return null;
            int interior = b.LabComplex.interiorParts;
            int lights = b.LabComplex.lights;
            int exteriorBastion = b.LabComplex.exteriorParts;

            b.Clear();
            b = BuildLab(LabStyle.InSlope);
            yield return null;

            Assert.AreEqual(LabStyle.InSlope, b.ResolvedLabStyle, "стиль разрешён в InSlope");
            Assert.IsNotNull(b.LabComplex, "комплекс InSlope собран");
            Assert.AreEqual(interior, b.LabComplex.interiorParts, "интерьер один и тот же");
            Assert.AreEqual(lights, b.LabComplex.lights, "свет один и тот же");
            Assert.AreNotEqual(Vector3.zero, b.LabComplex.door, "портал InSlope есть");
            Assert.AreNotEqual(exteriorBastion, b.LabComplex.exteriorParts, "оболочка отличается");
        }

        [UnityTest]
        public IEnumerator Lab_ClosedRoof_NoOpenSky()
        {
            var b = BuildLab(LabStyle.Bastion);
            yield return null;
            Physics.SyncTransforms();

            float ground = b.SampleGround(b.LabCenter.x, b.LabCenter.y);
            var from = new Vector3(b.LabCenter.x, ground + 20f, b.LabCenter.y);
            Assert.IsTrue(Physics.Raycast(from, Vector3.down, out var hit, 30f),
                "над залом есть перекрытие");
            Assert.IsTrue(hit.collider.transform.IsChildOf(b.LabComplex.root.transform),
                "перекрытие — часть комплекса");
            Assert.Greater(hit.point.y, ground + 3f, "крыша выше пола, неба нет");

            var overlaps = Physics.OverlapBox(
                new Vector3(b.LabCenter.x, ground + 2f, b.LabCenter.y),
                new Vector3(6f, 0.8f, 6f));
            Assert.AreEqual(0, overlaps.Length, "в центре зала нет колонны");
        }

        [UnityTest]
        public IEnumerator AnySeed_BuildsLab_DryOrWetDeck()
        {
            foreach (long seed in new long[] { 1, 4, 9 })
            {
                var b = BuildLab(LabStyle.InSlope, seed: seed);
                yield return null;

                Assert.IsNotNull(b.LabComplex, $"сид {seed}: комплекс обязан собраться");
                Assert.AreNotEqual(Vector3.zero, b.LabDoor, $"сид {seed}: LabDoor есть");
                Assert.AreEqual(12, b.Chunks, $"сид {seed}: чанки B0");
                if (seed == 9)
                    Assert.IsFalse(b.LabComplex.wetApproach, "сид 9: сухой подход без настила");
                if (seed == 1)
                    Assert.IsTrue(b.LabComplex.wetApproach, "сид 1: запасное место с настилом");

                b.Clear();
                Object.Destroy(host);
                host = null;
                builder = null;
            }
        }

        [UnityTest]
        public IEnumerator Ravines_And_FallenTrees_Build_Without_Lairs()
        {
            var b = BuildLab(LabStyle.InSlope, seed: 9,
                entrance: LabEntrance.Underground, ravines: true);
            yield return null;

            Assert.Greater(b.Ravines, 0, "овраги построены");
            Assert.Greater(b.FallenTrees, 0, "упавшие стволы есть");
            Assert.Greater(b.VisualNests, 0, "визуальные гнёзда есть");
            Assert.AreEqual(0, b.Lairs.Count(l => l.speciesName == "VisualSnakeNest"),
                "визуальное гнездо не LairSite");
        }

        [UnityTest]
        public IEnumerator Ash_OnB0_StandScale_Builds()
        {
            var b = BuildLab(LabStyle.InSlope, seed: 9, entrance: LabEntrance.Underground, ash: true);
            yield return null;

            Assert.IsTrue(b.HasAsh, "ясень на стенде включён и поставлен");
            Assert.AreNotEqual(Vector2.zero, b.Ash.pos, "у ясеня есть место");
            Assert.IsNotNull(b.NestSite, "гнездо на ясене есть геометрией");
        }

        [UnityTest]
        public IEnumerator Damage_SameSeed_IsReproducible()
        {
            var a = BuildLab(LabStyle.InSlope, seed: 9, entrance: LabEntrance.Underground);
            yield return null;
            string first = a.LabComplex.damageSignature;
            Assert.IsFalse(string.IsNullOrEmpty(first), "подпись повреждений есть");
            Assert.Greater(a.LabComplex.damageParts, 0, "повреждения добавили детали");
            a.Clear();
            Object.Destroy(host);
            host = null;
            builder = null;

            var b = BuildLab(LabStyle.InSlope, seed: 9, entrance: LabEntrance.Underground);
            yield return null;
            Assert.AreEqual(first, b.LabComplex.damageSignature,
                "тот же сид — та же раскладка повреждений");
        }

        [UnityTest]
        public IEnumerator Underground_BuildsRamp_OnSeed9()
        {
            var b = BuildLab(LabStyle.InSlope, seed: 9, entrance: LabEntrance.Underground);
            yield return null;

            Assert.IsNotNull(b.LabComplex, "подземный вход собирается");
            Assert.AreEqual(LabEntrance.Underground, b.LabComplex.entrance, "вход — Underground");
            Assert.AreEqual(12, b.Chunks, "чанки B0");
            Assert.Greater(b.LabComplex.parts, 163, "рампа/стены добавили детали");
            Assert.Less(b.LabDoor.y, b.SampleGround(b.LabDoor.x, b.LabDoor.z) + 0.5f,
                "дверь стоит внизу спуска, а не на верхней площадке");
        }

        [UnityTest]
        public IEnumerator A_OldLab_WhenComplexOff()
        {
            var b = BuildLab(LabStyle.Bastion, complex: false);
            yield return null;

            Assert.IsNull(b.LabComplex, "старый лабиринт: комплекса нет");
            Assert.IsNotNull(b.Facility, "старый Facility на месте");
            Assert.AreEqual(Vector2.zero, b.LabCenter, "центр старой лаборатории — ноль");
            Assert.AreEqual(12, b.Chunks, "профиль B0 не изменился");
        }
    }
}
