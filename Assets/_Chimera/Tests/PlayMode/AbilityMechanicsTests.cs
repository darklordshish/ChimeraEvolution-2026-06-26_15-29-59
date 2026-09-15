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
    /// кормит и NPC, и игрока. Ожидания читаются из тестовых записей и из раскрытой записи тела — поворот ручки в игре
    /// тест не краснит, разрыв проводки краснит. Индивидуальность выключена: средняя особь, урон не плавает.
    ///
    /// ТАБЛИЦА ПРИЁМОВ (<see cref="Cases"/>): переехавший приём добавляет строку, и общие проверки (прививка даёт приём,
    /// возврат родного органа снимает; нет записи — носитель недоступен) идут по всем приёмам сразу. Удары каждого
    /// приёма — отдельными тестами ниже.
    /// </summary>
    public class AbilityMechanicsTests
    {
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
            Time.timeScale = 1f; // хитстоп удара игрока роняет время на миг — не оставить его соседним тестам
        }

        // ── ЗАПИСИ (числа — тестовые, не баланс; замахи короткие: тест проверяет проводку, а не телеграф) ───────

        static BiteData Bite(int damage = 14, int bleed = 1, int venom = 1) => new BiteData
        {
            damage = damage, bleedStacks = bleed, venomStacks = venom, regenDebuff = 0.5f, regenDebuffTime = 3f,
            range = 2f, halfAngle = 55f, windupTime = 0.05f, cooldown = 0.7f,
        };

        static AntlerData Antler() => new AntlerData
        {
            damage = 12, knockForce = 9f, bleedStacks = 2, range = 2.5f, halfAngle = 50f, windupTime = 0.05f, cooldown = 1.2f,
        };

        static ChargeData Charge() => new ChargeData
        {
            damage = 22, knockForce = 12f, lightChargeMult = 0.25f, staggerTime = 0.5f, hitRadius = 1.8f,
            damagePerMeter = 0f, // разгон обнулён: пройденные метры зависят от кадров, а тест проверяет проводку
            minRange = 4f, maxRange = 18f, chargeSpeed = 35f, duration = 0.4f, windupTime = 0.05f,
            stompRadius = 4f, stompStagger = 0.4f, stompForce = 13f, plowForce = 6f, plowRadius = 1.6f,
        };

        struct Case
        {
            public string name, slot;
            public System.Func<AbilityData> record;
            public System.Type npc, player;
        }

        static readonly Case[] Cases =
        {
            new Case { name = "укус", slot = "Пасть", record = () => Bite(), npc = typeof(BiteAbility), player = typeof(PlayerBite) },
            new Case { name = "рога", slot = "Рога", record = Antler, npc = typeof(AntlerAbility), player = typeof(PlayerAntler) },
            new Case { name = "таран", slot = "Ноги", record = Charge, npc = typeof(ChargeAbility), player = typeof(PlayerCharge) },
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

        // человек без приёмов во всех слотах таблицы и зверь с записями во всех — пара для проверок прививки
        (SpeciesSO human, SpeciesSO beast) HumanAndBeast()
        {
            var humanOrgans = new List<Organ>();
            var beastOrgans = new List<Organ>();
            foreach (var c in Cases)
            {
                humanOrgans.Add(OrganWith("человечий " + c.slot, c.slot));
                beastOrgans.Add(OrganWith("звериный " + c.slot, c.slot, c.record()));
            }
            return (Species("Человек", humanOrgans.ToArray()), Species("Зверь", beastOrgans.ToArray()));
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

        CreatureBody Player(SpeciesSO chassis, SpeciesSO[] donors = null)
        {
            var go = new GameObject("Игрок");
            go.SetActive(false);                              // собрать до Awake: контроллер требует капсулу и драйвер ввода
            go.transform.position = new Vector3(0f, 1f, 0f);  // корень игрока — центр капсулы
            go.AddComponent<CharacterController>();
            NoDestroy(go.AddComponent<Health>());
            go.AddComponent<PlayerController>();
            var body = go.AddComponent<CreatureBody>();
            go.SetActive(true);
            body.Configure(chassis, donors ?? new SpeciesSO[0]);
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
        static bool Knocked(Health h) => h.GetComponent<Knockback>().IsActive;
        static bool Staggered(Health h) => h.GetComponent<Stagger>().IsStaggered;

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

        static IOrganAbility CarrierOf(CreatureBody body, System.Type type) => body.GetComponent(type) as IOrganAbility;

        // ── ОБЩЕЕ ПО ТАБЛИЦЕ ─────────────────────────────────────────────────────────────

        IEnumerator GraftGainsRevertLoses(bool player)
        {
            var (human, beast) = HumanAndBeast();
            var body = player ? Player(human, new[] { beast }) : Npc(human, expression: 1f, donors: new[] { beast });
            yield return null;

            foreach (var c in Cases)
            {
                var type = player ? c.player : c.npc;
                var carrier = CarrierOf(body, type);
                Assert.IsTrue(carrier == null || !carrier.Available, $"{c.name}: у человечьего органа приёма нет, а носитель доступен");

                int slot = SlotIndex(body, c.slot);
                Assert.IsTrue(body.Install(slot, VariantOf(body, slot, "Зверь")), $"{c.name}: не удалось привить звериный орган");
                carrier = CarrierOf(body, type);
                Assert.IsNotNull(carrier, $"{c.name}: прививка органа не завела носителя {type.Name}");
                Assert.IsTrue(carrier.Available, $"{c.name}: привитый орган не дал приём");

                Assert.IsTrue(body.Install(slot, VariantOf(body, slot, "Человек")), $"{c.name}: не удалось вернуть родной орган");
                Assert.IsFalse(carrier.Available, $"{c.name}: вернули родной орган — приём должен пропасть");
            }
        }

        [UnityTest] public IEnumerator EveryAbility_Npc_GraftGains_RevertLoses() => GraftGainsRevertLoses(player: false);
        [UnityTest] public IEnumerator EveryAbility_Player_GraftGains_RevertLoses() => GraftGainsRevertLoses(player: true);

        [UnityTest]
        public IEnumerator EveryAbility_Npc_NoRecord_CarrierUnavailable()
        {
            var (human, _) = HumanAndBeast();
            var body = Npc(human, expression: 1f);
            var hanging = new List<(Case c, IOrganAbility carrier)>();
            foreach (var c in Cases) hanging.Add((c, (IOrganAbility)body.gameObject.AddComponent(c.npc))); // так вешает психика в Awake
            body.Refeed();
            yield return null;

            foreach (var (c, carrier) in hanging)
            {
                Assert.IsFalse(carrier.Available, $"{c.name}: нет записи органа — приёма быть не должно");
                var windup = (WindupAbility)carrier;
                windup.SetTarget(Dummy(new Vector3(0f, 0f, 1.2f)));
                Assert.IsFalse(windup.TryUse(), $"{c.name}: недоступный приём запустил замах");
            }
        }

        // ── УКУС ─────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Npc_Bite_HitsWithOrganRecord()
        {
            var rec = Bite();
            var body = Npc(Species("Волк", OrganWith("Пасть волка", "Пасть", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;

            var bite = body.GetComponent<BiteAbility>();
            Assert.IsNotNull(bite, "тело не завело доставку укуса по записи органа");
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
            var body = Npc(Species("Волк", OrganWith("Пасть волка", "Пасть", rec)), expression: 0.5f);
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
        public IEnumerator Player_Bite_SameRecord_SameCone()
        {
            var rec = Bite(damage: 14, bleed: 1, venom: 0);
            var body = Player(Species("Человек", OrganWith("Пасть волка", "Пасть", rec)));
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;
            Physics.SyncTransforms();

            var bite = body.GetComponent<PlayerBite>();
            Assert.IsNotNull(bite, "тело игрока не завело грань укуса по записи органа");
            var resolved = body.Ability<BiteData>();

            int before = target.Current;
            Assert.IsTrue(bite.TryUse(), "укус игрока не сработал");
            Assert.AreEqual(resolved.damage, before - target.Current, "игрок бьёт не раскрытой записью тела");
            Assert.AreEqual(resolved.bleedStacks, BleedOf(target), "кровь игрока ≠ записи");

            yield return new WaitForSecondsRealtime(0.1f);                // отпустить хитстоп
            yield return new WaitForSeconds(resolved.cooldown + 0.05f);    // дождаться перезарядки из записи
            Teleport(target, new Vector3(0f, 0f, -1.2f));                  // за спину — вне конуса
            int behind = target.Current;
            Assert.IsTrue(bite.TryUse(), "укус игрока не перезарядился по записи");
            Assert.AreEqual(behind, target.Current, "укус игрока задел цель за спиной — конус из записи не соблюдён");
        }

        // ── РОГА ─────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Npc_Antler_HitsWithOrganRecord()
        {
            var rec = Antler();
            var body = Npc(Species("Лось", OrganWith("Рога", "Рога", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 1.5f));
            yield return null;

            var antler = body.GetComponent<AntlerAbility>();
            Assert.IsNotNull(antler, "тело не завело доставку рогов по записи органа");
            Assert.AreEqual(rec.range, antler.Range, 1e-5f, "психика читает досягаемость рогов не из записи");

            int before = target.Current;
            yield return Swing(antler, target);
            Assert.AreEqual(rec.damage, before - target.Current, "урон рогов ≠ записи органа");
            Assert.AreEqual(rec.bleedStacks, BleedOf(target), "кровь от рогов ≠ записи органа");
            Assert.IsTrue(Knocked(target), "рога не отбросили цель, хотя в записи есть отлёт");
        }

        [UnityTest]
        public IEnumerator Player_Antler_SameRecord_SameCone()
        {
            var rec = Antler();
            var body = Player(Species("Человек", OrganWith("Рога", "Рога", rec)));
            var target = Dummy(new Vector3(0f, 0f, 1.5f));
            yield return null;
            Physics.SyncTransforms();

            var antler = body.GetComponent<PlayerAntler>();
            Assert.IsNotNull(antler, "тело игрока не завело грань рогов по записи органа");

            int before = target.Current;
            Assert.IsTrue(antler.TryUse(), "удар рогами игрока не сработал");
            Assert.AreEqual(rec.damage, before - target.Current, "рога игрока бьют не записью органа");
            Assert.AreEqual(rec.bleedStacks, BleedOf(target), "кровь от рогов игрока ≠ записи");
            Assert.IsTrue(Knocked(target), "рога игрока не отбросили цель");

            yield return new WaitForSeconds(rec.cooldown + 0.05f);
            Teleport(target, new Vector3(0f, 0f, -1.5f));
            int behind = target.Current;
            Assert.IsTrue(antler.TryUse(), "рога игрока не перезарядились по записи");
            Assert.AreEqual(behind, target.Current, "рога игрока задели цель за спиной — конус из записи не соблюдён");
        }

        // ── ТАРАН ────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Npc_Charge_HitsWithOrganRecord()
        {
            var rec = Charge();
            var body = Npc(Species("Лось", OrganWith("Лосиные ноги", "Ноги", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 6f)); // в окне разбега записи [minRange, maxRange]
            yield return null;

            var charge = body.GetComponent<ChargeAbility>();
            Assert.IsNotNull(charge, "тело не завело доставку тарана по записи органа");
            Assert.AreEqual(rec.minRange, charge.MinRange, 1e-5f, "психика читает окно тарана не из записи");
            Assert.AreEqual(rec.maxRange, charge.MaxRange, 1e-5f, "психика читает окно тарана не из записи");

            int before = target.Current;
            yield return Swing(charge, target);
            Assert.AreEqual(rec.damage, before - target.Current, "урон тарана ≠ записи органа (разгон в записи обнулён)");
            Assert.IsTrue(Staggered(target), "таран не сбил цель, хотя в записи есть сбив");
        }

        [UnityTest]
        public IEnumerator Player_Charge_RidesDash_WithOrganRecord()
        {
            var rec = Charge();
            var body = Player(Species("Человек", OrganWith("Лосиные ноги", "Ноги", rec)));
            var target = Dummy(new Vector3(0f, 0f, 1f)); // внутри радиуса удара записи
            yield return null;
            Physics.SyncTransforms();

            var charge = body.GetComponent<PlayerCharge>();
            Assert.IsNotNull(charge, "тело игрока не завело грань тарана по записи органа");
            Assert.IsTrue(charge.Available, "запись есть, а таран игрока недоступен");

            int before = target.Current;
            // рывок: таран — пассивный наездник на нём, отдельной кнопки нет
            typeof(PlayerController).GetField("dashTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(body.GetComponent<PlayerController>(), 0.3f);
            yield return null;
            yield return null;
            Assert.AreEqual(rec.damage, before - target.Current, "таран игрока бьёт не записью органа (разгон в записи обнулён)");
            Assert.IsTrue(Staggered(target), "таран игрока не сбил цель — сбив из записи не доехал до грани");
        }
    }
}
