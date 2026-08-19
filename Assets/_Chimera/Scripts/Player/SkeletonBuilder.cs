using System.Collections.Generic;
using UnityEngine;

/// <summary>СБОРКА СКЕЛЕТА: считает положение костей и наращивает на них мясо (спека 2026-08-18).
///
/// ГЛАВНЫЙ ИНВАРИАНТ — СУСТАВ ОБЩИЙ: начало дочерней кости ЕСТЬ точка на родительской, а радиус там
/// один. Отсюда по построению невозможны три дефекта, которые в парадигме мест ловились только глазом
/// и стоили 15.08 четырёх переделок волка:
///   • деталь не висит в воздухе — её начало это конец предыдущей;
///   • нет уступа на стыке — радиусы совпадают по определению;
///   • ничего не «отваливается торпедой» — сустав принадлежит обеим костям сразу.
/// Выигрыш здесь не «легче исправить», а «нельзя сломать».</summary>
public static class SkeletonBuilder
{
    /// <summary>Где кость начинается и куда смотрит. `depth` страхует от цикла в данных — билдер не
    /// должен зависать на кривых числах (тот же приём, что в графе мест).</summary>
    public static (Vector3 pos, Quaternion rot) Place(Bone b, Dictionary<string, Bone> byName,
                                                     Dictionary<string, (Vector3, Quaternion)> done, int depth = 0)
    {
        if (done.TryGetValue(b.name, out var cached)) return cached;

        var rot = Quaternion.Euler(b.dir);
        var pos = b.origin;   // у корня цепи — своя точка старта, у остальных её перезапишет сустав

        if (depth < 16 && !string.IsNullOrEmpty(b.parent) && byName.TryGetValue(b.parent, out var par) && par != b)
        {
            var (ppos, prot) = Place(par, byName, done, depth + 1);
            // ТОЧКА НА РОДИТЕЛЕ вдоль его оси (+Y), доля `attach`: 1 = конец кости, 0.5 = середина.
            // Здесь сустав и становится общим — ребёнок стартует ровно там, где кончается родитель,
            // и никакой отдельной «позиции» у него нет, чтобы с ней разъехаться
            pos = ppos + prot * (Vector3.up * (par.length * b.attach));
            rot = prot * rot;   // поворот НАСЛЕДУЕТСЯ: согнул плечо — поехала вся нога
        }

        done[b.name] = (pos, rot);
        return (pos, rot);
    }

    /// <summary>Конец кости — он же начало её детей.</summary>
    public static Vector3 Tip(Bone b, Vector3 pos, Quaternion rot) => pos + rot * (Vector3.up * b.length);

    /// <summary>Радиус кости в точке `t` вдоль длины (0 — начало, 1 — конец).</summary>
    public static float RadiusAt(Bone b, float t) => Mathf.Lerp(b.r0, b.r1, Mathf.Clamp01(t));

    /// <summary>МЯСО НА КОСТИ — веретено: цепочка объёмов от `r0` к `r1` вдоль оси.
    ///
    /// ПЛОТНОСТЬ РЕШАЕТ ГЛАДКОСТЬ — найдено 15.08 на ручной лепке геймдизайнера: в кубическом языке
    /// силуэт сглаживает не форма отдельной детали, а ЧИСЛО промежуточных объёмов. Два шара встык
    /// читаются уступом, пять — перетекают. Шаг берём от толщины: чем тоньше кость, тем чаще сегменты.
    ///
    /// СЕЧЕНИЕ НЕРАВНОМЕРНОЕ. `section` сплющивает объём по X: грудь волка анфас вдвое уже, чем глубока,
    /// и без этого корпус читается бочкой, сколько ни правь длины.</summary>
    public static void Grow(Transform parent, Bone b, Vector3 pos, Quaternion rot, float side,
                            List<GameObject> made)
    {
        float avg = Mathf.Max(0.001f, (b.r0 + b.r1) * 0.5f);
        int segs = Mathf.Clamp(Mathf.RoundToInt(b.length / avg), 2, 12);

        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float r = RadiusAt(b, t);
            var p = pos + rot * (Vector3.up * (b.length * t));
            var e = rot.eulerAngles;
            if (side < 0f) { p.x = -p.x; e.y = -e.y; e.z = -e.z; }   // зеркало пары, как у парных мест

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // ИМЯ ПО СЛОТУ, А НЕ ПО КОСТИ. Контракт имён частей знает словарь МЕСТ («голова», «Пасть»,
            // «уши»), и он же остаётся именем модуля скелета: кость «ростр» — это часть «голова», и
            // эмоц-тинт красит её, ничего не зная про кости. Не будь этого, пришлось бы перечислять в
            // `Telegraph` все кости черепа — то есть завести список исключений вместо иерархии
            go.name = string.IsNullOrEmpty(b.socket) ? b.name : b.socket;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.transform.localEulerAngles = e;
            go.transform.localScale = new Vector3(r * 2f * b.section, r * 2f, r * 2f);
            made.Add(go);
        }
    }
}
