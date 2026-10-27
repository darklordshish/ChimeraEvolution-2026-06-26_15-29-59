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
            ForestClimate.ResetStatic();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        ForestBuilder BuildLab(LabStyle style, bool complex = true, long seed = 4,
            LabEntrance entrance = LabEntrance.Rock, bool ash = false, bool ravines = false,
            bool plan = false, int planVersion = 1, bool climate = false,
            WeatherKind weather = WeatherKind.Clear, float timeMinutes = 10f)
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
            builder.buildLabPlan = plan;
            builder.labPlanVersion = planVersion;
            builder.buildClimate = climate;
            builder.climateWeather = weather;
            builder.climateTimeMinutes = timeMinutes;
            builder.climateTimeScale = 0f;
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
            Assert.AreEqual(b.SampleGround(b.Ash.pos.x, b.Ash.pos.y), b.Ash.groundY, 0.05f,
                "ясень стоит по тому же рельефу, что и мир");
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
        public IEnumerator MirrorPlan_SwapsSurgeryAndCells()
        {
            long mirrorSeed = 0;
            for (long s = 1; s <= 32 && mirrorSeed == 0; s++)
            {
                var p = LabPlanGenerator.Generate(s, 1, LabEntrance.Rock, LabStyle.Bastion);
                if (p.template == "mirror") mirrorSeed = s;
            }
            Assert.AreNotEqual(0, mirrorSeed, "генератор должен уметь mirror в первых 32 сидах");

            var b = BuildLab(LabStyle.Bastion, seed: mirrorSeed, plan: true);
            yield return null;

            Assert.AreEqual("mirror", b.LabComplex.planTemplate, "план должен быть mirror");
            var root = b.LabComplex.root.transform;
            var table = Find(root, "SurgeryTableTop");
            var bar = Find(root, "CellBar");
            var gate = Find(root, "DamagedGate");
            Assert.IsNotNull(table, "стол хирургии собран");
            Assert.IsNotNull(bar, "решётка клеток собрана");
            Assert.Greater(table.localPosition.x, 0f, "mirror: хирургия справа");
            Assert.Less(bar.localPosition.x, 0f, "mirror: клетки слева");
            if (gate != null)
                Assert.Less(gate.localPosition.x, 0f, "mirror: повреждённая решётка на стороне клеток");
        }

        [UnityTest]
        public IEnumerator DeepWingsPlan_MovesFurnitureToWingEdges()
        {
            long deepSeed = 0;
            for (long s = 1; s <= 64 && deepSeed == 0; s++)
            {
                var p = LabPlanGenerator.Generate(s, 1, LabEntrance.Rock, LabStyle.Bastion);
                if (p.template == "deep-wings") deepSeed = s;
            }
            Assert.AreNotEqual(0, deepSeed, "генератор должен уметь deep-wings в первых 64 сидах");

            var b = BuildLab(LabStyle.Bastion, seed: deepSeed, plan: true);
            yield return null;

            Assert.AreEqual("deep-wings", b.LabComplex.planTemplate, "план должен быть deep-wings");
            var root = b.LabComplex.root.transform;
            var cabinet = Find(root, "SurgeryCabinet");
            var cellsLocker = Find(root, "CellsLocker");
            var table = Find(root, "SurgeryTableTop");
            Assert.IsNotNull(cabinet, "шкаф хирургии собран");
            Assert.IsNotNull(cellsLocker, "шкаф клеток собран");
            Assert.Greater(cabinet.localPosition.z, 7f, "deep-wings: шкаф хирургии у дальнего края");
            Assert.Greater(cellsLocker.localPosition.z, 7f, "deep-wings: шкаф клеток у дальнего края");
            Assert.Less(Mathf.Abs(table.localPosition.z), 2f, "стол хирургии остался в центре");
        }

        [UnityTest]
        public IEnumerator Damage_DifferentTemplates_Differ()
        {
            long fixedSeed = FindPlanSeed("fixed");
            long deepSeed = FindPlanSeed("deep-wings");
            Assert.AreNotEqual(0, fixedSeed, "нужен сид с fixed");
            Assert.AreNotEqual(0, deepSeed, "нужен сид с deep-wings");

            var a = BuildLab(LabStyle.Bastion, seed: fixedSeed, plan: true);
            yield return null;
            string fixedSignature = a.LabComplex.damageSignature;
            a.Clear();
            Object.Destroy(host);
            host = null;
            builder = null;

            var b = BuildLab(LabStyle.Bastion, seed: deepSeed, plan: true);
            yield return null;
            string deepSignature = b.LabComplex.damageSignature;

            StringAssert.Contains("plan=fixed", fixedSignature);
            StringAssert.Contains("plan=deep-wings", deepSignature);
            Assert.AreNotEqual(fixedSignature, deepSignature, "шаблоны дают разные события повреждений");
        }

        [UnityTest]
        public IEnumerator Damage_DoesNotBlockDoorRoute()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, plan: true);
            yield return null;

            Assert.IsTrue(NavMesh.SamplePosition(b.LabDoor, out var doorHit, 20f, NavMesh.AllAreas),
                "дверь на NavMesh после повреждений");
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(b.LabCenter.x, b.LabDoor.y, b.LabCenter.y), out var hallHit, 20f, NavMesh.AllAreas),
                "зал на NavMesh после повреждений");
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(doorHit.position, hallHit.position, NavMesh.AllAreas, path),
                "путь дверь → зал существует");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                "повреждения не рвут обязательный маршрут");
        }

        [UnityTest]
        public IEnumerator Clear_RemovesGeneratedPlanAndDamage()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, plan: true);
            yield return null;
            Assert.IsNotNull(b.LabPlan, "план есть до Clear");
            Assert.IsFalse(string.IsNullOrEmpty(b.LabComplex.damageSignature), "повреждения есть до Clear");

            b.Clear();
            Assert.IsNull(b.LabPlan, "Clear обнуляет план");
            Assert.IsNull(b.LabComplex, "Clear снимает комплекс");
        }

        [UnityTest]
        public IEnumerator Climate_Rain_BuildsRainAndWet()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, climate: true,
                weather: WeatherKind.Rain, timeMinutes: 10f);
            yield return null;

            var climate = b.GetComponent<ForestClimate>();
            Assert.IsNotNull(climate, "климат повешен на builder");
            Assert.AreEqual(WeatherKind.Rain, ForestClimate.CurrentState.kind, "дождь — текущая погода");
            Assert.IsNotNull(GameObject.Find("~DomeRain"), "дождь собран");
            Assert.AreEqual(0f, ForestClimate.CurrentTimeMinutes - 10f, 0.5f, "время не убежало");
        }

        [UnityTest]
        public IEnumerator Climate_Night_IsNight()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, climate: true,
                weather: WeatherKind.Clear, timeMinutes: 25f);
            yield return null;

            Assert.IsTrue(ForestClimate.IsNight(), "25 минут — ночь");
        }

        [UnityTest]
        public IEnumerator Climate_InsideLab_HidesRainAndFog()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, climate: true,
                weather: WeatherKind.Rain, timeMinutes: 10f);
            yield return null;
            var c = b.GetComponent<ForestClimate>();
            Assert.IsNotNull(c, "климат есть");
            var lab = b.LabComplex;
            Assert.IsNotNull(lab, "станция есть");

            var inside = lab.root.transform.TransformPoint(
                new Vector3(0f, DomeLabComplex.Plinth + 1f, 0f));
            c.ApplyVisuals(inside);
            Assert.IsFalse(RenderSettings.fog, "внутри станции нет внешнего тумана");
            Assert.IsFalse(c.rain.gameObject.activeSelf, "внутри станции нет дождя");

            var outside = lab.root.transform.TransformPoint(
                new Vector3(0f, 0f, -DomeLabComplex.HalfDepth - 20f));
            c.ApplyVisuals(outside);
            Assert.IsTrue(RenderSettings.fog, "снаружи туман возвращается");
            Assert.IsTrue(c.rain.gameObject.activeSelf, "снаружи дождь возвращается");
        }

        [UnityTest]
        public IEnumerator Climate_Clear_RemovesRain()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, climate: true,
                weather: WeatherKind.Rain);
            yield return null;
            Assert.IsNotNull(GameObject.Find("~DomeRain"), "дождь есть до Clear");

            b.Clear();
            yield return null;
            Assert.IsNull(GameObject.Find("~DomeRain"), "Clear снимает дождь");
        }

        [UnityTest]
        public IEnumerator Climate_AutoWeather_SurvivesMidnight()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9, climate: true,
                weather: WeatherKind.Clear, timeMinutes: 29.9f);
            yield return null;
            var c = b.GetComponent<ForestClimate>();
            Assert.IsNotNull(c, "климат есть");
            c.autoWeather = true;
            c.weatherChangeMinutes = 0.01f;
            c.timeScale = 100f;
            c.weatherTable = new WeatherTable { fogWeight = 1f };
            c.SetWeatherImmediate(WeatherKind.Clear);
            c.StartWeatherClock();

            yield return new WaitForSeconds(0.15f);

            Assert.Greater(c.WeatherStep, 0, "после заворота суток погода продолжает меняться");
            Assert.AreEqual(WeatherKind.Fog, ForestClimate.CurrentState.kind,
                "таблица только с туманом даёт туман");
        }

        [UnityTest]
        public IEnumerator LabRoof_AndSun_CastShadows()
        {
            var b = BuildLab(LabStyle.Bastion, seed: 9);
            yield return null;
            var roof = Find(b.LabComplex.root.transform, "LabRoofN");
            Assert.IsNotNull(roof, "крыша лаборатории есть");
            var roofRenderer = roof.GetComponent<MeshRenderer>();
            Assert.IsNotNull(roofRenderer, "у крыши есть рендерер");
            Assert.AreNotEqual(UnityEngine.Rendering.ShadowCastingMode.Off,
                roofRenderer.shadowCastingMode, "крыша кастит тень");
            var sun = GameObject.Find("DomeSun");
            Assert.IsNotNull(sun, "DomeSun есть");
            Assert.AreNotEqual(LightShadows.None, sun.GetComponent<Light>().shadows,
                "солнце кастит тень");
        }

        [UnityTest]
        public IEnumerator Clear_RestoresFogAndExternalDirectional()
        {
            var beforeFog = RenderSettings.fog;
            var beforeDensity = RenderSettings.fogDensity;
            var beforeColor = RenderSettings.fogColor;
            var ext = new GameObject("~ExternalSun");
            var light = ext.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            try
            {
                var b = BuildLab(LabStyle.Bastion, seed: 9, climate: true,
                    weather: WeatherKind.Rain, timeMinutes: 25f);
                yield return null;
                Assert.Less(light.intensity, 0.1f, "ночь приглушила внешний directional");

                b.Clear();
                yield return null;
                Assert.AreEqual(1.3f, light.intensity, 1e-3f, "Clear вернул интенсивность");
                Assert.AreEqual(beforeFog, RenderSettings.fog, "Clear вернул fog");
                Assert.AreEqual(beforeDensity, RenderSettings.fogDensity, 1e-5f, "Clear вернул fogDensity");
                Assert.AreEqual(beforeColor, RenderSettings.fogColor, "Clear вернул fogColor");
            }
            finally
            {
                Object.Destroy(ext);
            }
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

        static long FindPlanSeed(string template)
        {
            for (long s = 1; s <= 64; s++)
            {
                var p = LabPlanGenerator.Generate(s, 1, LabEntrance.Rock, LabStyle.Bastion);
                if (p.template == template) return s;
            }
            return 0;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
