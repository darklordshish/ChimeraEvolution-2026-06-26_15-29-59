using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S5: профили мира ОДНИМ местом. Это не «второй источник чисел»: профиль —
/// ровно сериализованные поля сцен A/C/B0/Pavilion, вынесенные в код, чтобы
/// паритет (<c>ForestSceneParityTests</c>) мог сверить сцену с тем же набором
/// полей, который собирают ворота. Сцена остаётся источником правды: если она
/// изменится, паритет покраснеет, а не подгонится.
/// <para>
/// v2 — профиль ворот S5: Ø2000, <c>terrainQuads 48</c> (объявленный профиль
/// полос H0), <c>worldVersion 2</c>, композиция S2 (<c>buildRelief</c>), эрозия S4.
/// A/C/B0/Pavilion остаются v1 — их отпечатки заморожены <c>ForestBaselineTests</c>.
/// </para>
/// </summary>
public static class ForestProfiles
{
    /// <summary>A: `ForestSandbox` — Ø2000, сид 1337, quads 16, без разметки.</summary>
    public static ForestBuilder A(GameObject host) => Configure(host, new Profile
    {
        seed = 1337, diameter = 2000f, chunk = 250f, quads = 16, treeCount = 0,
        patchy = false, ash = true, spawnAtEntry = false,
        buildOnAwake = true,
    });

    /// <summary>C: форма мира та же, что A; отличается только пулом спавнера.</summary>
    public static ForestBuilder C(GameObject host) => A(host);

    /// <summary>B0: `ForestFragment`, сид 9 (решение 01.11), Ø400, quads 24.</summary>
    public static ForestBuilder B0(GameObject host) => Configure(host, new Profile
    {
        seed = 9, diameter = 400f, chunk = 100f, quads = 24, treeCount = 0,
        patchy = false, ash = true, spawnAtEntry = true, ravines = true,
        labComplex = true, labPlan = true, climate = true, autoWeather = true,
        style = LabStyle.InSlope, entrance = LabEntrance.Underground,
        buildOnAwake = true,
    });

    /// <summary>Pavilion v1: B-lite — Ø2000, сид 1337, quads 48, 1500 деревьев. С 13.11 — первая зона вечного цикла:
    /// каньон выключен (заморожен, ассеты в теге `archive/les-zamorozka-2026-11-12`).</summary>
    public static ForestBuilder PavilionV1(GameObject host) => Configure(host, new Profile
    {
        seed = 1337, diameter = 2000f, chunk = 250f, quads = 48, treeCount = 1500,
        patchy = true, ash = true, spawnAtEntry = true,
            terrace = true, canyon = false, stationLoop = true, stationHill = true, animalLairs = true,
            labComplex = true, labPlan = true, climate = true,
            style = LabStyle.Bastion, entrance = LabEntrance.Rock,
        climateTimeMinutes = 10.5f, climateTimeScale = 0f,
        buildOnAwake = true,
    });

    /// <summary>
    /// Профиль ворот S5: тот же Pavilion, но v2 (полосы H0), композиция S2 и эрозия S4.
    /// Эрозию можно выключить для быстрых прогонов — это НЕ приёмка, а отладочный режим.
    /// </summary>
    public static ForestBuilder PavilionV2(GameObject host, long seed = 1337,
        bool erosion = true)
    {
        var b = Configure(host, new Profile
        {
            seed = seed, diameter = 2000f, chunk = 250f, quads = 48, treeCount = 1500,
            patchy = true, ash = true, spawnAtEntry = true,
            terrace = true, canyon = true, stationLoop = true, stationHill = false,
            labComplex = true, labPlan = true, climate = true,
            style = LabStyle.Auto, entrance = LabEntrance.Auto,
            climateTimeMinutes = 10.5f, climateTimeScale = 0f,
            buildOnAwake = false,
        });
        b.worldVersion = 2;
        b.treeVersion = 2;
        b.buildAqueduct = true;
        b.buildRelief = true;
        b.buildThermalErosion = erosion;
        b.buildHydraulicErosion = erosion;
        b.erosionVersion = 3;
        b.erosionResolution = 128;
        b.undergrowthScale = 0.01f;
        b.forestThreshold = 0.20f;
        b.treeCount = 2000;
        b.treeDensityTarget = 3120f;
        b.treeMinCount = 1500;
        b.erosionDrops = 96000;
        b.erosionSteps = 40;
        return b;
    }

    sealed class Profile
    {
        public long seed;
        public float diameter;
        public float chunk;
        public int quads;
        public int treeCount;
        public bool patchy;
        public bool ash;
        public bool spawnAtEntry;
        public bool ravines;
        public bool labComplex;
        public bool labPlan;
        public bool climate;
        public bool autoWeather;
        public bool terrace;
        public bool canyon;
        public bool animalLairs; // логова волка, лося, ежа — первая зона (13.11)
        public bool stationLoop;
        public bool stationHill;
        public LabStyle style;
        public LabEntrance entrance;
        public float climateTimeMinutes = 10f;
        public float climateTimeScale = 1f / 60f;
        public bool buildOnAwake;
    }

    static ForestBuilder Configure(GameObject host, Profile p)
    {
        host.SetActive(false);
        var b = host.AddComponent<ForestBuilder>();
        b.buildOnAwake = p.buildOnAwake;
        b.randomSeed = false;
        b.seed = p.seed;
        b.mapDiameter = p.diameter;
        b.chunkSize = p.chunk;
        b.terrainQuads = p.quads;
        b.treeCount = p.treeCount;
        b.patchyFlora = p.patchy;
        b.buildAsh = p.ash;
        b.spawnAtEntry = p.spawnAtEntry;
        b.buildRavines = p.ravines;
        b.buildTerrace = p.terrace;
        b.buildCanyon = p.canyon;
        b.buildAnimalLairs = p.animalLairs;
        b.buildStationLoop = p.stationLoop;
        b.buildStationHill = p.stationHill;
        b.buildLabComplex = p.labComplex;
        b.buildLabPlan = p.labPlan;
        b.labPlanVersion = 1;
        b.buildClimate = p.climate;
        b.climateWeather = WeatherKind.Clear;
        b.climateTimeMinutes = p.climateTimeMinutes;
        b.climateTimeScale = p.climateTimeScale;
        b.climateAutoWeather = p.autoWeather;
        b.labStyle = p.style;
        b.labEntrance = p.entrance;
        b.minLabDistance = 150f;
        // Поля, добавленные после сохранения сцены, читаются из инициализатора:
        // у erosionVersion это 1. Паритет это подтверждает на трёх сценах.
        b.erosionVersion = 1;
        b.treeVersion = 0;
        b.erosionCacheDir = "";
        b.erosionResolution = 0;
        b.erosionDrops = 0;
        b.erosionSteps = 0;
        b.speciesPool = new List<SpeciesSO>();
        return b;
    }
}
