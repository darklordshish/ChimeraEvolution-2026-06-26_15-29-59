using System;

namespace Chimera.Laws
{
    /// <summary>
    /// ВХОДЫ ЗАКОНОВ ОДНИМ ФАЙЛОМ (план Ф0, С9): то, что игра выгружает командой `chimera-sim-export` в
    /// `Docs/Диаграммы/Эволюция/данные.json` и что читает симулятор вне Unity. Простые классы с публичными полями — их
    /// понимают и `JsonUtility` (пишет игра), и `System.Text.Json` с `IncludeFields` (читает симулятор). Без типов движка.
    /// Версия схемы — `Schema`: меняешь поля — поднимай, симулятор откажется читать чужую схему.
    /// Отпечаток — FNV-1a 64 по тексту файла с пустым полем `fingerprint`, без даты: тот же состав — тот же отпечаток.
    /// </summary>
    [Serializable]
    public class SimData
    {
        public const int Schema = 1;

        public int schema = Schema;
        public string fingerprint = "";
        public SimWorld world = new SimWorld();
        public SimLaws laws = new SimLaws();
        public SimEvolution evolution = new SimEvolution();
        public SimSpecies[] species = new SimSpecies[0];
        public SimLair[] lairs = new SimLair[0];

        /// <summary>FNV-1a 64 по тексту; `\r` не считается — git может сменить перевод строки, содержимое то же.</summary>
        public static string Fnv64(string text)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
            {
                if (b == (byte)'\r') continue;
                h ^= b;
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }
    }

    /// <summary>Мир: диаметр и площадь купола (пока карты нет — из сцены `ForestPavilion`), длина игровых суток.</summary>
    [Serializable]
    public class SimWorld
    {
        public string scene = "";
        public float mapDiameter;
        public float domeArea;          // π·(Ø/2)², м²
        public float dayMinutes;        // реальных минут на игровые сутки (`WorldClock`)
        public float hoursPerSecond;    // игровых часов в реальной секунде (то же, что `WorldClock.HoursPerSecond`)
    }

    /// <summary>Числа законов статусов и сытости — действующий `LawsConfig`.</summary>
    [Serializable]
    public class SimLaws
    {
        public VenomParams venom;
        public BleedParams bleed;
        public RageParams rage;
        public SlowParams slow;
        public ThornsParams thorns;
        public HungerParams hunger;
    }

    /// <summary>Тюнинг эволюции павильона (`EvolutionConfig`).</summary>
    [Serializable]
    public class SimEvolution
    {
        public bool evolveNpc;
        public int startAffinity;
        public float chimerizeMultiplier;
        public string[] allSpecies = new string[0];
    }

    [Serializable]
    public class SimNumber
    {
        public string name = "";
        public float value;
        public bool isInt;
        public bool expressed;        // раскрывается экспрессией (`[Expressed]`)
        public bool lowerIsBetter;    // при дублях берётся минимум (`[LowerIsBetter]`)
    }

    /// <summary>Запись приёма органа (`AbilityData`): тип и все числа (float/int) как в ассете, до экспрессии.</summary>
    [Serializable]
    public class SimAbility
    {
        public string type = "";
        public bool nativeOnly;
        public SimNumber[] numbers = new SimNumber[0];

        public bool TryGet(string name, out SimNumber n)
        {
            foreach (var x in numbers) if (x.name == name) { n = x; return true; }
            n = null;
            return false;
        }
    }

    [Serializable]
    public class SimOrgan
    {
        public string name = "";
        public string slot = "";
        public int cost;
        public bool chassisOnly, nativeOnly;
        public string nativeChassis = "";
        public OrganValues values;
        public SimAbility[] abilities = new SimAbility[0];
    }

    /// <summary>Числа с префаба вида: экспрессия, разброс, кривые родства, ритм психики. Нет префаба (человек) — `hasPrefab = false`
    /// и значения по умолчанию тела.</summary>
    [Serializable]
    public class SimPrefab
    {
        public bool hasPrefab;
        public string prefab = "";
        public float expression;
        public bool applyVitals = true;
        public float hpSpread, damageSpread, speedSpread;
        public float maxDamageReduction = 0.6f, minAtkCooldown = 0.2f;
        public float discountPerAffinity = 0.008f, maxDiscount = 0.8f;
        public float bonusStartAffinity, bonusFullAffinity = 100f, maxBonusMult = 2f, affinityPerOrgan = 0.55f;
        public string psyche = "";
        public float psycheRhythm;      // `attackCooldown` психики; 0 — психики нет
    }

    [Serializable]
    public class SimSpecies
    {
        public string name = "";
        public int mutagenPool;
        public bool eatsMeat, massive;
        public float formScale = 1f, mass;
        public float baseHp, baseStamina, baseStaminaRegen;   // как их читает тело: ноль в ассете уже заменён дефолтом
        public SimOrgan[] organs = new SimOrgan[0];
        public SimPrefab prefab = new SimPrefab();
    }

    /// <summary>Логово павильона (`LairSite`): вид, ёмкость, темп в реальных секундах и в игровых часах, страх.</summary>
    [Serializable]
    public class SimLair
    {
        public string name = "";
        public string species = "";
        public string areaType = "";
        public int capacity, tier;
        public float homeRadius, spawnRadius, fearRadius;
        public float respawnSeconds, respawnGameHours;
        public float fearDecayPerSecond, fearDecayPerGameHour, fearPerKill, exhaustFear;
        public bool spawnEnabled = true;
        public float x, z;              // метры в системе мира
    }
}
