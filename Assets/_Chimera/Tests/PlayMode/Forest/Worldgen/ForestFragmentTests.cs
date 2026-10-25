using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// B0: малый стенд-фрагмент Ø400. Проверяет профиль, а не замысел:
    /// 12 чанков (4×4 минус квадратные углы), вход у подножия кольца, игрок на входе,
    /// ясень/сова не строятся, NavMesh есть у входа. Зверей и филина в B0 нет.
    /// </summary>
    public class ForestFragmentTests
    {
        GameObject host;
        ForestBuilder builder;
        GameObject probe;

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            if (probe != null) Object.Destroy(probe);
            builder = null;
            host = null;
            probe = null;
        }

        ForestBuilder Fragment()
        {
            host = new GameObject("~ForestFragmentProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = 4;
            builder.mapDiameter = 400f;
            builder.chunkSize = 100f;
            builder.terrainQuads = 24;
            builder.buildAsh = false;
            builder.spawnAtEntry = true;
            builder.speciesPool = new System.Collections.Generic.List<SpeciesSO>();
            host.SetActive(true);
            return builder;
        }

        [UnityTest]
        public IEnumerator B0_Profile_BuildsTwelveChunks_EntryAndNoAsh()
        {
            probe = new GameObject("~ForestFragmentPlayerProbe");
            probe.AddComponent<CharacterController>();

            var b = Fragment();
            yield return null;
            b.Build();
            yield return null;

            Assert.AreEqual(12, b.Chunks, "Ø400/chunk 100: 4×4 минус четыре угловых чанка");
            Assert.That(b.Trees, Is.InRange(450, 550), "бюджет деревьев Ø400/сид 4 не раздут");
            Assert.IsTrue(b.BuiltRoot != null, "корень мира собран");
            Assert.IsNull(b.NestSite, "B0 без филина: совиного гнезда нет");
            Assert.AreEqual(Vector2.zero, b.Ash.pos, "B0 без ясеня: Ash остаётся default");
            Assert.AreNotEqual(Vector3.zero, b.Entry, "вход у подножия кольца вычислен");
            var flatProbe = new Vector3(probe.transform.position.x, 0f, probe.transform.position.z);
            var flatSpawn = new Vector3(b.SpawnPoint.x, 0f, b.SpawnPoint.z);
            var flatEntry = new Vector3(b.Entry.x, 0f, b.Entry.z);
            Assert.Less(Vector3.Distance(flatProbe, flatSpawn), 0.2f,
                "spawnAtEntry поставил игрока в точку спавна у входа");
            Assert.Greater(Vector3.Distance(flatSpawn, flatEntry), 4f,
                "игрок стоит на 6 м в чашу от портала, а не внутри косяка");
            Assert.AreEqual(1f, probe.transform.position.y - b.SampleGround(flatProbe.x, flatProbe.z), 0.2f,
                "корень игрока — на метр выше земли (центр капсулы)");
            Vector2 lab = b.Facility != null ? b.Facility.labCenter : Vector2.zero;
            Assert.Greater(Vector2.Distance(new Vector2(b.Entry.x, b.Entry.z), lab), 50f,
                "вход не совпал с двором лаборатории");

            NavMeshHit hit;
            Assert.IsTrue(NavMesh.SamplePosition(b.Entry, out hit, 20f, NavMesh.AllAreas),
                $"NavMesh есть у входа {b.Entry}");
        }
    }
}
