using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ОДИН пол павильона (S3 §2.1). Здесь записан порядок слоёв и больше нигде:
/// <code>
/// Hydro0  = H0 + hydro                       (озеро/русло уже вырезаны)
/// Shape0  = Hydro0 + terrace + canyon + ravines + landform + hill   (= PreErosion)
/// ShapeGround = Shape0 + [erosion]           (слот S4; сейчас тождество)
/// SampleGround = pad/ramp/crevice(ShapeGround)  — площадка ПОСЛЕДНЯЯ
/// </code>
/// <para>
/// Две ветки:
/// <list type="bullet">
/// <item><b>v1</b> (<c>worldVersion &lt; 2</c>) — исторический порядок БИТ-В-БИТ:
/// <c>SampleGround = landform(pad(hydro→terrace→canyon→ravines→hill))</c>, то есть
/// P5-lite применяется ПОСЛЕ площадки. Так A/C/B0/Pavilion v1 не меняют высот.</item>
/// <item><b>v2</b> — целевой порядок S3: <c>landform → hill → ShapeGround → pad</c>.
/// Станция ищется на <see cref="Hydro0"/>, <c>platformY</c> читается из
/// <see cref="ShapeGround"/> уже найденного пятна, а площадка последним blend'ом
/// ставит плиту на него. Цикла «станция ↔ форма» нет.</item>
/// </list>
/// </para>
/// <para>
/// <c>BaseGround</c>/<c>TerrainGround</c> больше не отдельные правды: это внутренние
/// шаги этого класса, наружу выходят только <see cref="Hydro0"/>, <see cref="ShapeGround"/>
/// и <see cref="SampleGround"/> (и <see cref="Shape0"/> для входа эрозии S4).
/// </para>
/// </summary>
public sealed class DomeTerrain
{
    /// <summary>С какой версии мира действует целевой порядок S3.</summary>
    public const int TargetOrderVersion = 2;

    public DomeGenConfigSO cfg;
    public DomeHydro.State hydro;
    public DomeTerrace.State terrace;
    public DomeCanyon.State canyon;
    public DomeRavines.Ravine[] ravines;
    public DomeRelief.State relief;
    /// <summary>S2: семь форм композиции (v2). v1 остаётся на <see cref="relief"/>.</summary>
    public DomeComposition.State composition;
    public DomeHill.State hill;
    public int worldVersion = 1;

    // --- площадка/рампа/расщелина: последний Blend -------------------------
    public bool padEnabled;
    /// <summary>Прямоугольная площадка комплекса (false — круг старого лабиринта A/C).</summary>
    public bool padRect;
    public Vector2 labCenter;
    public float padYaw;
    public float platformY;
    public LabEntrance entrance;
    public bool northCrevice;

    /// <summary>
    /// S2 §1.1 fallback: локальная насыпь/выемка коридора, когда валидного места нет.
    /// Это НЕ второй пол: полоса лежит в ShapeGround, её видят чанки/NavMesh/флора.
    /// </summary>
    public struct CorridorPad
    {
        public Vector2 a;
        public Vector2 b;
        public float halfWidth;
        public float blend;
        public float y;
    }
    public List<CorridorPad> corridorPads = new List<CorridorPad>();

    public void AddCorridorPad(Vector2 a, Vector2 b, float width, float y, float blend)
    {
        corridorPads.Add(new CorridorPad
        {
            a = a, b = b, halfWidth = Mathf.Max(1f, width * 0.5f),
            blend = Mathf.Max(0f, blend), y = y,
        });
    }

    /// <summary>v2: целевой порядок S3. v1: исторический (площадка до landform).</summary>
    public bool TargetOrder => worldVersion >= TargetOrderVersion;

    public bool Valid => cfg != null && hydro != null;

    /// <summary><c>Hydro0 = H0 + hydro</c> — поле, на котором ищется станция.</summary>
    public float Hydro0(float x, float z) => Valid ? DomeHydro.SampleHydro(cfg, hydro, x, z) : 0f;

    /// <summary>Диагностика: это же поле; отдельного «сырого» пола нет.</summary>
    public float RawGround(float x, float z) => Hydro0(x, z);

    /// <summary>
    /// <c>Shape0 = PreErosion</c>: форма БЕЗ площадки/рампы/щели. Вход эрозии S4 —
    /// ровно эта точка, поэтому площадка и POI в неё не попадают.
    /// </summary>
    public float Shape0(float x, float z)
    {
        if (!Valid) return 0f;
        float h = Layers(x, z);
        if (TargetOrder)
        {
            // Целевой порядок: широкое (landform S2) → местное (hill).
            // v2 читает DomeComposition; DomeRelief остаётся исторической веткой v1
            // и запасным путём, если композиция не строилась.
            h = composition != null
                ? DomeComposition.Apply(composition, h, x, z)
                : DomeRelief.Apply(relief, h, x, z, worldVersion);
            h = DomeHill.Apply(h, hill, x, z);
        }
        else
        {
            // v1: холм до landform (landform приходит после площадки, см. SampleGround).
            h = DomeHill.Apply(h, hill, x, z);
        }
        return h;
    }

    /// <summary>
    /// Единственный пол формы: <c>Shape0</c> + эрозия. S4 вставляет дельту СЮДА (слот
    /// <c>[erosion]</c>), до площадки и до приёмки зон/связей/POI. Поэтому зоны
    /// считаются только на <c>ShapeGround</c> и не могут «переехать» после эрозии:
    /// маски, которые эрозия обязана уважать, — <see cref="DomeMasks.ErosionKeeps"/>.
    /// </summary>
    public float ShapeGround(float x, float z)
    {
        float h = Shape0(x, z);
        if (!TargetOrder || corridorPads.Count == 0) return h;
        for (int i = 0; i < corridorPads.Count; i++)
        {
            var pad = corridorPads[i];
            float d = DomeFootprint.DistanceToSegment(new Vector2(x, z), pad.a, pad.b);
            if (d <= pad.halfWidth) return pad.y;
            float outer = pad.halfWidth + pad.blend;
            if (d < outer && pad.blend > 1e-3f)
            {
                float t = (d - pad.halfWidth) / pad.blend;
                t = t * t * (3f - 2f * t);
                h = Mathf.Lerp(pad.y, h, t);
            }
        }
        return h;
    }

