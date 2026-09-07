using System.Collections.Generic;
using UnityEngine;

/// <summary>Ф6 — СМЕШЕНИЕ ПО ИДЕНТИЧНОСТИ С ЛОКАЛЬНОСТЬЮ ИЗ ГРАФА (спека 14.08 + SPEC-kletka-tela.md §3).
/// Пропорция места-родителя меняется, форма слота — донорская целиком, калибр и топология — всегда носитель.
/// Локальность выводится: орган влияет на место-родитель своего сокета, и только на него.
/// Пасть→голова, Чутьё→голова, Руки/Ноги→хребет (исключён), поэтому бьёт ровно по голове.
/// И5 тождественность: на родном составе вес=1 на себе → таблица берётся без изменений.
/// И4 калибр от шасси: в данных донора метров нет.
/// </summary>
public partial class CreatureBody
{
    /// <summary>Смешанный сокет-план для MorphBuilder. null = чистое шасси (нет химерных влияний).
    /// Возвращает клон chassis.sockets с пересчитанными пропорциями (sizeRel, attach, attachOffset, baseEuler)
    /// для тех мест-родителей, на которые есть графты. Хребет никогда не смешивается (И3).</summary>
    public BodySocket[] GetBlendedPlan()
    {
        if (chassis == null || chassis.sockets == null || slots == null || slots.Length == 0) return null;

        // быстрый путь: нет звериных графтов → тождественность, план не нужен
        bool hasBeast = false;
        foreach (var sl in slots) if (sl.Installed) { hasBeast = true; break; }
        if (!hasBeast) return null;

        // словарь parentOfSlot по шасси (топология — всегда носитель, И1)
        var parentOf = new Dictionary<string, string>();
        foreach (var s in chassis.sockets)
            if (s != null && !string.IsNullOrEmpty(s.name))
                parentOf[s.name] = s.parent;

        // какие места-родители затронуты графтами? (локальность)
        var affectedParents = new HashSet<string>();
        var organParent = new Dictionary<string, string>(); // organ slot → parent socket
        foreach (var sl in slots)
        {
            if (sl.Empty) continue;
            var organ = sl.Worn;
            if (organ == null || string.IsNullOrEmpty(organ.slot)) continue;
            string slotName = organ.slot;
            parentOf.TryGetValue(slotName, out var par);
            // также пробуем найти сокет с таким именем у шасси (для graft мест like Хвост)
            // если parent пуст — место корневое, не смешиваем
            if (string.IsNullOrEmpty(par)) continue;
            if (par == BodySlots.Spine) continue; // И3 хребет неприкосновенен
            organParent[slotName + "|" + sl.GetHashCode()] = par; // уникально на слот-инстанс
            affectedParents.Add(par);
        }
        if (affectedParents.Count == 0) return null;

        // клонируем план — калибр и топология остаются шассийными
        var clone = new BodySocket[chassis.sockets.Length];
        for (int i = 0; i < chassis.sockets.Length; i++)
        {
            var src = chassis.sockets[i];
            if (src == null) { clone[i] = null; continue; }
            clone[i] = CloneSocket(src);
        }
        var byNameClone = new Dictionary<string, BodySocket>();
        foreach (var c in clone) if (c != null && !string.IsNullOrEmpty(c.name)) byNameClone[c.name] = c;

        // для каждого затронутого родителя считаем смесь
        foreach (var target in affectedParents)
        {
            if (target == BodySlots.Spine) continue;
            if (!byNameClone.TryGetValue(target, out var targetSocket)) continue;

            // собрать виды, влияющие на это место: те, у кого есть орган с parent==target
            var influencingSpecies = new HashSet<string>();
            foreach (var sl in slots)
            {
                if (sl.Empty) continue;
                var organ = sl.Worn;
                if (organ == null || string.IsNullOrEmpty(organ.slot)) continue;
                string slotName = organ.slot;
                parentOf.TryGetValue(slotName, out var par);
                if (par != target) continue;
                // вид органа
                string spName = sl.Pick != null ? sl.Pick.species : null;
                if (!string.IsNullOrEmpty(spName)) influencingSpecies.Add(spName);
                // шасси тоже может влиять своим родным органом — он уже в списке если Pick.native с chassis видом
            }
            if (influencingSpecies.Count == 0) continue;

            // веса = Identity(вид), нормированные на сумму влияющих (локальная выпуклость, И6)
            var speciesList = new List<SpeciesSO>();
            var weights = new List<float>();
            float sumW = 0f;
            foreach (var spName in influencingSpecies)
            {
                var sp = FindSpecies(spName);
                if (sp == null) continue;
                float w = Identity(sp);
                // w может быть 0 если вид не в составе, но орган уже установлен — всё равно учитываем?
                // Identity для такого вида должен быть >0, т.к. орган установлен
                if (w <= 0f) w = 1e-6f;
                speciesList.Add(sp);
                weights.Add(w);
                sumW += w;
            }
            if (speciesList.Count == 0 || sumW <= 0f) continue;
            // нормируем
            for (int i = 0; i < weights.Count; i++) weights[i] /= sumW;

            // если единственный влияющий — тождественность, смесь = его значение
            // иначе — взвешенное среднее пропорций этого места
            // берём пропорции из донорских сокетов с именем target
            Vector3 blendedSizeRel = Vector3.zero;
            float blendedAttach = 0f;
            Vector3 blendedOffset = Vector3.zero;
            Vector3 blendedEuler = Vector3.zero;
            bool any = false;

            // Для усреднения углов — линейно по компонентам (без гимбал, углы малы)
            for (int i = 0; i < speciesList.Count; i++)
            {
                var donor = speciesList[i];
                float w = weights[i];
                var donorSock = FindSocket(donor, target);
                if (donorSock == null) continue;
                any = true;
                blendedSizeRel += donorSock.sizeRel * w;
                blendedAttach += donorSock.attach * w;
                blendedOffset += donorSock.attachOffset * w;
                blendedEuler += donorSock.baseEuler * w;
            }
            if (!any) continue;

            // применяем только если результат отличается (иначе тождественность до микрона)
            // ВАЖНО: калибр (baseSize, localPos, linkLength...) НЕ трогаем — И4
            targetSocket.sizeRel = blendedSizeRel;
            // attach и attachOffset — тоже пропорция, но хранятся на ДЕТЯХ, а не на родителе.
            // Смесь родителя меняет sizeRel, а дети уже едут долями от него.
            // Для полноты спеки здесь смешиваем и baseEuler родителя (наклон шеи/головы — поза)
            targetSocket.baseEuler = blendedEuler;
            // attach/offset самого места не трогаем — они его положение ОТНОСИТЕЛЬНО родителя, а не его пропорция.
            // Но если target сам является ребёнком (голова висит на шее), его attach уже отражает пропорцию
            // родителя через SizeOf. Чтобы не двойно-считать, здесь не меняем attach цели.
        }

        // ── ФОРМА САМОГО СЛОТА — ДОНОРСКАЯ (SPEC-kletka §3): если на слот надет чужой орган,
        // его пропорция берётся у донора без смеси (а не смешивается). Пасть волка на человеке = волчья Пасть.
        foreach (var sl in slots)
        {
            if (sl.Empty) continue;
            var organ = sl.Worn;
            if (organ == null || string.IsNullOrEmpty(organ.slot)) continue;
            string slotName = organ.slot;
            if (slotName == BodySlots.Spine) continue; // хребет никогда
            if (!byNameClone.TryGetValue(slotName, out var targetSock)) continue;
            string spName = sl.Pick != null ? sl.Pick.species : null;
            if (string.IsNullOrEmpty(spName)) continue;
            // родной орган — тождественность, не заменяем
            if (spName == chassis.speciesName) continue;
            var donor = FindSpecies(spName);
            var donorSock = FindSocket(donor, slotName);
            if (donorSock == null) continue;
            // заменяем пропорцию слота донорской (И4 калибр не трогаем — baseSize/link остаются шассийными)
            targetSock.sizeRel = donorSock.sizeRel;
            // baseEuler слота тоже донорский если задан (поза пасти)
            if (donorSock.baseEuler != Vector3.zero) targetSock.baseEuler = donorSock.baseEuler;
        }

        // если план совпал с шасси до микрона — возвращаем null для тождественности (оптимизация)
        if (PlansEqual(clone, chassis.sockets)) return null;
        return clone;
    }

