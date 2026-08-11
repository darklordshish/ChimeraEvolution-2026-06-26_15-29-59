using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ЯЗЫК ЦВЕТА: слои с приоритетами вместо исключений (спека `2026-08-09-yazyk-cveta-i-vospriyatie-na-morde`).
/// Цветом существа заведовали пятеро — тинт по составу, телеграф, эмоция, вспышка урона, стан — и каждый
/// затирал предыдущего; развязка держалась на костыле `Telegraph.Rebase()`. Теперь источники не красят, а
/// ЗАЯВЛЯЮТ СЛОЙ, и микшер собирает итог снизу вверх: `color = Lerp(color, слой, вес)` по возрастанию
/// приоритета. Добавить источник или переставить порядок — это число у источника, а не правка чужого кода.
///
/// БАЗА СВОЯ У КАЖДОЙ ДЕТАЛИ: собственный цвет из `PartMark`, иначе общий цвет по составу. Глаза не зеленеют
/// от змеиного билда не потому, что кто-то их исключил, а потому что состав до них не доходит.
///
/// Пишет ПО СОБЫТИЮ (смена слоя или пересборка тела), а не каждый кадр: пока телеграф со вспышками красят
/// напрямую (слайсы S2–S3), они ложатся поверх, как и раньше, — перевод систем идёт без мигания.
/// </summary>
public class TintMixer : MonoBehaviour
{
    /// <summary>На что ложится слой: на всё тело или только на куски одной роли (эмоция — на морду).</summary>
    public enum Scope { All, Role }

    struct Layer
    {
        public string id;        // кто заявил (им же снимает)
        public Color color;
        public float weight;     // 0 — нет эффекта, 1 — перекрывает нижние целиком
        public int priority;     // кто поверх кого
        public Scope scope;
        public PartRole role;    // для Scope.Role
    }

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    readonly List<Layer> layers = new();
    Renderer[] rends;
    PartMark[] marks;            // паспорт детали (может быть null — обычный кусок)
    MaterialPropertyBlock mpb;
    Color composition = Color.gray;   // цвет по составу — база для всех, у кого нет своей окраски
    bool hasComposition;              // задан ли он вообще: у обычных NPC материал ЗАПЕЧЁН, и красить их нечем
    bool dirty;

    void Awake() { mpb = new MaterialPropertyBlock(); Rebuild(); }

    /// <summary>Пере-собрать рендереры после морфа. Тело зовёт после каждой сборки: части рождаются в
    /// рантайме, и ссылки, снятые в Awake, к этому моменту уже мертвы — на этом сгорели и телеграф,
    /// и камуфляж.</summary>
    public void Rebuild()
    {
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);   // след/линии не красим
        rends = list.ToArray();
        marks = new PartMark[rends.Length];
        for (int i = 0; i < rends.Length; i++) rends[i].TryGetComponent(out marks[i]);
        dirty = true;
    }

    /// <summary>Цвет по составу — база тела. Отдельно от слоёв: это не эффект, а то, из чего существо сделано.</summary>
    public void SetComposition(Color c)
    {
        if (hasComposition && composition == c) return;
        composition = c;
        hasComposition = true;
        dirty = true;
    }

    /// <summary>Заявить (или обновить) слой. `id` — ключ источника: повторный вызов заменяет прежний,
    /// поэтому источнику не нужно помнить, заявлял он уже или нет.</summary>
    public void Set(string id, int priority, Color color, float weight, Scope scope = Scope.All, PartRole role = PartRole.None)
    {
        var l = new Layer { id = id, color = color, weight = Mathf.Clamp01(weight), priority = priority, scope = scope, role = role };
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].id == id)
            {
                if (layers[i].color == l.color && Mathf.Approximately(layers[i].weight, l.weight)
                    && layers[i].priority == l.priority && layers[i].scope == l.scope && layers[i].role == l.role) return;
                layers[i] = l; dirty = true; return;
            }
        layers.Add(l);
        layers.Sort((a, b) => a.priority.CompareTo(b.priority));   // снизу вверх — порядок наложения
        dirty = true;
    }

    /// <summary>Снять слой. Молча, если его нет: источнику не надо следить за собственным состоянием.</summary>
    public void Clear(string id)
    {
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].id == id) { layers.RemoveAt(i); dirty = true; return; }
    }

    void LateUpdate() { if (dirty) Apply(); }

    /// <summary>Пересчитать и записать НЕМЕДЛЕННО. Нужно там, где следом читают итоговый цвет: телеграф
    /// снимает им «родную» базу (`Rebase`), и, отложи мы запись до конца кадра, он снял бы прошлый цвет.</summary>
    public void Apply()
    {
        if (rends == null) return;
        dirty = false;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            var mark = marks[i];
            bool own = mark != null && mark.HasOwn;
            // ЧУЖОЕ НЕ ТРОГАЕМ: у обычного NPC материал запечён — состава ему никто не задавал, слоёв нет,
            // и красить его нечем. Без этой проверки микшер залил бы волка с лосём серым по построению
            if (!own && !hasComposition) continue;
            // БАЗА: своя окраска детали, иначе цвет по составу
            Color c = own ? mark.own : composition;
            PartRole role = mark != null ? mark.role : PartRole.None;

            for (int k = 0; k < layers.Count; k++)          // отсортированы по приоритету
            {
                var l = layers[k];
                if (l.scope == Scope.Role && l.role != role) continue;
                c = Color.Lerp(c, l.color, l.weight);
            }

            rends[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, c);
            rends[i].SetPropertyBlock(mpb);
        }
    }
}
