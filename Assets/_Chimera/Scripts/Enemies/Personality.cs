using UnityEngine;

/// <summary>
/// ЛИЧНОСТЬ ОСОБИ (сенсорный слайс S1, срез 6): расширение `SpawnVariance` на ПОВЕДЕНИЕ. Катается раз на
/// спавне (устойчиво — не шум), делает особей в стае чуть разными = «колесо жизни экосистемы». Три оси:
/// ХРАБРОСТЬ (порог Страха — кто ломается первым), АГРЕССИЯ (как охотно лезет в атаку / частит), ЛЮБОПЫТСТВО
/// (как охотно идёт на зацепку — гремок/след). Игрок детерминирован (личность не вешаем). Психика опрашивает.
/// </summary>
public class Personality : MonoBehaviour
{
    [SerializeField] Vector2 braveryRange = new(2f, 5f);         // порог Страха (сколько накопить до паники)
    [SerializeField] Vector2 aggressionRange = new(0.85f, 1.2f); // >1 — чаще атакует (короче кулдаун); <1 — осторожнее
    [SerializeField] Vector2 curiosityRange = new(0.7f, 1.3f);   // множитель памяти любопытства (охота проверять гремок/след)

    public float Bravery { get; private set; }
    public float Aggression { get; private set; }
    public float Curiosity { get; private set; }

    void Awake()
    {
        Bravery = Random.Range(braveryRange.x, braveryRange.y);
        Aggression = Random.Range(aggressionRange.x, aggressionRange.y);
        Curiosity = Random.Range(curiosityRange.x, curiosityRange.y);
    }
}