    static BodySocket CloneSocket(BodySocket s)
    {
        return new BodySocket
        {
            name = s.name,
            localPos = s.localPos,
            baseSize = s.baseSize,
            baseEuler = s.baseEuler,
            mirrorX = s.mirrorX,
            codeDriven = s.codeDriven,
            solid = s.solid,
            inner = s.inner,
            graft = s.graft,
            parent = s.parent,
            attach = s.attach,
            attachOffset = s.attachOffset,
            parts = s.parts,
            chain = s.chain,
            chainTaper = s.chainTaper,
            linkDiameter = s.linkDiameter,
            sizeRel = s.sizeRel,
            formFrom = s.formFrom,
            formRole = s.formRole,
            linkLength = s.linkLength,
            linkTaper = s.linkTaper,
        };
    }

    static BodySocket FindSocket(SpeciesSO species, string name)
    {
        if (species == null || species.sockets == null) return null;
        foreach (var s in species.sockets) if (s != null && s.name == name) return s;
        return null;
    }

    static bool PlansEqual(BodySocket[] a, BodySocket[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        const float eps = 1e-6f;
        for (int i = 0; i < a.Length; i++)
        {
            var x = a[i]; var y = b[i];
            if (x == null && y == null) continue;
            if (x == null || y == null) return false;
            if (x.name != y.name) return false;
            if ((x.sizeRel - y.sizeRel).sqrMagnitude > eps) return false;
            if (Mathf.Abs(x.attach - y.attach) > eps) return false;
            if ((x.attachOffset - y.attachOffset).sqrMagnitude > eps) return false;
            if ((x.baseEuler - y.baseEuler).sqrMagnitude > eps) return false;
        }
        return true;
    }

    /// <summary>Бюджет: трис на существо и на 25 в кадре. 324 квада ≈ 648 трис (2 на квад) + диаманты/шапки ≈830 (было 310/800; ADR-1 хребет 8×10).
    /// Проверяется детектором, а не глазом.</summary>
    public static int BudgetTrisPerCreature => 830;
    public static int BudgetTris25 => 20750;
}
