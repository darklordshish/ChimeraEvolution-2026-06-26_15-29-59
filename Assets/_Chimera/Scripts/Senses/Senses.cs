using UnityEngine;

/// <summary>
/// Сенсорный ПРОФИЛЬ существа (S1): держит каналы чувств и отдаёт психике/восприятию ПЕР-СОСТОЯНЧАТУЮ
/// дальность (читает свой `AlertState`). Матрица-способно: будущее чувство = новый канал; новый вид =
/// свой профиль ДАННЫМИ (на префабе или сид из психики). Наполнение ленивое — сейчас волк/змея сидят на
/// прежних дальностях, множители =1 (фил не тронут); острота и эмиттер-сторона богатеют в S2.
/// </summary>
public class Senses : MonoBehaviour
{
    [SerializeField] SenseChannel sight = new();
    [SerializeField] SenseChannel thermal = new();
    [SerializeField] SenseChannel scent = new();
    [SerializeField] SenseChannel hearing = new(); // СЛУХ (ось звука, срез B лося): дальность уха; источники — Noise

    AlertState alert;

    SenseChannel Ch(SenseKind k) =>
        k == SenseKind.Sight ? sight : k == SenseKind.Thermal ? thermal : k == SenseKind.Hearing ? hearing : scent;

    /// <summary>Дальность чувства с учётом ТЕКУЩЕГО состояния восприятия (Спок/Настор/Атака).</summary>
    public float Range(SenseKind k)
    {
        if (alert == null) TryGetComponent(out alert); // ленивая привязка (порядок Awake не гарантирован)
        return Ch(k).For(alert != null ? alert.State : Alert.Wary);
    }

    public float Acuity(SenseKind k) => Ch(k).acuity;

    /// <summary>Полу-угол сектора обзора чувства (180 = круговое; зрение — узкий конус).</summary>
    public float ViewHalfAngle(SenseKind k) => Ch(k).viewHalfAngle;

    /// <summary>Сид базовой дальности из психики — только если канал ещё НЕ настроен на префабе (range ≤ 0).</summary>
    public void Seed(SenseKind k, float range)
    {
        var c = Ch(k);
        if (c.range <= 0f) c.range = range;
    }

    /// <summary>Сид сектора обзора из психики — только если канал ещё круговой по умолчанию (не задан на префабе).</summary>
    public void SeedViewAngle(SenseKind k, float halfAngle)
    {
        var c = Ch(k);
        if (c.viewHalfAngle >= 180f) c.viewHalfAngle = halfAngle;
    }
}
