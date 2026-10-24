using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Срез A леса (24.10): ForestBuilder собирает мир РАНТАЙМОМ через DomeWorld, а не
    /// редакторным превью. Проверяется plumbing: корень, вода, корпуса, гнездо, NavMesh,
    /// чистота Clear и повторная сборка.
    /// <para>
    /// Мир намеренно малый: Ø300, 6 квадов на чанк. Полная карта Ø2000 — витрина, в тестах
    /// она не нужна; на малом диаметре те же правила видны за секунды.
    /// </para>
    /// <para>
    /// Каждый тест убирает за собой: Clear() возвращает статику (DomeFlora/DomeFacility
    /// кэши, небо, мокрость), Destroy снимает пробный объект. Без Clear статика переживает
    /// тест и красит следующие сборки (гоча проекта).
    /// </para>
    /// </summary>
    public class ForestBuilderRuntimeTests
    {
        const float SmallDiameter = 300f;
        const int SmallQuads = 6;

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

        /// <summary>
        /// Пробный ForestBuilder: выключен на время настройки, сборка — только явным Build().
        /// NavMeshSurface приезжает сам (RequireComponent).
        /// </summary>
        ForestBuilder SmallWorld(long seed = 1337L)
        {
            host = new GameObject("~ForestBuilderRuntime");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = seed;
            builder.mapDiameter = SmallDiameter;
            builder.terrainQuads = SmallQuads;
            host.SetActive(true);
            return builder;
        }

        [UnityTest]
        public IEnumerator Build_SmallWorld_CreatesRootWorldAndNav()
        {
            var b = SmallWorld();
            yield return null;

            b.Build();
            yield return null; // бейк NavMesh и Destroy прошлого кадра — до чтения

            Assert.IsNotNull(b.BuiltRoot, "корень мира собран");
            Assert.Greater(b.BuiltRoot.transform.childCount, 0, "у корня есть содержимое");
            Assert.IsNotNull(b.Hydro, "гидрология есть (озеро/река)");
            Assert.IsNotNull(b.Facility, "корпуса лаборатории есть");
            Assert.IsNotNull(b.NestSite, "гнездо совы есть");

            NavMeshHit hit;
            bool onNav = NavMesh.SamplePosition(b.SpawnPoint, out hit, 20f, NavMesh.AllAreas);
            Assert.IsTrue(onNav, $"NavMesh есть у точки спавна {b.SpawnPoint}");
        }

        [UnityTest]
        public IEnumerator Clear_RemovesWorldAndNav_AndCanRebuild()
        {
            var b = SmallWorld();
            yield return null;
            b.Build();
            yield return null;

            var surface = host.GetComponent<NavMeshSurface>();
            Assert.IsNotNull(surface, "NavMeshSurface приехал через RequireComponent");
            var firstRoot = b.BuiltRoot;
            Assert.IsNotNull(firstRoot, "первый корень собран");
            Assert.IsNotNull(surface.navMeshData, "NavMesh запёкся");
            var registry = host.GetComponent<LairRegistry>();
            Assert.IsNotNull(registry, "реестр логовищ появился вместе с миром");
            Assert.AreEqual(1, registry.Count, "гнездо совы в реестре");

            b.Clear();
            yield return null;

            Assert.IsNull(b.BuiltRoot, "Clear снимает корень");
            Assert.IsNull(b.Hydro, "Clear снимает воду");
            Assert.IsNull(surface.navMeshData, "Clear снимает данные NavMesh");
            Assert.AreEqual(0, registry.Count, "Clear снимает гнездо с реестра, а не бросает его снесённым");

            b.Build();
            yield return null;

            Assert.IsNotNull(b.BuiltRoot, "мир собирается заново после Clear");
            Assert.AreNotSame(firstRoot, b.BuiltRoot, "новый корень, а не прежний");
        }

        [UnityTest]
        public IEnumerator SameSeed_SameWorldFacts()
        {
            var b = SmallWorld(seed: 1337L);
            yield return null;
            b.Build();
            yield return null;

            Assert.AreEqual(1337, b.ResolvedSeed, "сид разрешён");
            Vector2 lakeCenter = b.Hydro.lakeCenter;
            float lakeRadius = b.Hydro.lakeRadius;
            int facilityParts = b.Facility.parts.Count;
            Vector3 nestPos = b.NestSite.transform.position;

            b.Clear();
            yield return null;
            b.Build();
            yield return null;

            Assert.Less(Vector2.Distance(lakeCenter, b.Hydro.lakeCenter), 1e-4f,
                "озеро на том же месте");
            Assert.AreEqual(lakeRadius, b.Hydro.lakeRadius, 1e-4f, "радиус озера тот же");
            Assert.AreEqual(facilityParts, b.Facility.parts.Count, "раскладка корпусов та же");
            Assert.Less(Vector3.Distance(nestPos, b.NestSite.transform.position), 1e-4f,
                "гнездо совы на том же месте");
        }

        [UnityTest]
        public IEnumerator LabPad_IsFlatAndDry()
        {
            var b = SmallWorld();
            yield return null;
            b.Build();
            yield return null;

            float lake = b.Hydro.lakeLevel;
            float center = b.SampleGround(0f, 0f);

            Assert.Greater(center, lake + 0.4f, "двор лаборатории выше уровня воды");
            Assert.Less(Mathf.Abs(b.SampleGround(5f, 0f) - center), 0.05f,
                "площадка плоская в пределах R20");
            Assert.Greater(b.SpawnPoint.y, lake + 1.0f, "точка спавна над водой");
        }

        [UnityTest]
        public IEnumerator Nest_IsOwlLair()
        {
            var b = SmallWorld();
            yield return null;
            b.Build();
            yield return null;

            var nest = b.NestSite;
            Assert.IsNotNull(nest, "гнездо совы поставлено");
            Assert.AreEqual("Сова", nest.speciesName, "вид гнезда");
            Assert.AreEqual(LairSite.LairAreaType.Nest, nest.areaType, "тип места — гнездо");
            Assert.AreEqual(2, nest.capacity, "кап гнезда");
            Assert.Greater(nest.transform.position.y, 30f, "гнездо в кроне (на 36 м), а не на земле");

            float ground = b.SampleGround(nest.transform.position.x, nest.transform.position.z);
            Assert.That(nest.transform.position.y - ground, Is.InRange(30f, 42f),
                "гнездо на высоте NestHeight (36 м) над землёй: допуск на 12 м вбок от ствола");

            var registry = host.GetComponent<LairRegistry>();
            Assert.IsNotNull(registry, "реестр логовищ заведён на builder");
            Assert.AreEqual(1, registry.Count, "гнездо совы зарегистрировано ровно одно");
        }

        [UnityTest]
        public IEnumerator PlayerProbe_MovesToSpawn()
        {
            // ForestBuilder ищет PlayerController, иначе CharacterController. Чужая
            // проба в сцене увела бы постановку на себя — проверяем, что сцена пуста.
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController>(),
                "перед тестом в сцене не должно быть PlayerController");
            Assert.AreEqual(0, Object.FindObjectsByType<CharacterController>().Length,
                "перед тестом в сцене не должно быть чужих CharacterController");

            probe = new GameObject("~PlayerProbe");
            probe.AddComponent<CharacterController>();

            var b = SmallWorld();
            yield return null;
            b.Build();
            yield return null;

            Assert.Less(Vector3.Distance(probe.transform.position, b.SpawnPoint), 0.2f,
                "проба встала в точку спавна мира (гоча CharacterController не сбила позицию)");
        }
    }
}
