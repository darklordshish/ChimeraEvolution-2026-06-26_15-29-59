using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// МЕХАНИКА ПРИЁМОВ ОТНОСИТЕЛЬНО ДАННЫХ (спека `2026-09-12-dannye-v-organah.md`). Проверяется ПРОВОДКА, а не баланс:
    /// приём бьёт ровно тем, что записано в органе (после закона экспрессии), есть ровно пока надет орган, и одна запись
    /// кормит и NPC, и игрока. Ожидания читаются из тестовых записей и из раскрытой записи тела — поворот ручки
    /// в игре тест не краснит, разрыв проводки краснит. Индивидуальность выключена: средняя особь, урон не плавает.
    /// </summary>
    public class AbilityMechanicsTests
    {
        const string Maw = "Пасть";
        readonly List<Object> trash = new();

        [SetUp]
        public void SetUp()
        {
            IndividualityConfig.On = false;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // пол: доставки стоят на земле через CharacterController
            ground.transform.localScale = Vector3.one * 4f;
            trash.Add(ground);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
            IndividualityConfig.On = true;
            Time.timeScale = 1f; // хитстоп укуса игрока роняет время на миг — не оставить его соседним тестам
        }

        // ── ЗАПИСИ И ВИДЫ ────────────────────────────────────────────────────────────────

        static BiteData Bite(int damage = 14, int bleed = 1, int venom = 1) => new BiteData
        {
            damage = damage, bleedStacks = bleed, venomStacks = venom, regenDebuff = 0.5f, regenDebuffTime = 3f,
            range = 2f, halfAngle = 55f, cooldown = 0.7f,
            windupTime = 0.05f, // короткий замах: тест проверяет проводку, а не телеграф
        };

        static Organ OrganWith(string name, string slot, params AbilityData[] records) =>
            new Organ { organName = name, slot = slot, cost = 1, abilities = records.Length > 0 ? records : null };

        SpeciesSO Species(string name, params Organ[] organs)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 100;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            so.organs = organs;
            trash.Add(so);
            return so;
        }

        // ── СУЩЕСТВА ─────────────────────────────────────────────────────────────────────

        CreatureBody Npc(SpeciesSO chassis, float expression, SpeciesSO[] donors = null)
        {
            var go = new GameObject("NPC-" + chassis.speciesName);
            Capsule(go);
            NoDestroy(go.AddComponent<Health>());
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors ?? new SpeciesSO[0]);
            body.SetExpression(expression); // у NPC мощь органа = экспрессия
            trash.Add(go);
            return body;
        }

        CreatureBody Player(SpeciesSO chassis)
        {
            var go = new GameObject("Игрок");
            go.SetActive(false);                              // собрать до Awake: контроллер требует капсулу и драйвер ввода
            go.transform.position = new Vector3(0f, 1f, 0f);  // корень игрока — центр капсулы
            go.AddComponent<CharacterController>();
            NoDestroy(go.AddComponent<Health>());
            go.AddComponent<PlayerController>();
            var body = go.AddComponent<CreatureBody>();
            go.SetActive(true);
            body.Configure(chassis, new SpeciesSO[0]);
            trash.Add(go);
            return body;
        }

        Health Dummy(Vector3 feet)
        {
            var go = new GameObject("Манекен");
            go.transform.position = feet;
            Capsule(go);
            var h = go.AddComponent<Health>();
            NoDestroy(h);
            h.SetMaxHealth(1000);
            go.AddComponent<Knockback>();
            go.AddComponent<Stagger>();
            trash.Add(go);
            return h;
        }

        static void Capsule(GameObject go)
        {
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.4f; cc.center = Vector3.up;
        }

        static void NoDestroy(Health h) =>
            typeof(Health).GetField("destroyOnDeath", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(h, false);

        static void Teleport(Health target, Vector3 feet)
        {
            var cc = target.GetComponent<CharacterController>();
            cc.enabled = false; // гоча проекта: CharacterController перебивает transform.position
            target.transform.position = feet;
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        static IEnumerator Swing(WindupAbility ability, Health target, float timeout = 3f)
        {
            ability.SetTarget(target);
            Assert.IsTrue(ability.TryUse(), ability.GetType().Name + ": замах не начался");
            AbilityRun state;
            float end = Time.time + timeout;
            do { yield return null; state = ability.Tick(); } while (state == AbilityRun.Running && Time.time < end);
            Assert.AreEqual(AbilityRun.Done, state, ability.GetType().Name + ": приём не дошёл до удара");
        }

        static int BleedOf(Health h) => h.TryGetComponent<Bleed>(out var b) ? b.Stacks : 0;
        static int VenomOf(Health h) => h.TryGetComponent<Venom>(out var v) ? v.Stacks : 0;

        static int SlotIndex(CreatureBody body, string slot)
        {
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == slot) return i;
            Assert.Fail("у тела нет слота " + slot);
            return -1;
        }

        static int VariantOf(CreatureBody body, int slot, string species)
        {
            var variants = body.GetVariants(slot);
            for (int i = 0; i < variants.Count; i++) if (variants[i].species == species) return i;
            Assert.Fail($"в слоте нет органа вида {species}");
            return -1;
        }

        // ── УКУС ─────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Npc_Bite_HitsWithOrganRecord()
        {
            var rec = Bite();
            var body = Npc(Species("Волк", OrganWith("Пасть волка", Maw, rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;

            var bite = body.GetComponent<BiteAbility>();
            Assert.IsNotNull(bite, "тело не завело доставку укуса по записи органа");
            Assert.IsTrue(bite.Available, "запись есть, а укус недоступен");
            Assert.AreEqual(rec.range, bite.Range, 1e-5f, "психика читает досягаемость не из записи");
            Assert.AreEqual(0, bite.Payload().LifeSteal, "у приёма вампиризм — он принадлежит модулю боссовости");

            int before = target.Current;
            yield return Swing(bite, target);
            Assert.AreEqual(rec.damage, before - target.Current, "урон укуса ≠ записи органа (экспрессия 1)");
            Assert.AreEqual(rec.bleedStacks, BleedOf(target), "кровь ≠ записи органа");
            Assert.AreEqual(rec.venomStacks, VenomOf(target), "яд ≠ записи органа");
        }

        [UnityTest]
        public IEnumerator Npc_Bite_ExpressionScalesDamage_NotEffects()
        {
            var rec = Bite(damage: 20, bleed: 1, venom: 0);
            var body = Npc(Species("Волк", OrganWith("Пасть волка", Maw, rec)), expression: 0.5f);
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;

            var resolved = body.Ability<BiteData>();
            Assert.AreEqual(Mathf.RoundToInt(rec.damage * 0.5f), resolved.damage, "урон родного органа = запись × экспрессия");
            Assert.AreEqual(rec.bleedStacks, resolved.bleedStacks, "стаки экспрессией не раскрываются");
            Assert.AreEqual(rec.range, resolved.range, 1e-5f, "форма удара экспрессией не раскрывается");

            int before = target.Current;
            yield return Swing(body.GetComponent<BiteAbility>(), target);
            Assert.AreEqual(resolved.damage, before - target.Current, "доставка бьёт не раскрытой записью тела");
        }

        [UnityTest]
        public IEnumerator Npc_NoRecord_NoBite_EvenIfDeliveryHangs()
        {
            var body = Npc(Species("Лось", OrganWith("Глотка", Maw)), expression: 1f); // Пасть без записи укуса
            var bite = body.gameObject.AddComponent<BiteAbility>();                  // так доставку вешает психика в Awake
            body.Refeed();
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;

            Assert.IsFalse(bite.Available, "нет записи органа — нет укуса");
            Assert.AreEqual(0f, bite.Range, 1e-5f, "недоступный укус не должен заманивать психику в зону атаки");
            bite.SetTarget(target);
            Assert.IsFalse(bite.TryUse(), "недоступный укус запустил замах");
        }

        [UnityTest]
        public IEnumerator Graft_GainsBite_Removal_LosesIt()
        {
            var rec = Bite();
            var human = Species("Человек", OrganWith("Рот", Maw));
            var wolf = Species("Волк", OrganWith("Пасть волка", Maw, rec));
            var body = Npc(human, expression: 1f, donors: new[] { wolf });
            yield return null;
            Assert.IsNull(body.Ability<BiteData>(), "у человечьего рта укуса нет");

            int slot = SlotIndex(body, Maw);
            Assert.IsTrue(body.Install(slot, VariantOf(body, slot, "Волк")), "не удалось привить волчью Пасть");
            var bite = body.GetComponent<BiteAbility>();
            Assert.IsNotNull(bite, "прививка Пасти не завела доставку укуса");
            Assert.IsTrue(bite.Available, "привитая Пасть не дала укус");
            Assert.AreEqual(rec.damage, body.Ability<BiteData>().damage, "донор на мощи 1 бьёт числом записи");

            Assert.IsTrue(body.Install(slot, VariantOf(body, slot, "Человек")), "не удалось вернуть родной рот");
            Assert.IsFalse(bite.Available, "вернули человечий рот — укус должен пропасть");
        }

        [UnityTest]
        public IEnumerator Player_Bite_SameRecord_SameCone()
        {
            var rec = Bite(damage: 14, bleed: 1, venom: 0);
            var body = Player(Species("Человек", OrganWith("Пасть волка", Maw, rec)));
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;
            Physics.SyncTransforms();

            var bite = body.GetComponent<PlayerBite>();
            Assert.IsNotNull(bite, "тело игрока не завело грань укуса по записи органа");
            Assert.IsTrue(bite.Available, "запись есть, а укус игрока недоступен");
            var resolved = body.Ability<BiteData>();

            int before = target.Current;
            Assert.IsTrue(bite.TryUse(), "укус игрока не сработал");
            Assert.AreEqual(resolved.damage, before - target.Current, "игрок бьёт не раскрытой записью тела");
            Assert.AreEqual(resolved.bleedStacks, BleedOf(target), "кровь игрока ≠ записи");

            yield return new WaitForSecondsRealtime(0.1f);                   // отпустить хитстоп
            yield return new WaitForSeconds(resolved.cooldown + 0.05f);       // дождаться перезарядки из записи
            Teleport(target, new Vector3(0f, 0f, -1.2f));                     // за спину — вне конуса
            int behind = target.Current;
            Assert.IsTrue(bite.TryUse(), "укус игрока не перезарядился по записи");
            Assert.AreEqual(behind, target.Current, "укус игрока задел цель за спиной — конус из записи не соблюдён");
        }
    }
}
