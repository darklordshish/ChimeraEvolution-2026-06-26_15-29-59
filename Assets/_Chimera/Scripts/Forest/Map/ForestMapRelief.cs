using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// РЕЛЬЕФ КАРТЫ (спека `2026-11-18-forest-karta-kak-istochnik`, шаг 2): рецепт поля v2 и его высота. Рецепт — то, из чего
/// `DomeTerrain` считает пол: конфиг, гидро, терраса, формы композиции, холм, эрозия, площадка и пады коридоров. Карта
/// держит его сериализуемыми полями и считает высоту по СВОЕЙ копии террейна, собранной из этих полей (`Restore`), — не
/// по объекту мира. Поэтому 3D, читающее `Height`, читает карту, а карта из JSON даёт тот же пол.
///
/// Сетка эрозии — данные (в JSON), а в хеш карты входит её ключ `DomeErosion.Key`, а не сама сетка: float-разнобой машин
/// не должен менять хеш (ГеймБосс 18.11).
///
/// Не поддержано (v2-павильон их не строит): каньон, овраги, холм с дыркой-делегатом, v2 без композиции, v1. Встретив
/// их, <see cref="From"/> бросает <see cref="NotSupportedException"/> — слой не теряется молча.
/// </summary>
[Serializable]
public sealed class ForestMapRelief
{
    public int worldVersion;
    public string cfgJson = "";
    public DomeHydro.State hydro;
    public DomeTerrace.State terrace;
    public List<DomeForm> forms = new List<DomeForm>();
    public DomeHill.State hill;
    public DomeErosion.State erosion;
    public string erosionKey = "";
    public List<DomeTerrain.CorridorPad> corridorPads = new List<DomeTerrain.CorridorPad>();
    public bool padEnabled;
    public bool padRect;
    public Vector2 labCenter;
    public float padYaw;
    public float platformY;
    public LabEntrance entrance;
    public bool northCrevice;

    [NonSerialized] DomeTerrain terrain;
    [NonSerialized] DomeGenConfigSO cfg;

    /// <summary>Пол карты: площадка и пады поверх формы и эрозии (= `DomeTerrain.SampleGround`).</summary>
    public float Height(float x, float z) => Live.SampleGround(x, z);

    /// <summary>Пол формы без площадки (= `DomeTerrain.ShapeGround`): по нему выбираются зоны и связи.</summary>
    public float Shape(float x, float z) => Live.ShapeGround(x, z);

    DomeTerrain Live
    {
        get
        {
            if (terrain == null) Restore();
            return terrain;
        }
    }

    /// <summary>Снять рецепт с террейна мира. Состояния копируются (через JSON), а не берутся ссылкой.</summary>
    public static ForestMapRelief From(DomeTerrain t, long seed)
    {
        if (t == null) throw new ArgumentNullException(nameof(t));
        if (!t.TargetOrder) throw new NotSupportedException("рельеф карты — только v2 (решение геймдизайнера 18.11)");
        if (t.composition == null) throw new NotSupportedException("v2 без композиции форм — рецепт карты его не знает");
        if (t.canyon.valid && !t.useHandoffCanyon) throw new NotSupportedException("каньон заморожен — в рецепт карты не входит");
        if (t.ravines != null && t.ravines.Length > 0) throw new NotSupportedException("овраги заморожены — в рецепт карты не входят");
        if (t.hill.valid && t.hill.hole != null) throw new NotSupportedException("холм с дыркой-делегатом не сериализуется");

        var r = new ForestMapRelief
        {
            worldVersion = t.worldVersion,
            cfgJson = JsonUtility.ToJson(t.cfg),
            hydro = Clone(t.hydro),
            terrace = t.terrace,
            forms = new List<DomeForm>(t.composition.forms),
            hill = t.hill,
            erosion = t.erosion != null ? Clone(t.erosion) : null,
            erosionKey = t.erosion != null
                ? DomeErosion.Key(seed, t.worldVersion, t.erosion.erosionVersion, t.erosion.resolution)
                : "",
            corridorPads = new List<DomeTerrain.CorridorPad>(t.corridorPads),
            padEnabled = t.padEnabled,
            padRect = t.padRect,
            labCenter = t.labCenter,
            padYaw = t.padYaw,
            platformY = t.platformY,
            entrance = t.entrance,
            northCrevice = t.northCrevice,
        };
        r.Restore();
        return r;
    }

    /// <summary>Собрать копию террейна из полей рецепта (после `FromJson` — обязательно).</summary>
    public void Restore()
    {
        if (cfg == null) cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        JsonUtility.FromJsonOverwrite(cfgJson, cfg);
        if (hydro != null)
            hydro.riverIndex = new DomeHydro.RiverIndex(hydro.riverPts, hydro.riverHalfWidth * 3f);
        var comp = new DomeComposition.State();
        comp.forms.AddRange(forms);
        terrain = new DomeTerrain
        {
            cfg = cfg,
            hydro = hydro,
            terrace = terrace,
            canyon = default,
            useHandoffCanyon = false,
            ravines = new DomeRavines.Ravine[0],
            relief = null,
            composition = comp,
            erosion = erosion,
            hill = hill,
            worldVersion = worldVersion,
            padEnabled = padEnabled,
            padRect = padRect,
            labCenter = labCenter,
            padYaw = padYaw,
            platformY = platformY,
            entrance = entrance,
            northCrevice = northCrevice,
        };
        terrain.corridorPads.AddRange(corridorPads);
    }

    static T Clone<T>(T src) where T : class =>
        src == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(src));
}
