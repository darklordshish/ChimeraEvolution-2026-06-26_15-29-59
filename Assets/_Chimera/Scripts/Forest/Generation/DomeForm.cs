using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Знак формы: <c>Lift</c> (+1) поднимает, <c>Carve</c> (−1) режет.</summary>
public enum DomeFormSign
{
    Lift = 1,
    Carve = -1,
}

/// <summary>Носитель формы и профиль поперёк него.</summary>
public enum DomeFormKind
{
    /// <summary>Диск: холм/чаша/массив, <c>u = d/R</c>.</summary>
    Disc = 0,
    /// <summary>Капсула: гряда/овраг, <c>u = d_Σ/run</c> (не <c>max</c> — без излома на диагоналях).</summary>
    Capsule = 1,
    /// <summary>Плато/терраса: плоский верх <c>ρ = бровка/D</c>, кромка <c>S</c>.</summary>
    Plateau = 2,
    /// <summary>Каньон: плоское дно, борта только поперёк оси.</summary>
    Canyon = 3,
}

/// <summary>
/// S1 §3.1/§3.3: форма — эндоморфизм сечения <c>H ↦ H + s·H_i·φ_i(u)</c>,
/// тождественный вне носителя. Сумма форм коммутативна; порядок важен только
/// относительно масок и выравнивания (это S3).
/// <para>
/// <c>H/D = H/(D − бровка)</c> — единственное определение аспекта (S1 §3.5).
/// <c>max|∇H| = 1.5·H/run = 3H/(D−бровка)</c> — отдельный ворот, из аспекта
/// повторно не выводится как второй порог.
/// </para>
/// </summary>
[Serializable]
public struct DomeForm
{
    public string name;
    public DomeFormSign sign;
    public DomeFormKind kind;
    public Vector2 center;
    /// <summary>Ось капсулы/каньона (нормируется при расчёте).</summary>
    public Vector2 axis;
    /// <summary>Полудлина отрезка капсулы вдоль оси, м.</summary>
    public float halfLength;
    /// <summary>Полная ширина по подошве D, м.</summary>
    public float diameter;
    /// <summary>Полная ширина плоского верха/дна (бровка), м.</summary>
    public float crest;
    /// <summary>Превышение/глубина H ≥ 0, м.</summary>
    public float height;

    public float Run => Mathf.Max(0.01f, (diameter - crest) * 0.5f);

    /// <summary><c>H/D = H/(D − бровка)</c>.</summary>
    public float Aspect => diameter > crest
        ? height / (diameter - crest)
        : 0f;

    /// <summary><c>max|∇H| = 1.5·H/run</c> в градусах.</summary>
    public float MaxSlopeDegrees => Mathf.Atan(1.5f * height / Run) * Mathf.Rad2Deg;

