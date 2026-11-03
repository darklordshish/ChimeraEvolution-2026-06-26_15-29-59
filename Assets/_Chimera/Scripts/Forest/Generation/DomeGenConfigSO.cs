using UnityEngine;

[CreateAssetMenu(menuName = "Chimera/Forest/DomeGenConfig", fileName = "DomeGenConfig")]
public class DomeGenConfigSO : ScriptableObject
{
    [Header("РЎРёРґ: РІРёРґРµРЅ РІ Inspector, РїРёС€РµС‚СЃСЏ РІ Р»РѕРі СЃ РёРЅСЃС‚СЂСѓРєС†РёРµР№ (РєР°Рє ArenaWalls)")]
    public long seed = 1337;
    public bool randomSeed = true;

    [Header("Р’РµСЂСЃРёСЏ (СЃРјРµРЅР° Р°Р»РіРѕСЂРёС‚РјР° = СЃРјРµРЅР° version)")]
    public int version = 1;

    // S1 (2026-11-01): Сѓ РџРћР›РЇ СЃРІРѕСЏ РІРµСЂСЃРёСЏ. 0/1 вЂ” v1 Р±РёС‚-РІ-Р±РёС‚ (A/C/B0),
    // 2 вЂ” С‚СЂРё РїРѕР»РѕСЃС‹ H0. `version` РѕСЃС‚Р°С‘С‚СЃСЏ СЃРѕР»СЊСЋ v1 Рё РЅРµ РїРµСЂРµРёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ:
    // РёРЅР°С‡Рµ СЃРјРµРЅР° РїРѕР»РѕСЃ СЃРґРІРёРЅСѓР»Р° Р±С‹ A/C. 0 РїСЂРёС…РѕРґРёС‚ Сѓ СЃС‚Р°СЂС‹С… Р°СЃСЃРµС‚РѕРІ/СЃС†РµРЅ
    // (РіРѕС‡Р° РЅРѕРІРѕРіРѕ СЃРµСЂРёР°Р»РёР·РѕРІР°РЅРЅРѕРіРѕ РїРѕР»СЏ) Рё С‡РёС‚Р°РµС‚СЃСЏ РєР°Рє v1.
    [Header("Р’РµСЂСЃРёСЏ РїРѕР»СЏ: 0 = РЅРµ РЅР°СЃС‚СЂРѕРµРЅРѕ = v1, 2 = РїРѕР»РѕСЃС‹ H0 (S1)")]
    public int worldVersion = 0;

    [Header("РџРѕР»РѕСЃС‹ H0 v2: РјРµС‚СЂС‹ РЅР° Г2000, РјР°СЃС€С‚Р°Р±РёСЂСѓСЋС‚СЃСЏ D/2000")]
    public float macroLambda = 1600f;
    [Tooltip("РџРѕР»РЅС‹Р№ РїРµСЂРµРїР°Рґ P, Рј (Р°РјРїР»РёС‚СѓРґР° A = P/2)")]
    public float macroAmplitude = 56f;
    [Range(0, 4)] public int macroOctaves = 2;
    [Range(0f, 0.12f)] public float macroWarp = 0.10f;

    public float mesoLambda = 450f;
    public float mesoAmplitude = 36f;
    [Range(0, 4)] public int mesoOctaves = 3;
    [Range(0f, 0.12f)] public float mesoWarp = 0.10f;
    [Tooltip("Ridged С‚РѕР»СЊРєРѕ РЅР° РјРµР·Рѕ Рё С‚РѕР»СЊРєРѕ РїРѕРґ РјР°СЃРєРѕР№ (S1 В§3.2)")]
    public bool mesoRidged = true;

    public float microLambda = 90f;
    public float microAmplitude = 6f;
    [Range(0, 4)] public int microOctaves = 2;
    [Range(0f, 0.12f)] public float microWarp = 0.08f;

    [Header("РљР»Р°СЃСЃС‹ СѓРєР»РѕРЅР° S1 В§11: С…РѕРґСЊР±Р° в‰¤ walk, СЃС‚РµРЅР° > maxSlopeDegrees")]
    public float walkMaxDegrees = DefaultWalkMaxDegrees;

    /// <summary>Р“СЂР°РЅРёС†Р° В«РїСЂРѕС…РѕРґРёРјРѕВ» РїРѕ S1 В§11.3; 0 РІ РїРѕР»Рµ = В«РЅРµ РЅР°СЃС‚СЂРѕРµРЅРѕВ».</summary>
    public const float DefaultWalkMaxDegrees = 22f;

