using UnityEngine;

// PARTIAL-SPLIT #4 (рефактор ядра): ПАЛИТРА ТЕЛА вынесена сюда из CreatureBody.cs — тот же класс,
// НОЛЬ изменений поведения. Концерн: «цвет тела ИГРОКА = смесь тинтов видов надетых органов».
// CompositionTint = смесь по составу (общая для тела и запахового следа); UpdateTint = раздача её по рендерерам.
// Зовутся из Recompute (осталась в CreatureBody.cs) через partial; SpeciesTint — в CreatureBody.Identity.cs.
// Поля renderers/mpb ПРИСВАИВАЮТСЯ в Awake (лайфсайкл, остался в ядре) — partial-доступ через тот же класс.
public partial class CreatureBody
{
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    // цвет тела ИГРОКА = СМЕСЬ тинтов ВИДОВ надетых органов (CompositionTint). NPC сюда не заходят
    // (запечённый материал — не драться с Telegraph), но их ЗАПАХ красится той же смесью в Recompute.
    void UpdateTint()
    {
        Color body = CompositionTint();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, body);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    // СМЕСЬ тинтов ВИДОВ по составу тела: человеческий слот → тинт шасси (телесный), звериный → тинт
    // вида-донора. Отражает СОСТАВ (волчий билд серее, змеиный зеленее; чем химернее, тем «грязнее»/чуждее —
    // визуальная цена химеризации). ОБЩАЯ для палитры тела и запахового следа (видовой отпечаток в запахе)
    Color CompositionTint()
    {
        float r = 0f, g = 0f, b = 0f; int n = 0;
        foreach (var sl in slots)
        {
            if (sl.Empty) continue;                                                  // пустой химерный слот не считаем
            Color t = sl.Installed ? SpeciesTint(sl.DonorSpecies)                    // звериный орган → тинт его вида
                                   : (chassis != null ? chassis.tint : Color.gray);  // родной → телесный
            r += t.r; g += t.g; b += t.b; n++;
        }
        return n > 0 ? new Color(r / n, g / n, b / n) : (chassis != null ? chassis.tint : Color.gray);
    }
}
