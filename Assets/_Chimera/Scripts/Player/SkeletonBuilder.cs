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

    // МЯСО НАРАЩИВАЕТ `BoneMesher` — трубой со скиннингом. Прежде здесь жил `Grow`, лепивший вдоль кости
    // цепочку сфер: он был честным промежуточным шагом (проверял ПРАВИЛА обрастания, не трогая покраску),
    // но стоил 315 рендереров на волка и не давал анимации. Правила проверены, шары сняты.
}
