using UnityEngine;

// PARTIAL-SPLIT #3 (рефактор ядра): ДВИЖОК ЭКСПРЕССИИ вынесен сюда из CreatureBody.cs — тот же класс,
// НОЛЬ изменений поведения. Концерн: «как ОДИН надетый орган раскрывается в статы» (правило бленда/мощи).
// Contribution = вклад органа; Express = единое правило раскрытия; Blend/ChassisOrgan/EmptyOrgan — его матчасть.
// АГРЕГАЦИЯ вкладов и РАЗДАЧА по компонентам (Recompute) — отдельный концерн, остались в CreatureBody.cs
// (зовут Express/Contribution.Sup через partial). Потребитель мощи BonusMultiplier — в CreatureBody.Affinity.cs.
public partial class CreatureBody
{
    // вклад одного надетого органа в статы тела (после бленда/экспрессии)
    struct Contribution
    {
        public float dmg, hpBonus, stam, stamRegen, life, rng, atkCd, mv, dash, dashDur, dashCd, reduce, regen, regenOOC, thermal, howlR, howlStunAt;
        public int venom, bleed;
        public bool bite, scent, kick, howl, cold, camo, thermalOn, constrict, digest, bellow, antler, charge, roll, curl;
        public bool thorns, venomResist, quillVolley; // иглы-ответка, ядоупорность, залп (ёж)
        public bool bleedResist;  // кровеупорность (лосиное сердце)
        public float volleyMult; // мощь залпа от родства с ежом (0 = залпа нет)
        public bool insight; // ЧУТЬЁ УЧЁНОГО: распознавание намерений + числа состояний (человеческое Чутьё)
        public bool keenEar;  // ОСТРЫЙ СЛУХ: различение вида источника + волны звука на экране
        public float earMult; // множитель дальности слуха (супремум дублей)
        public int constrictCap; // эффективный кап стадии захвата органа: native ? constrictStage : min(2, constrictStage); 0 = не грэпл

        // СУПРЕМУМ дублей одного типа слота: скаляры — max (кулдауны — min: меньше = лучше), флаги — OR.
        // Дубль оси силу НЕ растит (второе сердце ≠ ×2 регена) — окупается только НОВЫМ направлением.
        public static Contribution Sup(Contribution a, Contribution b) => new()
        {
            dmg = Mathf.Max(a.dmg, b.dmg), hpBonus = Mathf.Max(a.hpBonus, b.hpBonus), life = Mathf.Max(a.life, b.life),
            stam = Mathf.Max(a.stam, b.stam), stamRegen = Mathf.Max(a.stamRegen, b.stamRegen),
            rng = Mathf.Max(a.rng, b.rng), atkCd = Mathf.Min(a.atkCd, b.atkCd),
            mv = Mathf.Max(a.mv, b.mv), dash = Mathf.Max(a.dash, b.dash), dashDur = Mathf.Max(a.dashDur, b.dashDur), dashCd = Mathf.Min(a.dashCd, b.dashCd),
            reduce = Mathf.Max(a.reduce, b.reduce), regen = Mathf.Max(a.regen, b.regen),
            regenOOC = Mathf.Max(a.regenOOC, b.regenOOC), thermal = Mathf.Max(a.thermal, b.thermal),
            howlR = Mathf.Max(a.howlR, b.howlR),
            howlStunAt = Mathf.Max(a.howlStunAt, b.howlStunAt),
            venom = Mathf.Max(a.venom, b.venom), bleed = Mathf.Max(a.bleed, b.bleed),
            bite = a.bite || b.bite, scent = a.scent || b.scent, kick = a.kick || b.kick,
            howl = a.howl || b.howl, cold = a.cold || b.cold, camo = a.camo || b.camo,
            thermalOn = a.thermalOn || b.thermalOn, constrict = a.constrict || b.constrict,
            constrictCap = Mathf.Max(a.constrictCap, b.constrictCap),
            digest = a.digest || b.digest, bellow = a.bellow || b.bellow, antler = a.antler || b.antler,
            charge = a.charge || b.charge, roll = a.roll || b.roll, curl = a.curl || b.curl, insight = a.insight || b.insight,
            keenEar = a.keenEar || b.keenEar, earMult = Mathf.Max(a.earMult, b.earMult),
            thorns = a.thorns || b.thorns, venomResist = a.venomResist || b.venomResist,
            quillVolley = a.quillVolley || b.quillVolley, volleyMult = Mathf.Max(a.volleyMult, b.volleyMult),
            bleedResist = a.bleedResist || b.bleedResist,
        };
    }

    static readonly Organ EmptyOrgan = new(); // «нет базы»: бленд от нуля = чистый орган × мощь

    // родной орган шасси для типа слота — ТО, ЧТО ДОНОРСКИЙ ВЫТЕСНЯЕТ (база экспрессии).
    // Живёт в теле, а не полем слота: база — функция шасси, слот её только использует
    Organ ChassisOrgan(string slotName)
    {
        if (chassis == null || chassis.organs == null) return null;
        foreach (var o in chassis.organs) if (o.slot == slotName) return o;
        return null;
    }