    /// <summary>Финальный пол: площадка/рампа/щель — ПОСЛЕДНИЙ Blend.</summary>
    public float SampleGround(float x, float z)
    {
        if (!Valid) return 0f;
        if (!TargetOrder)
        {
            // v1 бит-в-бит: сначала площадка, потом landform (исторический порядок).
            float h = PadBlend(Shape0(x, z), x, z);
            return DomeRelief.Apply(relief, h, x, z, worldVersion);
        }
        return PadBlend(ShapeGround(x, z), x, z);
    }

    /// <summary>
    /// Внутренний шаг v1: площадка поверх поля БЕЗ landform (историческое поведение,
    /// в котором P5-lite ложится после площадки). Наружу как «пол» не выходит —
    /// потребители читают <see cref="SampleGround"/>.
    /// </summary>
    public float BaseGround(float x, float z) => Valid ? PadBlend(Shape0(x, z), x, z) : 0f;

    /// <summary>Hydro0 → terrace → canyon → ravines (без landform и без холма).</summary>
    float Layers(float x, float z)
    {
        float h = Hydro0(x, z);
        h = DomeTerrace.Apply(h, terrace, x, z);
        h = DomeCanyon.Apply(h, canyon, x, z, hydro.lakeLevel);
        h = DomeRavines.Apply(h, ravines, x, z, hydro.lakeLevel);
        return h;
    }

    /// <summary>
    /// Площадка, спуск и северная расщелина. Порядок внутри один и тот же для обеих
    /// веток — меняется только то, ПО КАКОМУ полю он бьёт.
    /// </summary>
    float PadBlend(float field, float x, float z)
    {
        if (!padEnabled) return field;

        // Подземный вход: вырезаем пологую траншею-рампу от двери к лесной площадке.
        if (padRect && (entrance == LabEntrance.Underground || entrance == LabEntrance.Buried))
        {
            if (TryRamp(x, z, out float rampY)) return rampY;
        }

        // Северная расщелина — такой же вырез в поле, как южный спуск.
        if (padRect && northCrevice)
        {
            if (TryCrevice(x, z, out float creviceY)) return creviceY;
        }

        float u = PadWeight(x, z);
        if (u <= 0f) return platformY;
        float t = u * u * (3f - 2f * u);
        return Mathf.Lerp(platformY, field, t);
    }

    bool TryRamp(float x, float z, out float y)
    {
        y = 0f;
        float dx = x - labCenter.x, dz = z - labCenter.y;
        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s;
        float lz = dx * s + dz * c;
        float zDoor = -DomeLabComplex.HalfDepth;
        float zOuter = zDoor - DomeLabComplex.RampLength;
        float halfRamp = DomeLabComplex.RampWidth * 0.5f;
        const float sideBlend = 5f;
        if (Mathf.Abs(lx) > halfRamp + sideBlend || lz > zDoor || lz < zOuter) return false;

        float rampT = Mathf.Clamp01((zDoor - lz) / DomeLabComplex.RampLength);
        float rampY = Mathf.Lerp(platformY, platformY +
            DomeLabComplex.DepthFor(entrance), rampT);
        float sideW = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(halfRamp, halfRamp + sideBlend, Mathf.Abs(lx)));
        y = Mathf.Lerp(platformY, rampY, sideW);
        return true;
    }

    bool TryCrevice(float x, float z, out float y)
    {
        y = 0f;
        float dx = x - labCenter.x, dz = z - labCenter.y;
        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s;
        float lz = dx * s + dz * c;
        float z0 = DomeLabComplex.HalfDepth;
        float z1 = z0 + DomeLabComplex.CreviceLength;
        float half = DomeLabComplex.CreviceWidth * 0.5f;
        const float sideBlend = 4f;
        if (Mathf.Abs(lx) > half + sideBlend || lz < z0 || lz > z1) return false;

        float northT = Mathf.Clamp01((lz - z0) / DomeLabComplex.CreviceLength);
        float floorY = platformY + DomeLabComplex.Plinth +
            (DomeLabComplex.DepthFor(LabEntrance.Buried) - DomeLabComplex.Plinth) * northT;
        float sideW = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(half, half + sideBlend, Mathf.Abs(lx)));
        y = Mathf.Lerp(platformY, floorY, sideW);
        return true;
    }

    /// <summary>
    /// 0 — внутри площадки (пол = platformY), 1 — поле формы. Двор A/C — круг R20/R25;
    /// у комплекса добавляется прямоугольник плиты с полем PadBlend (минимум веса).
    /// </summary>
    public float PadWeight(float x, float z)
    {
        float dx = x - labCenter.x, dz = z - labCenter.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float u = Mathf.InverseLerp(DomeWorld.LabFlatRadius, DomeWorld.LabRadius, d);
        if (!padRect) return u;

        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s; // поле → кадр плиты
        float lz = dx * s + dz * c;
        float q = Mathf.Max(Mathf.Abs(lx) - DomeLabComplex.PadHalfWidth,
            Mathf.Abs(lz) - DomeLabComplex.PadHalfDepth);
        float ur = Mathf.InverseLerp(0f, DomeLabComplex.PadBlend, q);
        return Mathf.Min(u, ur);
    }
}
