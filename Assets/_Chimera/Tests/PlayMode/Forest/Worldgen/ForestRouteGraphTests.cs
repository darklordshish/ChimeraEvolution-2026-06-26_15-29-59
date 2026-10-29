using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Маршрутный граф по SampleGround на 20 сидах: вход → дверь → зал.
    /// Строит настоящий ForestBuilder, поэтому проверяет тот же пол, воду и кольцо,
    /// что видит игра; NavMesh здесь не используется — он отдельный долг.
    /// </summary>
    public class ForestRouteGraphTests
    {
        GameObject host;
        ForestBuilder builder;
        DomeGenConfigSO cfg;

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            if (cfg != null) Object.Destroy(cfg);
            builder = null;
            host = null;
            cfg = null;
        }

        [UnityTest]
        public IEnumerator TwentySeeds_EntryDoorAtrium_AreConnected()
        {
            for (long seed = 1; seed <= 20; seed++)
            {
                cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
                cfg.seed = seed;
                cfg.randomSeed = false;
                cfg.mapDiameter = 2000f;
                cfg.chunkSize = 250f;

                host = new GameObject($"~ForestRouteGraph_{seed}");
                host.SetActive(false);
                builder = host.AddComponent<ForestBuilder>();
                builder.buildOnAwake = false;
                builder.randomSeed = false;
                builder.seed = seed;
                builder.mapDiameter = 2000f;
                builder.chunkSize = 250f;
                builder.terrainQuads = 8;
                builder.buildAsh = false;
                builder.buildRavines = false;
                builder.buildLabComplex = true;
                builder.buildStationLoop = true;
                builder.buildStationHill = true;
                builder.labEntrance = LabEntrance.Buried;
                builder.labStyle = LabStyle.Bastion;
                builder.minLabDistance = 150f;
                builder.spawnAtEntry = true;
                builder.speciesPool = new System.Collections.Generic.List<SpeciesSO>();
                builder.config = cfg;
                host.SetActive(true);
                builder.Build();
                yield return null;

                Assert.AreNotEqual(Vector3.zero, builder.LabDoor,
                    $"сид {seed}: станция не нашла место");
                Assert.AreNotEqual(Vector3.zero, builder.LabAtrium,
                    $"сид {seed}: LabAtrium не выставлен");

                var graph = ForestRouteGraph.Build(cfg, builder.Hydro, builder.SampleGround);
                Assert.IsTrue(ForestRouteGraph.Connected(graph, cfg.mapDiameter,
                        new Vector2(builder.Entry.x, builder.Entry.z),
                        new Vector2(builder.LabDoor.x, builder.LabDoor.z)),
                    $"сид {seed}: нет пути Entry → LabDoor; entry={builder.Entry} door={builder.LabDoor} lake={builder.Hydro.lakeCenter} r={builder.Hydro.lakeRadius}");
                Assert.IsTrue(ForestRouteGraph.Connected(graph, cfg.mapDiameter,
                        new Vector2(builder.LabDoor.x, builder.LabDoor.z),
                        new Vector2(builder.LabAtrium.x, builder.LabAtrium.z)),
                    $"сид {seed}: нет пути LabDoor → LabAtrium");

                builder.Clear();
                Object.Destroy(host);
                Object.Destroy(cfg);
                builder = null;
                host = null;
                cfg = null;
            }
        }
    }
}