    /// <summary>ЕДИНОЕ ПРАВИЛО ЭКСПРЕССИИ для любого органа в любом слоте (было двумя копипаст-ветками).
    /// База — то, что орган ВЫТЕСНИЛ:
    ///  • РОДНОЙ орган шасси не вытесняет ничего (он и есть оригинал) → раскрывается ОТ СЕБЯ: величины ×мощь;
    ///  • ДОНОРСКИЙ вытесняет родной орган этого слота → блендится ОТ НЕГО (низкое родство ≈ человек,
    ///    высокое ≈ зверь, овершут — за зверя). В ХИМЕРНОМ слоте вытеснять нечего (орган ДОПОЛНИТЕЛЬНЫЙ) → от нуля.
    /// Режим решает `native`, а НЕ «есть ли база»: иначе донорский орган в химерном слоте поменял бы
    /// поведение времён и вне-боевого регена (там база пуста, но раскрытие всё равно «графтовое»).</summary>
    Contribution Express(Slot sl)
    {
        var pick = sl.Pick;
        Organ w = pick.organ;
        bool own = pick.native;   // родной орган шасси — раскрывается от себя
        Organ h = own || sl.chimera ? EmptyOrgan : ChassisOrgan(sl.name) ?? EmptyOrgan;
        float m = BonusMultiplier(pick.species); // у родного варианта species = вид шасси → та же ручка

        float Scaled(float hv, float wv) => own ? wv * m : Blend(hv, wv, m);
        float Timed(float hv, float wv) => own ? wv : Blend(hv, wv, m); // СВОЁ время не растягиваем: ×2 на кулдаун = наказание за свой вид

        // ЭФФЕКТИВНЫЙ КАП ЗАХВАТА: нативен для шасси → полная сила органа, чужой → min(2, сила).
        // 0 у enablesConstrict-органа = «не настроено» → дефолт 3 (старое нативное); после бутстрапа не встречается
        int cStage = w.constrictStage > 0 ? w.constrictStage : 3;
        bool cNative = chassis != null && w.nativeChassis == chassis.speciesName;

        return new Contribution
        {
            dmg = Scaled(h.damage, w.damage),
            hpBonus = Scaled(h.hpBonus, w.hpBonus), // ДОЛЯ базы шасси — экспрессия раскрывает бонус, не тело
            stam = Scaled(h.staminaBonus, w.staminaBonus),
            stamRegen = Scaled(h.staminaRegenBonus, w.staminaRegenBonus),
            life = Scaled(h.lifeSteal, w.lifeSteal),
            atkCd = Timed(h.atkCooldown, w.atkCooldown),
            mv = Scaled(h.moveSpeed, w.moveSpeed),
            dash = Scaled(h.dashSpeed, w.dashSpeed),
            dashCd = Timed(h.dashCooldown, w.dashCooldown),
            reduce = Scaled(h.damageReduction, w.damageReduction),
            regen = Scaled(h.regen, w.regen),
            // вне-боя реген: у ДОНОРСКОГО как есть (бленд на мощи 2 уводит в минус), у РОДНОГО раскрываем —
            // иначе человеческое сердце единственное не растёт с родством к своему виду
            regenOOC = own ? w.regenOOC * m : w.regenOOC,
            // ДИСКРЕТНОЕ — всегда у надетого как есть: фичи не «раскрываются», они либо есть, либо нет
            venom = w.venomStacks, bleed = w.bleedStacks,
            rng = w.range, dashDur = w.dashDuration, thermal = w.thermalRange,
            howlR = w.howlRadius, howlStunAt = w.howlStunAt,
            bite = w.enablesBite, scent = w.enablesScent, kick = w.enablesKick,
            howl = w.enablesHowl, cold = w.coldBlooded, camo = w.camo, thermalOn = w.enablesThermal,
            constrict = w.enablesConstrict, digest = w.digestion, bellow = w.enablesBellow,
            antler = w.enablesAntler, charge = w.enablesCharge, roll = w.enablesRoll, curl = w.enablesCurl, insight = w.insight,
            keenEar = w.keenHearing, earMult = w.hearingMult,
            thorns = w.thorns, venomResist = w.venomResist, quillVolley = w.enablesQuillVolley,
            volleyMult = w.enablesQuillVolley ? m : 0f, // мощь залпа = экспрессия органа-придатка (родство с ежом)
            bleedResist = w.bleedResist,
            constrictCap = w.enablesConstrict ? (cNative ? cStage : Mathf.Min(2, cStage)) : 0,
        };
    }

    // человеч.значение + (звериное − человеч.) × множитель: на ×1 = звериное, на ×2 = вдвое дальше от человека
    static float Blend(float human, float beast, float mult) => human + (beast - human) * mult;
}
