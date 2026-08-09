using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КАМУФЛЯЖ-В-НЕПОДВИЖНОСТИ (Чешуя змеи): прячет меши ТЕЛА, пока существо почти не движется И не «раскрыто».
/// Раскрыт = телеграф (замах/приём/захват/гремок) ИЛИ боль (стаггер/обхват) ИЛИ ПОЛУЧЕННЫЙ УРОН (память
/// damageRevealTime — «нельзя прятаться, пока рвут»). Двинулось / атакует / ранено → видно; замерло в безопасности
/// (напр. на насесте-стене) → растворяется. Скорость меряем СВОИМ смещением transform (climb двигает напрямую).
/// След (TrailRenderer) НЕ прячем: запах остаётся зацепкой для нюха (RPS). Вешает CreatureBody по флагу Шкуры.
/// Симметрия: игрок со змеиной кожей тоже растворяется на месте.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Camouflage : MonoBehaviour
{
    [SerializeField] float moveThreshold = 0.6f; // ниже этой горизонтальной скорости считаемся «неподвижным»
    [SerializeField] float damageRevealTime = 1.5f; // укушенный/раненый НЕ прячется: раскрыт на это (> паузы между укусами стаи → «нельзя прятаться, пока рвут»)

    Telegraph telegraph;
    Stagger stagger;
    Health health;
    Renderer[] bodyRenderers;
    bool hidden;
    float revealUntil;
    Vector3 lastPos; // скорость меряем СВОИМ смещением transform, не controller.velocity: климб двигает змею
                     // напрямую через transform (без Move) — CC-скорость там застревает и ломала бы «неподвижность»

    float stealthUntil;   // психика: держать невидимость ДАЖЕ В ДВИЖЕНИИ (крадётся-ищет засадой) — но раскрытие перебивает

    public bool Hidden => hidden; // психика змеи читает: погремушка видима вместе с телом (плюс мигание гремка)

    // раскрыть на seconds (психика зовёт на время боя — чтобы камуфляж не мигал в паузах между ударами)
    public void Reveal(float seconds) => revealUntil = Mathf.Max(revealUntil, Time.time + seconds);

    // держать стелс в движении на seconds (психика зовёт на прокрадывании-поиске: охотник-невидимка не роит стаю).
    // Раскрытие (атака/урон/боль) всё равно приоритетнее — под ударом не спрятаться
    public void HoldStealth(float seconds) => stealthUntil = Mathf.Max(stealthUntil, Time.time + seconds);

    void Awake()
    {
        TryGetComponent(out telegraph);
        TryGetComponent(out stagger);
        if (TryGetComponent(out health)) health.onDamaged.AddListener(OnDamaged); // рвут/ранят — раскрываемся (не спрятаться под ударом)
        lastPos = transform.position;
        Rebuild();
    }

    /// <summary>Пере-собрать меши тела. ЗОВЁТ `CreatureBody` ПОСЛЕ КАЖДОЙ СБОРКИ МОРФА: части рождаются в
    /// рантайме, и ссылки, снятые в `Awake`, к этому моменту мертвы — камуфляж прятал префабные меши,
    /// которых уже нет, и не видел звеньев цепи. Змея просто переставала исчезать, без единой ошибки
    /// в консоли. Ровно та же гоча была у телеграфа (там `RebuildRenderers`), лечится так же.</summary>
    public void Rebuild()
    {
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue; // след/линии не прячем — запах = зацепка
            // каналы чувств НЕ трогаем — ими рулят свои системы: термо-контур (HeatGhost, сквозь стены!)
            // гаснет сам по холоднокровности, аура запаха (Sphere) — по нюху. Камуфляж прячет ТЕЛО ЦЕЛИКОМ,
            // включая глаза: невидимость честная, а зацепка остаётся через другие чувства (КНБ восприятия).
            // Прежде в списке значился ещё «RattleMesh» — имя из префабной змеи, которого после перевода
            // погремушки на сокеты не существует; исключение молча перестало что-либо исключать
            string n = r.gameObject.name;
            if (n == "HeatGhost" || n == "Sphere") continue;
            list.Add(r);
        }
        bodyRenderers = list.ToArray();
        // пересборка во время невидимости: новые части рождаются ВИДИМЫМИ — гасим их сразу, иначе
        // сменившая состав змея проступит на насесте, хотя по состоянию она спрятана
        if (hidden) foreach (var r in bodyRenderers) if (r != null) r.enabled = false;
    }

    void OnDisable() => SetHidden(false); // сняли компонент/выключили — вернуть видимость
    void OnDestroy() { if (health != null) health.onDamaged.RemoveListener(OnDamaged); }

    void OnDamaged() => Reveal(damageRevealTime); // получили урон — раскрыты (нельзя раствориться, пока рвут)

    void Update()
    {
        // СВОЁ смещение за кадр (горизонталь) → скорость: работает и для CharacterController.Move, и для
        // прямого transform-климба (где controller.velocity застревает). Неподвижен на насесте → прячемся
        Vector3 delta = transform.position - lastPos; delta.y = 0f;
        lastPos = transform.position;
        float speedSqr = delta.sqrMagnitude / Mathf.Max(Time.deltaTime * Time.deltaTime, 1e-8f);
        bool revealed = (telegraph != null && telegraph.IsShowing) || Time.time < revealUntil
                        || (stagger != null && stagger.IsStaggered); // телеграф / память боя-урона / боль (стаггер, обхват) = раскрыт
        bool still = speedSqr < moveThreshold * moveThreshold || Time.time < stealthUntil; // неподвижен ИЛИ психика держит стелс на ходу
        SetHidden(still && !revealed);
    }

    void SetHidden(bool h)
    {
        if (h == hidden) return;
        hidden = h;
        foreach (var r in bodyRenderers) if (r != null) r.enabled = !h;
    }
}
