using Chimera.Laws;
using UnityEngine;

/// <summary>
/// ЧИСЛА ЗАКОНОВ СТАТУСОВ И СЫТОСТИ — один конфиг-ассет `Resources/LawsConfig.asset` (план Ф0, С5). Раньше они жили
/// инициализаторами полей компонентов, которые до-создаются в рантайме (`Hit.cs`), то есть были константами кода: ни в
/// сцене, ни в симуляторе их не поправить. Теперь компоненты читают отсюда, симулятор берёт те же значения.
///
/// ЛЕНИВАЯ ЗАГРУЗКА: `Current` грузит ассет при первом обращении; нет ассета — собирается из `Default` законов, то есть
/// тот же набор, молча. Тест, подменивший конфиг (`Override`), обязан вернуть его в TearDown (`ResetCurrent`) — статика
/// переживает тест. Новое поле структуры приходит в старый ассет НУЛЁМ — заводить его в `Default` и в ассет вместе;
/// сторож `LawsConfigTests` сверяет ассет с `Default`.
/// </summary>
[CreateAssetMenu(menuName = "Chimera/LawsConfig", fileName = "LawsConfig")]
public class LawsConfig : ScriptableObject
{
    public VenomParams venom = VenomParams.Default;
    public BleedParams bleed = BleedParams.Default;
    public RageParams rage = RageParams.Default;
    public SlowParams slow = SlowParams.Default;
    public ThornsParams thorns = ThornsParams.Default;
    public HungerParams hunger = HungerParams.Default;

    static LawsConfig current;

    /// <summary>Действующий конфиг: подмена теста, иначе ассет из `Resources`, иначе значения по умолчанию.</summary>
    public static LawsConfig Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<LawsConfig>("LawsConfig");
                if (current == null) current = CreateInstance<LawsConfig>(); // ассета нет — те же числа из Default
            }
            return current;
        }
    }

    /// <summary>Подменить конфиг (тесты). Вернуть — `ResetCurrent()` в TearDown.</summary>
    public static void Override(LawsConfig config) => current = config;

    public static void ResetCurrent() => current = null;
}