    /// <summary>РџРµСЂРІР°СЏ (Рё РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РґРµР№СЃС‚РІСѓСЋС‰Р°СЏ) РІРµСЂСЃРёСЏ РїРѕР»СЏ.</summary>
    public const int FirstWorldVersion = 1;

    // Р•Р”РРќРЎРўР’Р•РќРќРђРЇ С‚РѕС‡РєР° РїСЂР°РІРёР»Р° В«0 в†’ v1В». Р—РѕРІСѓС‚ РѕР±Р° РІС…РѕРґР°: <see cref="WorldVersion"/>
    // РЅР° РєРѕРЅС„РёРіРµ Рё <see cref="ForestBuilder.WorldVersion"/> РІ СЃС†РµРЅРµ; СЃРІРѕРµРіРѕ `> 0 ? : 1`
    // Р±РѕР»СЊС€Рµ РЅРёРіРґРµ РЅРµС‚, РёРЅР°С‡Рµ РґРІР° РїРѕР»СЏ РґР°РґСѓС‚ РґРІРµ С‚СЂР°РєС‚РѕРІРєРё РѕРґРЅРѕРіРѕ РЅСѓР»СЏ.
    /// <summary>Р Р°Р·СЂРµС€РёС‚СЊ РІРµСЂСЃРёСЋ РїРѕР»СЏ: 0/РѕС‚СЂРёС†Р°С‚РµР»СЊРЅРѕРµ = В«РЅРµ РЅР°СЃС‚СЂРѕРµРЅРѕВ» = v1.</summary>
    public static int ResolveWorldVersion(int rawVersion)
    {
        return rawVersion > 0 ? rawVersion : FirstWorldVersion;
    }

    /// <summary>0 РІ РїРѕР»Рµ = В«РЅРµ РЅР°СЃС‚СЂРѕРµРЅРѕВ» = v1: РЅРѕРІРѕРµ РїРѕР»Рµ Сѓ СЃС‚Р°СЂРѕР№ СЃС†РµРЅС‹ РїСЂРёС…РѕРґРёС‚ РЅСѓР»С‘Рј.</summary>
    public int WorldVersion => ResolveWorldVersion(worldVersion);

    /// <summary>0 РІ РїРѕР»Рµ = В«РЅРµ РЅР°СЃС‚СЂРѕРµРЅРѕВ» в†’ 22 (РіРѕС‡Р° РЅРѕРІРѕРіРѕ СЃРµСЂРёР°Р»РёР·РѕРІР°РЅРЅРѕРіРѕ РїРѕР»СЏ).</summary>
    public float WalkMaxDegrees => walkMaxDegrees > 0f ? walkMaxDegrees : DefaultWalkMaxDegrees;

    /// <summary>Р“СЂР°РЅРёС†Р° СЃС‚РµРЅС‹ = СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёР№ РїСЂРµРґРµР» С„Р»РѕСЂС‹; РІС‚РѕСЂРѕРіРѕ Р»РёС‚РµСЂР°Р»Р° 35 РЅРµС‚.</summary>
    public float WallMinDegrees => maxSlopeDegrees > 0f ? maxSlopeDegrees : DefaultWallMinDegrees;

    [Header("Р Р°Р·РјРµСЂ: РѕРґРёРЅ РїР°СЂР°РјРµС‚СЂ РјР°СЃС€С‚Р°Р±РёСЂСѓРµС‚ РІСЃС‘ (С„РѕСЂРјСѓР»С‹ вЂ” DomeGenRules)")]
    public float mapDiameter = 2000f;
    public float chunkSize = 250f;

    [Header("Р РµР»СЊРµС„: FBM + ridged СЃ РјР°СЃРєРѕР№ РіРѕСЂ + domain warp")]
    public int octaves = 5;
    public int ridgedOctaves = 5;
    public int warpOctaves = 4;
    public float warpStrength = 6f;
    public float baseFrequency = 0.02f;
    public float amplitude = 6f;
    public float maxSlopeDegrees = DefaultWallMinDegrees;
    /// <summary>Р“СЂР°РЅРёС†Р° В«СЃС‚РµРЅР°В» РїРѕ S1 В§11.3 вЂ” РѕРЅР° Р¶Рµ РїСЂРµРґРµР» С„Р»РѕСЂС‹ (35).</summary>
    public const float DefaultWallMinDegrees = 35f;

