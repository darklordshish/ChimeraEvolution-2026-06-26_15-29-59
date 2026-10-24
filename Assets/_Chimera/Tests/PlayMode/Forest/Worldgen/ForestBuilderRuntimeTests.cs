using System.Collections;
using System.Collections.Generic;
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
        readonly List<Object> trash = new List<Object>();
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            foreach (var go in spawned) if (go != null) Object.Destroy(go);
            spawned.Clear();
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
            if (host != null) Object.Destroy(host);
            if (probe != null) Object.Destroy(probe);
            builder = null;
            host = null;
            probe = null;
        }

        /// <summary>
        /// Синтетический вид для спавнера (как `SpawnerHookTests.MakeSpecies`): настоящих
        /// ассетов не трогаем. Имя — контракт: спавнер ищет вид по `speciesName` логова.
        /// </summary>
        SpeciesSO MakeSpecies(string name)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 60;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            so.organs = new[] { new Organ { organName = name + "-shard", slot = "Руки", cost = 2 } };
            trash.Add(so);
            return so;
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

            // Срез C: точки купола стали логовами, мир принёс живой спавнер.
            // autoTick — то, чем логова живут в игре; без него очередь не капает (гоча).
            Assert.IsNotNull(b.Spawner, "спавнер логовищ появился вместе с миром");
            Assert.IsTrue(b.Spawner.autoTick, "в мире autoTick включён: логова тикают сами");
            Assert.IsNotNull(b.Registry, "реестр логовищ заведён на builder");
            Assert.AreEqual(b.Lairs.Count, b.Registry.Count,
                "Lairs и Registry держат ОДНО множество: разошлись — часть логов потеряна");
            Assert.Greater(b.Lairs.Count, 0, "в мире есть хотя бы совиное гнездо");

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
            Assert.AreEqual(b.Lairs.Count, registry.Count,
                "до Clear реестр и список логов совпадают");
            Assert.Greater(registry.Count, 0, "в мире есть логова (хотя бы совиное)");
            Assert.IsTrue(b.Spawner.autoTick, "до Clear спавнер тикает");

            b.Clear();
            yield return null;

            Assert.IsNull(b.BuiltRoot, "Clear снимает корень");
            Assert.IsNull(b.Hydro, "Clear снимает воду");
            Assert.IsNull(surface.navMeshData, "Clear снимает данные NavMesh");
            Assert.AreEqual(0, registry.Count, "Clear снимает гнездо с реестра, а не бросает его снесённым");
            Assert.AreEqual(0, b.Lairs.Count, "Clear чистит и список логов");
            Assert.IsFalse(b.Spawner.autoTick,
                "Clear выключает тик: иначе следующий Update дёрнул бы Tick по снесённым сайтам");

            b.Build();
            yield return null;

            Assert.IsNotNull(b.BuiltRoot, "мир собирается заново после Clear");
            Assert.AreNotSame(firstRoot, b.BuiltRoot, "новый корень, а не прежний");
            Assert.IsTrue(b.Spawner.autoTick, "после пересборки тик снова включён");
            Assert.AreEqual(b.Lairs.Count, registry.Count,
                "после пересборки реестр снова совпадает со списком логов");
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
            Assert.AreEqual(b.Lairs.Count, registry.Count,
                "реестр и список логов — одно множество");
            Assert.IsFalse(nest.spawnEnabled,
                "сова в реестре БЕЗ спавна: тела филина нет, рожать из пула пустое тело нельзя");
            CollectionAssert.Contains(registry.Sites, nest,
                "гнездо совы зарегистрировано (иначе страх и дом мимо реестра)");
        }

        /// <summary>
        /// Срез C: устья и гнёзда склона — настоящие логова «Змея» в реестре; совиное
        /// гнездо — тоже в реестре, но без спавна. Закрепляем ПРАВИЛА, а не числа мира:
        /// на малом диаметре устьев может не найтись, и это нормально.
        /// </summary>
        [UnityTest]
        public IEnumerator Lairs_AreSnakeSitesAndRegistered()
        {
            var b = SmallWorld();
            yield return null;
            b.Build();
            yield return null;

            Assert.IsNotNull(b.Registry, "реестр логовищ заведён");
            int owl = 0;
            foreach (var site in b.Lairs)
            {
                Assert.IsNotNull(site, "в списке логов нет дыр");
                if (site == b.NestSite)
                {
                    owl++;
                    Assert.AreEqual("Сова", site.speciesName, "совиное гнездо — вид «Сова»");
                    Assert.AreEqual(LairSite.LairAreaType.Nest, site.areaType,
                        "совиное гнездо — тип Nest");
                    Assert.IsFalse(site.spawnEnabled, "сова не рожает: тела филина нет");
                }
                else
                {
                    Assert.AreEqual("Змея", site.speciesName,
                        "каждое срез-логово — змеиное (устье или гнездо склона)");
                    Assert.IsTrue(site.areaType == LairSite.LairAreaType.Den
                                  || site.areaType == LairSite.LairAreaType.Nest,
                        "змеиное логово бывает только устьем (Den) или гнездом (Nest)");
                    Assert.IsTrue(site.spawnEnabled, "змеиное логово рожает");

                    // Ноль в сериализованном поле читается как «не настроено» (гоча):
                    // сборка обязана подставить дефолты среза, а не оставить capacity 0.
                    Assert.AreEqual(3, site.capacity, "кап змеиного логова — дефолт среза");
                    Assert.AreEqual(2, site.tier, "tier змеиного логова");
                    Assert.AreEqual(12f, site.homeRadius, 1e-4f, "радиус дома");
                    Assert.AreEqual(3f, site.spawnRadius, 1e-4f, "радиус рождения");
                    Assert.AreEqual(18f, site.fearRadius, 1e-4f, "радиус страха");
                    Assert.AreEqual(30f, site.respawnSeconds, 1e-4f, "перезарядка логова");
                }
                CollectionAssert.Contains(b.Registry.Sites, site,
                    $"логова «{site.name}» нет в реестре — спавнер его не увидит");
            }

            Assert.AreEqual(b.Lairs.Count, b.Registry.Count,
                "Lairs и Registry держат одно и то же множество");
            Assert.LessOrEqual(owl, 1, "совиное гнездо в мире одно");
        }

        /// <summary>
        /// Спавнер мира рожает ИЗ ОЧЕРЕДИ ЛОГОВА и ставит зверя НА ЗЕМЛЮ, а не на Y логова.
        /// Это то, чего старый `SpawnPos` не умел (гоча: на склоне зверь в воздухе/в скале).
        /// <para>
        /// Чужие логова мира на время теста сняты с реестра: у нас ОДИН тик, и если бы
        /// тикали все, `lastSpawn` зависел бы от того, сколько устьев нашёл сид.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Spawner_SpawnsFromLair_OnGround()
        {
            var b = SmallWorld();
            yield return null;

            // Пул ДО Build: ForestBuilder передаёт его спавнеру при сборке мира.
            b.speciesPool.Add(MakeSpecies("Змея"));

            b.Build();
            // Кадра НЕ ждём: autoTick уже включён, и первый же Update родил бы из настоящих
            // устьев мира — их в этом тесте быть не должно. Реестр снимаем синхронно.
            Assert.IsNotNull(b.Spawner, "спавнер мира собран");
            b.Registry.Clear();

            var lairGo = new GameObject("~SnakeLairProbe");
            lairGo.transform.position = Vector3.zero;
            trash.Add(lairGo);
            var site = lairGo.AddComponent<LairSite>();
            site.speciesName = "Змея";
            site.capacity = 1;
            site.spawnEnabled = true;
            site.respawnSeconds = 0.01f;
            site.spawnRadius = 2f;
            b.Registry.Register(site);

            b.Spawner.Tick(1f);

            var born = b.Spawner.lastSpawn;
            Assert.IsNotNull(born, "спавнер родил из очереди логова");
            spawned.Add(born.gameObject);

            Assert.IsNotNull(born.Chassis, "у рождённого есть шасси");
            Assert.AreEqual("Змея", born.Chassis.speciesName, "родился вид логова, а не сосед");

            float x = born.transform.position.x;
            float z = born.transform.position.z;
            Assert.AreEqual(b.SampleGround(x, z), born.transform.position.y, 0.2f,
                $"зверь на земле, а не на Y логова: pos {born.transform.position}, " +
                $"земля {b.SampleGround(x, z)}");

            yield return null; // дать Destroy в TearDown сработать в следующем кадре
        }

        /// <summary>
        /// C.1: рождённый логовом зверь — жилец МИРА, а не сцены. `Clear` сносит мир вместе
        /// с сайтами, и зверь обязан уйти вместе с ним: иначе он висит с `home` на снесённом
        /// сайте — некуда возвращаться, а кап и крыша логова считаются по мёртвому адресу.
        /// <para>
        /// Чужие логова мира сняты с реестра сразу после Build: у нас ОДИН Tick, а autoTick уже
        /// включён — иначе рожали бы настоящие устья и `lastSpawn` зависел бы от сида.
        /// </para>
        /// <para>
        /// `born == null` сравнивается оператором Unity (объект снесён), а не ссылкой: после
        /// Destroy не-нулевая ссылка ведёт себя как null. Поэтому ниже есть и поиск по сцене.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Clear_DestroysSpawnedFromLair()
        {
            var b = SmallWorld();
            yield return null;

            // Пул ДО Build: ForestBuilder передаёт его спавнеру при сборке мира.
            b.speciesPool.Add(MakeSpecies("Змея"));

            b.Build();
            Assert.IsNotNull(b.Spawner, "спавнер мира собран");
            var registry = b.Registry;
            Assert.IsNotNull(registry, "реестр мира заведён");
            registry.Clear(); // синхронно, без кадра: настоящие устья рожать не должны

            var lairGo = new GameObject("~SnakeLairProbe");
            lairGo.transform.position = Vector3.zero;
            trash.Add(lairGo);
            var site = lairGo.AddComponent<LairSite>();
            site.speciesName = "Змея";
            site.capacity = 1;
            site.spawnEnabled = true;
            site.respawnSeconds = 0.01f;
            site.spawnRadius = 2f;
            registry.Register(site);

            b.Spawner.Tick(1f); // DrainOnce синхронный: после Tick зверь уже в сцене

            var born = b.Spawner.lastSpawn;
            Assert.IsNotNull(born, "спавнер родил из очереди логова");
            // Страховка на случай красного: тест обязан оставить после себя пустую сцену.
            spawned.Add(born.gameObject);
            Assert.AreSame(site, born.home,
                "новорождённый держит home на сайте — оттого снос мира и оставляет сироту");
            Assert.AreEqual(1, b.Spawner.SpawnedCount,
                "спавнер помнит СВОЕГО зверя: без списка уборщику некого снимать");

            b.Clear();
            yield return null; // Object.Destroy отложен до конца кадра — читаем кадром позже

            Assert.IsTrue(born == null,
                "Clear снёс рождённого вместе с миром: иначе змея живёт с home на снесённом сайте");
            Assert.IsTrue(b.Spawner.lastSpawn == null,
                "спавнер не держит ссылку на снесённого (null по оператору Unity — уничтожен или обнулён)");
            Assert.AreEqual(0, b.Spawner.SpawnedCount,
                "список рождённых пуст: иначе пересборка снова и снова снимала бы мёртвые записи");
            Assert.AreEqual(0, b.Lairs.Count, "Clear чистит список логов");
            Assert.AreEqual(0, registry.Count, "Clear чистит реестр");
            Assert.AreEqual(0, Object.FindObjectsByType<CreatureBody>().Length,
                "в сцене не осталось тел от логовищ: проверка поиском, а не по ссылке");
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