    /// <summary>
    /// Максимальный шаг уклона на 1 м вдоль борта — порог C¹-проверки (S1 §3.6, P0-8).
    /// Профиль <c>S(1−u)</c> имеет <c>|h''| ≤ 6H/run²</c>, поэтому скачок уклона между
    /// соседними отсчётами равен этой кривизне, а НЕ фиксированным 2°. Фиксированное
    /// число краснело бы на законной малой куэсте (D≈120, H/D 0.18 → 2.03°), у которой
    /// излом не больше ожидаемого от радиуса кривизны <c>run²/(6H)</c>.
    /// </summary>
    public float SlopeStepDegrees(float step)
    {
        if (height <= 0f) return 0f;
        return Mathf.Atan(6f * height / (Run * Run) * Mathf.Max(0.01f, step)) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Радиус кривизны профиля <c>S(1−u)</c> на гребне: <c>run²/(6H)</c>.
    /// Для гряды <c>run = W/2</c>, то есть <c>0.347·W</c> при <c>H=0.12W</c> (S1 §3.6).
    /// </summary>
    public float CurvatureRadius => height > 1e-4f ? Run * Run / (6f * height) : float.PositiveInfinity;

    /// <summary>Безразмерное расстояние до носителя; ≥1 — вне формы.</summary>
    public float U(float x, float z)
    {
        float dx = x - center.x;
        float dz = z - center.y;
        switch (kind)
        {
            case DomeFormKind.Capsule:
            {
                Vector2 d = new Vector2(dx, dz);
                Vector2 a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector2.right;
                float along = Mathf.Clamp(Vector2.Dot(d, a), -halfLength, halfLength);
                Vector2 nearest = a * along;
                return (d - nearest).magnitude / Run;
            }
            case DomeFormKind.Canyon:
            {
                Vector2 a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector2.right;
                float across = Mathf.Abs(-a.y * dx + a.x * dz);
                return across / Run;
            }
            default:
                return Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(0.01f, diameter * 0.5f);
        }
    }

    /// <summary>Профиль φ(u); 0 вне подошвы. <c>S(t)=t²(3−2t)</c> — C¹.</summary>
    public float Profile(float u)
    {
        if (u >= 1f) return 0f;
        if (kind == DomeFormKind.Plateau)
        {
            float rho = diameter > 0f ? Mathf.Clamp01(crest / diameter) : 0f;
            if (u <= rho) return 1f;
            float t = Mathf.Clamp01((u - rho) / Mathf.Max(1e-4f, 1f - rho));
            return 1f - S(t);
        }
        return S(1f - Mathf.Max(0f, u));
    }

    /// <summary>Вклад формы в высоту (знак учтён).</summary>
    public float Apply(float h, float x, float z)
    {
        return h + (int)sign * height * Profile(U(x, z));
    }

    /// <summary><c>S(t) = t²(3−2t)</c>: S(0)=0, S(1)=1, S'(0)=S'(1)=0.</summary>
    public static float S(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

/// <summary>
/// Операторы S1 §3: <c>Lift</c> / <c>Carve</c> / <c>Blend</c>, подошва <c>smax</c>,
/// профили и две объявленные формы вертикали — <c>Massif</c> и <c>Crag</c>.
/// Размещения здесь нет: координаты и маски приходят снаружи (S2).
/// </summary>
public static class DomeForms
{
    /// <summary>k подошвы: сдвиг ≤ k·ln2 ≈ 0.14 м (S1 §3.3).</summary>
    public const float SmaxK = 0.2f;

    public static DomeForm Disc(Vector2 center, float diameter, float height,
        DomeFormSign sign = DomeFormSign.Lift, string name = "Hill")
    {
        return new DomeForm
        {
            name = name,
            sign = sign,
            kind = DomeFormKind.Disc,
            center = center,
            axis = Vector2.right,
            diameter = diameter,
            crest = 0f,
            height = Mathf.Abs(height),
        };
    }

    public static DomeForm Capsule(Vector2 center, Vector2 axis, float halfLength,
        float diameter, float height, DomeFormSign sign = DomeFormSign.Lift, string name = "Ridge")
    {
        return new DomeForm
        {
            name = name,
            sign = sign,
            kind = DomeFormKind.Capsule,
            center = center,
            axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector2.right,
            halfLength = Mathf.Max(0f, halfLength),
            diameter = diameter,
            crest = 0f,
            height = Mathf.Abs(height),
        };
    }

    /// <summary>Плато: <c>ρ = crest/diameter</c>, кромка C¹; правило <c>blend ≥ 4H</c> — на S2.</summary>
    public static DomeForm Plateau(Vector2 center, float diameter, float crest, float height,
        DomeFormSign sign = DomeFormSign.Lift, string name = "Plateau")
    {
        return new DomeForm
        {
            name = name,
            sign = sign,
            kind = DomeFormKind.Plateau,
            center = center,
            axis = Vector2.right,
            diameter = diameter,
            crest = Mathf.Clamp(crest, 0f, diameter),
            height = Mathf.Abs(height),
        };
    }

    public static DomeForm Canyon(Vector2 center, Vector2 axis, float halfLength,
        float width, float depth, string name = "Canyon")
    {
        return new DomeForm
        {
            name = name,
            sign = DomeFormSign.Carve,
            kind = DomeFormKind.Canyon,
            center = center,
            axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector2.right,
            halfLength = Mathf.Max(0f, halfLength),
            diameter = width,
            crest = 0f,
            height = Mathf.Abs(depth),
        };
    }

    /// <summary>
    /// Массив (S1 §11.5): <c>D = (0.16…0.21)·2rin</c>, <c>H = 0.11·D</c>, диск.
    /// Крупная форма, остающаяся проходимой: смелость в метрах, не в градусах.
    /// </summary>
    public static DomeForm Massif(Vector2 center, float innerDiameter, float spotFraction = 0.185f,
        float aspect = 0.11f, string name = "Massif")
    {
        float d = Mathf.Max(1f, innerDiameter) * Mathf.Clamp(spotFraction, 0.16f, 0.21f);
        return Disc(center, d, Mathf.Clamp(aspect, 0f, 0.11f) * d, DomeFormSign.Lift, name);
    }

    /// <summary>
    /// Куэста (S1 §11.5): <c>D = (0.065…0.095)·2rin</c>, <c>H/D 0.14…0.21</c>,
    /// капсула (не <c>max</c>), бровка 0. Даёт полосу класса C, но не стену.
    /// </summary>
    public static DomeForm Crag(Vector2 center, Vector2 axis, float innerDiameter,
        float spotFraction = 0.08f, float aspect = 0.18f, float lengthFactor = 1.6f,
        string name = "Crag")
    {
        float d = Mathf.Max(1f, innerDiameter) * Mathf.Clamp(spotFraction, 0.065f, 0.095f);
        return Capsule(center, axis, d * 0.5f * Mathf.Max(0.1f, lengthFactor), d,
            Mathf.Clamp(aspect, 0.14f, 0.21f) * d, DomeFormSign.Lift, name);
    }

    public static float Apply(DomeForm form, float h, float x, float z)
    {
        return form.Apply(h, x, z);
    }

    public static float Apply(IReadOnlyList<DomeForm> forms, float h, float x, float z)
    {
        if (forms == null) return h;
        for (int i = 0; i < forms.Count; i++)
            h = forms[i].Apply(h, x, z);
        return h;
    }

    /// <summary>Сумма форм под маской (маска 0..1 — оператор <c>Blend</c>).</summary>
    public static float Apply(IReadOnlyList<DomeForm> forms, float h, float x, float z,
        Func<float, float, float> mask)
    {
        if (forms == null) return h;
        if (mask == null) return Apply(forms, h, x, z);
        float before = h;
        h = Apply(forms, h, x, z);
        float m = Mathf.Clamp01(mask(x, z));
        return Mathf.Lerp(before, h, m);
    }

    /// <summary>Оператор <c>Blend</c>: C¹-переход между двумя значениями по маске.</summary>
    public static float Blend(float a, float b, float t)
    {
        return Mathf.Lerp(a, b, Mathf.Clamp01(t));
    }

    /// <summary>Полиномиальный smooth-max; жёсткий <c>max</c> — только объявленный шов у воды.</summary>
    public static float SmoothMax(float a, float b, float k)
    {
        float kk = Mathf.Max(1e-4f, k);
        float t = Mathf.Clamp01(0.5f + 0.5f * (a - b) / kk);
        return Mathf.Lerp(b, a, t) + kk * t * (1f - t);
    }

    /// <summary>Подошва рельефа: пол ниже воды/берега не опускаем (smax, k = 0.2 м).</summary>
    public static float Sole(float h, float floor)
    {
        return SmoothMax(h, floor, SmaxK);
    }

    /// <summary>Объём профиля <c>S(1−u)</c> над подошвой: <c>V = 0.3·A·H</c> (S1 §11.5).</summary>
    public static float DiscVolume(float diameter, float height)
    {
        float r = diameter * 0.5f;
        return 0.3f * Mathf.PI * r * r * Mathf.Abs(height);
    }

    /// <summary>
    /// Объём формы: диск — <c>0.3·πR²H</c>; капсула — <c>(0.3·πrun² + 0.5·4runL)·H</c>.
    /// У капсулы прямоугольная часть имеет другой вес профиля, чем круглая:
    /// смешивать их одной площадью нельзя (занижение ~1.45× на куэсте).
    /// </summary>
    public static float VolumeOf(DomeForm f)
    {
        float run = f.Run;
        if (f.kind == DomeFormKind.Capsule || f.kind == DomeFormKind.Canyon)
        {
            float round = 0.3f * Mathf.PI * run * run;
            float rect = 0.5f * 4f * run * f.halfLength;
            return (round + rect) * Mathf.Abs(f.height);
        }
        float radius = f.diameter * 0.5f;
        return 0.3f * Mathf.PI * radius * radius * Mathf.Abs(f.height);
    }

    /// <summary>
    /// Доля пятна куэсты, попадающая в класс C: <c>√(1 − tan22°/(3a))</c>.
    /// Считается, а не задаётся: от неё зависит ворот <c>A_C ≥ 0.8 %</c> (S1 §11.5).
    /// </summary>
    public static float ClimbAreaFraction(float aspect, float walkDegrees = 22f)
    {
        if (aspect <= 1e-4f) return 0f;
        float tan = Mathf.Tan(walkDegrees * Mathf.Deg2Rad);
        return Mathf.Sqrt(Mathf.Max(0f, 1f - tan / (3f * aspect)));
    }
}