    [Header("Эрозия S4 (только v2; v1 флаги false)")]
    public bool buildThermalErosion = false;
    public bool buildHydraulicErosion = false;
    [Tooltip("Поднимать при любой правке алгоритма/параметров: ключ кеша")]
    public int erosionVersion = 2;
    public int erosionResolution = 512;
    public float thermalTalusDegrees = 30f;
    public int thermalIterations = 10;
    [Range(0f, 1f)] public float thermalRate = 0.7f;
    public float thermalClamp = 0.6f;
    public int hydroDrops = 96000;
    public int hydroSteps = 40;
    [Range(0f, 1f)] public float hydroInertia = 0.05f;
    public float hydroCapacity = 4f;
    [Range(0f, 1f)] public float hydroErode = 0.3f;
    [Range(0f, 1f)] public float hydroDeposit = 0.3f;
    [Range(0f, 0.2f)] public float hydroEvaporate = 0.02f;
    public int hydroRadius = 3;
    public float hydroQuantum = 0.02f;
    public float hydroClamp = 1.5f;

    [Header("Р’РѕРґР° (s10b): РїРѕРєР° Р—РђР“Р›РЈРЁРљРђ С‡РёСЃР»РѕРј, РєР°Рє РІ s8")]
    public float waterLevel = -1.5f;

    // S3 (2026-11-01): P5-lite вЂ” РЎР’РћР™ СЃР»РѕР№ С„РѕСЂРјС‹, Р° РЅРµ С‡Р°СЃС‚СЊ B-lite. РџРѕРєР° СЃС†РµРЅР°
    // `ForestPavilion` РЅРµ РїРѕР»СѓС‡РёР»Р° С„Р»Р°Рі СЏРІРЅРѕ (СЌС‚Рѕ S1 С€Р°Рі 8), v1 РѕР±СЏР·Р°РЅ РѕСЃС‚Р°С‚СЊСЃСЏ
    // Р±РёС‚-РІ-Р±РёС‚: С‚Р°Рј РґРµР№СЃС‚РІСѓРµС‚ РёСЃС‚РѕСЂРёС‡РµСЃРєР°СЏ СЃРІСЏР·РєР° `patchyFlora в†’ СЂРµР»СЊРµС„`.
    [Header("Р РµР»СЊРµС„ С„РѕСЂРјС‹ (S3): РѕС‚РґРµР»СЊРЅС‹Р№ СЃР»РѕР№; 0 Сѓ СЃС‚Р°СЂРѕР№ СЃС†РµРЅС‹ = РёСЃС‚РѕСЂРёС‡РµСЃРєР°СЏ СЃРІСЏР·РєР° СЃ patchyFlora")]
    public bool buildRelief = false;

    /// <summary>
    /// Р’РєР»СЋС‡С‘РЅ Р»Рё СЃР»РѕР№ СЂРµР»СЊРµС„Р°. Р”Р»СЏ v1 вЂ” РёСЃС‚РѕСЂРёСЏ (patchyFlora), С‡С‚РѕР±С‹ РѕС‚РїРµС‡Р°С‚РѕРє
    /// Pavilion РЅРµ РїРѕРµС…Р°Р» РґРѕ S1 С€Р°РіР° 8; РґР»СЏ v2 вЂ” С‚РѕР»СЊРєРѕ СЏРІРЅС‹Р№ <see cref="buildRelief"/>.
    /// Р”РІСѓС… РёСЃС‚РѕС‡РЅРёРєРѕРІ РЅРµС‚: СЂРµС€РµРЅРёРµ РІ РѕРґРЅРѕРј СЃРІРѕР№СЃС‚РІРµ.
    /// </summary>
    public bool ReliefEnabled => buildRelief || (WorldVersion <= 1 && patchyFlora);

