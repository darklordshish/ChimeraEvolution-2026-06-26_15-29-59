using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ПОДРЫВ ПРИЗНАНИЯ (K3, шкала предательства на теле ИГРОКА): удар по кину копит временные стаки ПО ВИДУ
/// жертвы. Эффективная идентичность к виду = базовая − эрозия (сумма живых стаков × шаг). Кины судят игрока
/// по ЭФФЕКТИВНОЙ (CreatureBody.Tier читает Erosion): просела ниже признания — «свой» стал чужим, бьёт штатно.
/// Стаки живут секунды и сами гаснут — перестал махать по своим, признание восстановилось. Симметрично
/// Morale (стек-шкала с временем жизни). Числа тюнятся: повесь компонент на игрока в редакторе.
/// </summary>
public class Betrayal : MonoBehaviour
{
    [SerializeField] float stackLife = 2.5f;  // сколько живёт один стак предательства
    [SerializeField] float perStack = 0.12f;  // −идентичности за стак (≈12%): ~3 удара валят сильного кина, слабого — с первого

    // стаки по виду (ключ = имя вида): времена истечения, как у Morale — считаем живые
    readonly Dictionary<string, List<float>> bySpecies = new();

    /// <summary>Удар по кину вида species — повесить стак предательства.</summary>
    public void Hit(SpeciesSO species)
    {
        if (species == null) return;
        if (!bySpecies.TryGetValue(species.speciesName, out var list))
        { list = new List<float>(); bySpecies[species.speciesName] = list; }
        list.Add(Time.time + stackLife);
    }

    /// <summary>Эрозия признания к виду: сумма живых стаков × шаг. Попутно чистит протухшие.</summary>
    public float Erosion(SpeciesSO species)
    {
        if (species == null || !bySpecies.TryGetValue(species.speciesName, out var list) || list.Count == 0) return 0f;
        int alive = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (Time.time >= list[i]) list.RemoveAt(i);
            else alive++;
        }
        return alive * perStack;
    }
}
