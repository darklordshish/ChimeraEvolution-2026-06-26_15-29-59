using UnityEngine;

/// <summary>
/// Вид/тело как данные — один ассет на вид («Человек», «Волк»). Две роли:
///  • ШАССИ — базовое тело: ёмкость пула + органы по умолчанию для каждого слота;
///  • ДОНОР — источник органов, которые ставятся в слоты шасси при химеризации.
/// Органы лежат внутри вида (не отдельными файлами). Готовит смену шасси и мультивид.
/// </summary>
[CreateAssetMenu(menuName = "Chimera/Вид (SpeciesSO)", fileName = "Species")]
public class SpeciesSO : ScriptableObject
{
    public string speciesName = "Вид";
    public Color tint = Color.gray;   // цвет тела при озверении этим видом (лерп шкалы мозга)
    public int mutagenPool = 10;      // ёмкость пула, когда вид = шасси
    public Organ[] organs;            // органы вида (по одному на покрываемый слот)
}

/// <summary>
/// Орган: вклад в статы своего слота (абсолютные значения; человеческие = базовая норма).
/// Каждый орган заполняет только «свои» статы, остальные = 0 — суммирование по слотам даёт итог.
/// </summary>
[System.Serializable]
public class Organ
{
    public string organName = "Орган";
    public string slot = "Руки";   // в какой слот шасси ставится
    public string hotkey = "1";    // временный бинд клавиши (MVP-конструктор)
    public int cost;               // цена в пуле

    public int damage, maxHp, lifeSteal;
    public float range, atkCooldown, moveSpeed, dashSpeed, dashCooldown, damageReduction, regen, regenOOC;
    public bool enablesBite, enablesScent;
}