    [Header("РџР»РѕС‚РЅРѕСЃС‚Рё (s10d): С†РµР»Рё РІС‹РІРѕРґСЏС‚СЃСЏ С„РѕСЂРјСѓР»РѕР№, РЅРµ С‡РёСЃР»РѕРј")]
    public float treeDensityPerM2 = 0.03f;
    /// <summary>0 = СЃС‚Р°СЂС‹Рµ 700; РёРЅР°С‡Рµ С†РµР»РµРІРѕРµ С‡РёСЃР»Рѕ РґРµСЂРµРІСЊРµРІ РЅР° СЃС‚РµРЅРґ.</summary>
    public int treeCount = 0;
    /// <summary>РџСЏС‚РЅР° Р»РµСЃР°/РїРѕР»СЏРЅС‹ РїРѕ РЅРёР·РєРѕС‡Р°СЃС‚РѕС‚РЅРѕРјСѓ fBm; РІС‹РєР»СЋС‡РµРЅРѕ РґР»СЏ A/C.</summary>
    public bool patchyFlora = false;
    public float forestFraction = 0.55f;
    /// <summary>Явный порог fBm для пятен v2; −999 — историческая формула forestFraction.</summary>
    public float forestThreshold = -999f;
    /// <summary>Целевая локальная плотность пятен (дер./км²); 0 — фиксированный treeCount.</summary>
    public float treeDensityTarget = 0f;
    /// <summary>Пол числа деревьев v2 (0 — без пола): не даёт крошечному пятну упасть ниже 3000.</summary>
    public int treeMinCount = 0;
    /// <summary>Потолок числа деревьев v2 (0 — без потолка): держит верх 3500 и G8.</summary>
    public int treeMaxCount = 0;
    /// <summary>Множитель подлеска (S5 G8): 1 — исторические бюджеты; v2 берёт меньше,
    /// потому что 2.8k GameObject'ов подлеска стоили 2.1 с сборки.</summary>
    public float undergrowthScale = 1f;

    [Header("NavMesh: РўРћР›Р¬РљРћ С‚Р°Р№Р»Р°РјРё (РјРѕРЅРѕ-Р±РµР№Рє РЅР° Г2000 Р·Р°РїСЂРµС‰С‘РЅ)")]
    public float navVoxelSize = 0.35f;
    public float navTileSize = 112f;

    [Header("РљСѓРїРѕР»: РїРёРє РєРѕРЅСЃС‚Р°РЅС‚Р°, СЂР°РґРёСѓСЃС‹ вЂ” РѕС‚ РґРёР°РјРµС‚СЂР°")]
    public float ringPeakHeight = 100f;
    public float vaultRadiusFactor = 1.4f;

    [Header("Fast-preview (20вЂ“45СЃ РІРјРµСЃС‚Рѕ 70вЂ“140СЃ РїРѕР»РЅРѕРіРѕ Р±РµР№РєР°)")]
    public bool fastPreview = false;
    public int previewHeightmapSize = 2048;

    void OnValidate()
    {
        mapDiameter = Mathf.Clamp(mapDiameter, 100f, 4000f);
        chunkSize = Mathf.Clamp(chunkSize, 50f, 500f);
        octaves = Mathf.Clamp(octaves, 1, 8);
        ridgedOctaves = Mathf.Clamp(ridgedOctaves, 1, 8);
        warpOctaves = Mathf.Clamp(warpOctaves, 0, 8);
        warpStrength = Mathf.Max(warpStrength, 0f);
        baseFrequency = Mathf.Max(baseFrequency, 0.0001f);
        amplitude = Mathf.Max(amplitude, 0f);
        maxSlopeDegrees = Mathf.Clamp(maxSlopeDegrees, 1f, 60f);
        if (walkMaxDegrees > 0f) walkMaxDegrees = Mathf.Clamp(walkMaxDegrees, 1f, 60f);
        if (worldVersion < 0) worldVersion = 0;
        macroLambda = Mathf.Max(macroLambda, 1f);
        mesoLambda = Mathf.Max(mesoLambda, 1f);
        microLambda = Mathf.Max(microLambda, 1f);
        macroAmplitude = Mathf.Max(macroAmplitude, 0f);
        mesoAmplitude = Mathf.Max(mesoAmplitude, 0f);
        microAmplitude = Mathf.Max(microAmplitude, 0f);
        macroOctaves = Mathf.Clamp(macroOctaves, 0, 4);
        mesoOctaves = Mathf.Clamp(mesoOctaves, 0, 4);
        microOctaves = Mathf.Clamp(microOctaves, 0, 4);
        macroWarp = Mathf.Clamp(macroWarp, 0f, 0.12f);
        mesoWarp = Mathf.Clamp(mesoWarp, 0f, 0.12f);
        microWarp = Mathf.Clamp(microWarp, 0f, 0.12f);
        treeDensityPerM2 = Mathf.Max(treeDensityPerM2, 0f);
        forestFraction = Mathf.Clamp01(forestFraction);
        navVoxelSize = Mathf.Clamp(navVoxelSize, 0.05f, 1f);
        navTileSize = Mathf.Max(navTileSize, 16f);
        ringPeakHeight = Mathf.Max(ringPeakHeight, 0f);
        vaultRadiusFactor = Mathf.Max(vaultRadiusFactor, 1.01f);
    }
}
