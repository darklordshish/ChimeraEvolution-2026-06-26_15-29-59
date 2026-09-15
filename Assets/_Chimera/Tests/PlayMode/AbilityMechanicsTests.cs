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

        static VolleyData Volley() => new VolleyData
        {
            damagePerQuill = 4, slowPerQuill = 1, quills = 6, speed = 22f, hitRadius = 0.35f,
            bleedPerQuill = 0,  // кровь обнулена: шесть стаков перешли бы порог кровопотери и доливали урон, а тест меряет урон игл
            // разлёт почти нулевой: ёж стреляет у самой земли (вылет 0.5 м, игла толщиной 0.35), и игла с разлётом
            // вниз гаснет о пол — это честная механика низкого стрелка, но тест меряет счёт урона игл, а не пол
            spreadAngle = 0.01f,
            minRange = 6f, maxRange = 15f, windupTime = 0.05f, cooldown = 0.8f, blindAimError = 1.6f,
        };

        static RollData Roll() => new RollData { damage = 10, bleedStacks = 1, knockForce = 8f, radius = 1.2f };

        static CurlData Curl() => new CurlData
        {
            curlArmor = 0.6f, staminaDrain = 30f, rollSpeed = 9f, rollDrain = 40f, rollTurnSpeed = 90f, rollGravity = 20f,
        };

        static LimbStrikeData Limb() => new LimbStrikeData
        {
            damage = 18, knockForce = 4f, bleedStacks = 1, range = 1.8f, halfAngle = 60f, windupTime = 0.05f,
        };

        static KickData Kick() => new KickData { damage = 4, knockForce = 12f, reach = 1.8f, radius = 1.6f, cooldown = 1f };

        static LeapData Leap() => new LeapData
        {
            damage = 12, minRange = 5f, maxRange = 6.5f, speed = 13f, up = 5f, duration = 0.5f, hitRadius = 1.3f, windupTime = 0.05f,
        };

        static HowlData Howl() => new HowlData { radius = 14f, stunAt = 0.5f, stunDuration = 1f, fearMoraleHit = 2f, cooldown = 0.1f };

        static BellowData Bellow() => new BellowData { fearRadius = 10f, rallyRadius = 40f, fearMoraleHit = 2f, cooldown = 0.1f };

        static ScreamData Scream() => new ScreamData { cooldown = 0.1f, boostPerStack = 0.12f, maxBoost = 2f };

        // захват: сжатие быстрое, окно вырывания и дыхалка с запасом — тест меряет стадии, кап и срыв, а не гонку
        static ConstrictData Grip() => new ConstrictData
        {
            maxStage = 3, foreignMaxStage = 2, tightenRate = 20f, stage2At = 0.2f, stage3At = 0.4f,
            npcChokeDamage = 6, npcChokeInterval = 0.1f, escapeMin = 60f, escapeMax = 60f,
            breakRawThreshold = 10, grabBleedStacks = 0, holdDrain = 0.01f, wearInterval = 1.5f,
            grabRange = 2.2f, holdRange = 3.2f, cooldown = 0.2f, selfSlow1 = 0.8f, selfSlow2 = 0.6f, dragOffset = 1.1f,
        };

        struct Case
        {
            public string name, slot;
            public System.Func<AbilityData> record;
            public System.Type npc, player; // null — у этой стороны своего носителя нет
        }

        static readonly Case[] Cases =
        {
            new Case { name = "укус", slot = "Пасть", record = () => Bite(), npc = typeof(BiteAbility), player = typeof(PlayerBite) },
            new Case { name = "рога", slot = "Рога", record = Antler, npc = typeof(AntlerAbility), player = typeof(PlayerAntler) },
            new Case { name = "таран", slot = "Ноги", record = Charge, npc = typeof(ChargeAbility), player = typeof(PlayerCharge) },
            new Case { name = "залп", slot = "Игломёт", record = Volley, npc = typeof(QuillVolley), player = typeof(PlayerQuillVolley) },
            new Case { name = "перекат", slot = "Ноги ежа", record = Roll, npc = null, player = typeof(PlayerRoll) },
            new Case { name = "клубок", slot = "Клубок", record = Curl, npc = typeof(CurlDefense), player = typeof(CurlDefense) },
            new Case { name = "удар конечностью", slot = "Руки", record = Limb, npc = typeof(LimbStrikeAbility), player = typeof(PlayerAttack) },
            new Case { name = "пинок", slot = "Ноги человека", record = Kick, npc = null, player = typeof(PlayerKick) },
            new Case { name = "наскок", slot = "Ноги волка", record = Leap, npc = typeof(LeapAbility), player = null },
            new Case { name = "вой", slot = "Пасть волка", record = Howl, npc = null, player = typeof(PlayerHowl) },
            new Case { name = "рёв", slot = "Глотка", record = Bellow, npc = null, player = typeof(PlayerBellow) },
            new Case { name = "клич", slot = "Рот", record = Scream, npc = null, player = typeof(PlayerScream) },
            new Case { name = "захват", slot = "Хвост", record = Grip, npc = typeof(Constrict), player = typeof(PlayerConstrict) },
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
            Capsule(go, 2f);
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

        Health Dummy(Vector3 feet, float height = 2f)
        {
            var go = new GameObject("Манекен");
            go.transform.position = feet;
            Capsule(go, height);
            var h = go.AddComponent<Health>();
            NoDestroy(h);
            h.SetMaxHealth(1000);
            go.AddComponent<Knockback>();
            go.AddComponent<Stagger>();
            trash.Add(go);
            return h;
        }

        static void Capsule(GameObject go, float height)
        {
            var cc = go.AddComponent<CharacterController>();
            cc.height = height; cc.radius = 0.4f; cc.center = Vector3.up * height * 0.5f;
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

        static void Dash(CreatureBody player, float seconds) =>
            typeof(PlayerController).GetField("dashTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player.GetComponent<PlayerController>(), seconds); // рывок: перекат и таран — пассивные наездники на нём

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
        static int SlowOf(Health h) => h.TryGetComponent<Slow>(out var s) ? s.Stacks : 0;
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
                if (type == null) continue; // у этой стороны своего носителя нет
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
            foreach (var c in Cases)
                if (c.npc != null) hanging.Add((c, (IOrganAbility)body.gameObject.AddComponent(c.npc))); // так вешает психика в Awake
            body.Refeed();
            yield return null;

            foreach (var (c, carrier) in hanging)
            {
                Assert.IsFalse(carrier.Available, $"{c.name}: нет записи органа — приёма быть не должно");
                if (carrier is WindupAbility windup)
                {
                    windup.SetTarget(Dummy(new Vector3(0f, 0f, 1.2f)));
                    Assert.IsFalse(windup.TryUse(), $"{c.name}: недоступный приём запустил замах");
                }
                if (carrier is Constrict machine)
                    Assert.IsFalse(machine.Begin(Dummy(new Vector3(0f, 0f, 1.2f))), $"{c.name}: без записи машина взяла жертву");
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
            Assert.AreEqual(body.Ability<AntlerData>().damage, before - target.Current, "рога игрока бьют не раскрытой записью органа");
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
            Dash(body, 0.3f);
            yield return null;
            yield return null;
            Assert.AreEqual(body.Ability<ChargeData>().damage, before - target.Current, "таран игрока бьёт не раскрытой записью органа (разгон в записи обнулён)");
            Assert.IsTrue(Staggered(target), "таран игрока не сбил цель — сбив из записи не доехал до грани");
        }

        // ── ЗАЛП ─────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Npc_Volley_QuillsHitWithOrganRecord()
        {
            var rec = Volley();
            var body = Npc(Species("Ёж", OrganWith("Игломёт", "Игломёт", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 8f)); // в окне залпа записи [minRange, maxRange]
            yield return null;

            var volley = body.GetComponent<QuillVolley>();
            Assert.IsNotNull(volley, "тело не завело доставку залпа по записи органа");
            Assert.AreEqual(rec.minRange, volley.MinRange, 1e-5f, "психика читает окно залпа не из записи");
            Assert.AreEqual(rec.maxRange, volley.MaxRange, 1e-5f, "психика читает окно залпа не из записи");

            int before = target.Current;
            yield return Swing(volley, target);
            yield return new WaitForSeconds(8f / rec.speed + 0.3f); // долёт игл
            Assert.AreEqual(rec.quills * rec.damagePerQuill, before - target.Current, "урон пучка ≠ число игл × урон иглы из записи");
            Assert.Greater(SlowOf(target), 0, "иглы не замедлили цель — замедление из записи не доехало");
        }

        [UnityTest]
        public IEnumerator Player_Volley_SameRecord_OrganPowerAsModifier()
        {
            if (Camera.main != null) Assert.Ignore("в тестовой сцене есть камера: залп игрока целится по ней, а тест — по взгляду тела");
            var rec = Volley();
            var body = Player(Species("Человек", OrganWith("Игломёт", "Игломёт", rec)));
            var target = Dummy(new Vector3(0f, 0f, 8f), height: 5f); // игрок стреляет с груди — манекен повыше
            yield return null;
            Physics.SyncTransforms();

            var volley = body.GetComponent<PlayerQuillVolley>();
            Assert.IsNotNull(volley, "тело игрока не завело грань залпа по записи органа");
            var resolved = body.Ability<VolleyData>();
            float power = Mathf.Max(1f, resolved.power);                // мощь органа — модификатор полёта
            int perQuill = Mathf.Max(1, resolved.damagePerQuill);       // урон уже раскрыт законом органа

            int before = target.Current;
            Assert.IsTrue(volley.TryUse(), "залп игрока не сработал");
            yield return new WaitForSeconds(8f / (rec.speed * power) + 0.3f);
            Assert.AreEqual(rec.quills * perQuill, before - target.Current, "урон залпа игрока ≠ раскрытой записи органа");
        }

        // ── ПЕРЕКАТ И КЛУБОК ─────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Player_Roll_StingsOnlyWithThorns()
        {
            var rec = Roll();

            var bare = Player(Species("Человек", OrganWith("Ежиные ноги", "Ноги", rec)));
            var t1 = Dummy(new Vector3(0f, 0f, 1f));
            yield return null;
            Physics.SyncTransforms();
            var roll = bare.GetComponent<PlayerRoll>();
            Assert.IsNotNull(roll, "тело игрока не завело грань переката по записи органа");
            Assert.IsTrue(roll.Available, "запись ног есть, а перекат недоступен");
            Assert.IsFalse(roll.Active, "без игл в Шкуре перекат не должен колоться — кросс-слот сет");
            int b1 = t1.Current;
            Dash(bare, 0.3f);
            yield return null;
            yield return null;
            Assert.AreEqual(b1, t1.Current, "перекат без игл ранил цель");
            Object.Destroy(bare.gameObject);
            Object.Destroy(t1.gameObject);
            yield return null;

            var skin = new Organ { organName = "Шкура ежа", slot = "Шкура", cost = 1, thorns = true };
            var spiky = Player(Species("Человек", OrganWith("Ежиные ноги", "Ноги", rec), skin));
            var t2 = Dummy(new Vector3(0f, 0f, 1f));
            yield return null;
            Physics.SyncTransforms();
            var roll2 = spiky.GetComponent<PlayerRoll>();
            Assert.IsTrue(roll2.Active, "ноги + иглы — перекат должен колоться");
            int b2 = t2.Current;
            Dash(spiky, 0.3f);
            yield return null;
            yield return null;
            Assert.AreEqual(spiky.Ability<RollData>().damage, b2 - t2.Current, "перекат бьёт не раскрытой записью органа");
            Assert.AreEqual(rec.bleedStacks, BleedOf(t2), "кровь переката ≠ записи");
        }

        [UnityTest]
        public IEnumerator Npc_Curl_ArmorFromCurlRecord_RollHitsWithRollRecord()
        {
            var roll = Roll();
            var curlRec = Curl();
            var body = Npc(Species("Ёж", OrganWith("Ежиные ноги", "Ноги", roll, curlRec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 2f));
            yield return null;

            var curl = body.GetComponent<CurlDefense>();
            Assert.IsNotNull(curl, "тело не завело машину клубка по записи органа");
            Assert.IsTrue(curl.Available, "запись клубка есть, а клубок недоступен");

            var hp = body.GetComponent<Health>();
            float baseArmor = hp.DamageReduction;
            curl.Curl();
            Assert.AreEqual(Mathf.Max(baseArmor, curlRec.curlArmor), hp.DamageReduction, 1e-5f, "броня клубка не из записи");

            int before = target.Current;
            float end = Time.time + 2f;
            while (target.Current == before && Time.time < end)
            {
                curl.RollTick(target.transform.position - body.transform.position);
                yield return null;
            }
            Assert.AreEqual(roll.damage, before - target.Current, "прокат шара бьёт не записью переката того же органа");
            Assert.AreEqual(roll.bleedStacks, BleedOf(target), "кровь проката ≠ записи переката");
        }

        [UnityTest]
        public IEnumerator Curl_IsHomeOnly_NotOnForeignChassis()
        {
            var legs = OrganWith("Ежиные ноги", "Ноги", Roll(), Curl());
            legs.nativeChassis = "Ёж";
            var body = Npc(Species("Волк", legs), expression: 1f);
            yield return null;

            var curl = body.GetComponent<CurlDefense>();
            Assert.IsTrue(curl == null || !curl.Available, "клубок — домашний приём: на чужом шасси его быть не должно");
            Assert.IsNotNull(body.Ability<RollData>(), "перекат тех же ног открыт на любом шасси");
        }

        // ── УДАР КОНЕЧНОСТЬЮ, ПИНОК, НАСКОК ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Npc_LimbStrike_HitsWithOrganRecord()
        {
            var rec = Limb();
            var body = Npc(Species("Лось", OrganWith("Копыто", "Руки", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;

            var hoof = body.GetComponent<LimbStrikeAbility>();
            Assert.IsNotNull(hoof, "тело не завело удар конечностью по записи органа");
            Assert.AreEqual(rec.range, hoof.Range, 1e-5f, "психика читает досягаемость удара не из записи");

            int before = target.Current;
            yield return Swing(hoof, target);
            Assert.AreEqual(rec.damage, before - target.Current, "урон конечности ≠ записи органа (экспрессия 1)");
            Assert.AreEqual(rec.bleedStacks, BleedOf(target), "кровь от удара конечностью ≠ записи");
            Assert.IsTrue(Knocked(target), "удар конечностью не толкнул цель, хотя в записи есть толчок");
        }

        [UnityTest]
        public IEnumerator Player_Attack_SameRecord_SameCone_TempoFromHeart()
        {
            var rec = Limb();
            var heart = new Organ { organName = "Сердце", slot = "Сердце", cost = 1, atkCooldown = 0.6f };
            var body = Player(Species("Человек", OrganWith("Коготь", "Руки", rec), heart));
            var target = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;
            Physics.SyncTransforms();

            var attack = body.GetComponent<PlayerAttack>();
            Assert.IsTrue(attack.Available, "запись конечности есть, а удар игрока недоступен");
            var resolved = body.Ability<LimbStrikeData>();

            int before = target.Current;
            Assert.IsTrue(attack.TryUse(), "удар игрока не сработал");
            Assert.AreEqual(resolved.damage, before - target.Current, "удар игрока бьёт не раскрытой записью органа");
            Assert.IsFalse(attack.TryUse(), "темп атак от Сердца не соблюдён — удар повторился сразу");

            yield return new WaitForSecondsRealtime(0.15f);           // отпустить хитстоп
            yield return new WaitForSeconds(heart.atkCooldown + 0.05f); // дождаться темпа Сердца
            Teleport(target, new Vector3(0f, 0f, -1.2f));             // за спину — вне конуса
            int behind = target.Current;
            Assert.IsTrue(attack.TryUse(), "удар игрока не восстановился по темпу Сердца");
            Assert.AreEqual(behind, target.Current, "удар игрока задел цель за спиной — конус из записи не соблюдён");
        }

        [UnityTest]
        public IEnumerator Player_Kick_WithOrganRecord()
        {
            var rec = Kick();
            var body = Player(Species("Человек", OrganWith("Ноги", "Ноги", rec)));
            var target = Dummy(new Vector3(0f, 0f, 1.5f));
            yield return null;
            Physics.SyncTransforms();

            var kick = body.GetComponent<PlayerKick>();
            Assert.IsNotNull(kick, "тело игрока не завело грань пинка по записи органа");
            int before = target.Current;
            Assert.IsTrue(kick.TryUse(), "пинок не сработал");
            Assert.AreEqual(body.Ability<KickData>().damage, before - target.Current, "пинок бьёт не раскрытой записью органа");
            Assert.IsTrue(Knocked(target), "пинок не оттолкнул цель, хотя в записи есть отлёт");
        }

        [UnityTest]
        public IEnumerator Npc_Leap_LandsWithOrganRecord()
        {
            var rec = Leap();
            var body = Npc(Species("Волк", OrganWith("Волчьи ноги", "Ноги", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, 6f)); // в окне наскока записи [minRange, maxRange]
            yield return null;

            var leap = body.GetComponent<LeapAbility>();
            Assert.IsNotNull(leap, "тело не завело наскок по записи органа");
            Assert.AreEqual(rec.minRange, leap.MinRange, 1e-5f, "психика читает окно наскока не из записи");
            Assert.AreEqual(rec.maxRange, leap.MaxRange, 1e-5f, "психика читает окно наскока не из записи");

            int before = target.Current;
            yield return Swing(leap, target);
            Assert.AreEqual(rec.damage, before - target.Current, "урон наскока ≠ записи органа");
        }

        // ── ГОЛОС ────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Player_Howl_StunsOnlyWhenOrganPowerReachesThreshold()
        {
            var rec = Howl(); // порог стана низкий: любая мощь органа до него дорастает
            var body = Player(Species("Человек", OrganWith("Пасть волка", "Пасть", rec)));
            var target = Dummy(new Vector3(0f, 0f, 2f)); // ближнее кольцо: половина радиуса
            yield return null;
            Physics.SyncTransforms();

            var howl = body.GetComponent<PlayerHowl>();
            Assert.IsNotNull(howl, "тело игрока не завело грань воя по записи органа");
            Assert.IsTrue(body.Ability<HowlData>().Stuns, "мощь органа выше порога записи — стан должен быть открыт");
            Assert.IsTrue(howl.TryUse(), "вой не сработал");
            Assert.IsTrue(target.GetComponent<Stagger>().IsStunned, "вой не оглушил ближнюю чужую цель");

            rec.stunAt = 100f; // порог недостижим
            body.Refeed();
            yield return new WaitForSeconds(rec.stunDuration + rec.cooldown + 0.1f); // прошлый стан спал, перезарядка прошла
            Assert.IsFalse(body.Ability<HowlData>().Stuns, "порог выше мощи — стан должен быть закрыт");
            Assert.IsTrue(howl.TryUse(), "вой не перезарядился по записи");
            Assert.IsFalse(target.GetComponent<Stagger>().IsStunned, "вой оглушил, хотя мощь органа не доросла до порога");
        }

        [UnityTest]
        public IEnumerator Player_Bellow_FrightensStrangersInFearRadius()
        {
            var rec = Bellow();
            var body = Player(Species("Человек", OrganWith("Глотка", "Пасть", rec)));
            var near = Dummy(new Vector3(0f, 0f, rec.fearRadius * 0.5f));
            var far = Dummy(new Vector3(0f, 0f, rec.fearRadius + 5f));
            var nearMorale = near.gameObject.AddComponent<Morale>();
            var farMorale = far.gameObject.AddComponent<Morale>();
            yield return null;
            Physics.SyncTransforms();

            var bellow = body.GetComponent<PlayerBellow>();
            Assert.IsNotNull(bellow, "тело игрока не завело грань рёва по записи органа");
            float nearBefore = nearMorale.Current, farBefore = farMorale.Current;
            Assert.IsTrue(bellow.TryUse(), "рёв не сработал");
            Assert.AreEqual(nearBefore - rec.fearMoraleHit, nearMorale.Current, 1e-4f, "рёв давит дух чужих в радиусе ужаса числом записи");
            Assert.AreEqual(farBefore, farMorale.Current, 1e-4f, "рёв задел дух за радиусом ужаса записи");
        }

        [UnityTest]
        public IEnumerator Player_Scream_BleedsSelf_AndEnrages()
        {
            var rec = Scream();
            var body = Player(Species("Человек", OrganWith("Рот", "Пасть", rec)));
            var rage = body.gameObject.AddComponent<Rage>();
            yield return null;

            var scream = body.GetComponent<PlayerScream>();
            Assert.IsNotNull(scream, "тело игрока не завело грань клича по записи органа");
            Assert.IsTrue(scream.TryUse(), "клич не сработал");
            Assert.AreEqual(1, BleedOf(body.GetComponent<Health>()), "клич не пустил кровь кричащему");
            Assert.IsTrue(rage.IsEnraged, "клич не ввёл в ярость");
        }

        // ── ЗАХВАТ ───────────────────────────────────────────────────────────────────────

        // у NPC тикает машину психика-драйвер, каждый кадр, пока держит. Тест делает то же
        static IEnumerator Hold(Constrict machine, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
                if (machine.Tick() != GrabTick.Holding) yield break;
            }
        }

        [UnityTest]
        public IEnumerator Npc_Constrict_StagesAndChokeFromRecord_CapOnForeignChassis()
        {
            var rec = Grip();
            var tail = OrganWith("Хвост", "Хвост", rec);
            tail.nativeChassis = "Удав";
            var holder = Npc(Species("Удав", tail), expression: 1f);
            var victim = Dummy(new Vector3(0f, 0f, 1.2f));
            yield return null;

            var machine = holder.GetComponent<Constrict>();
            Assert.IsNotNull(machine, "тело не завело машину захвата по записи органа");
            Assert.AreEqual(rec.maxStage, holder.Ability<ConstrictData>().maxStage, "дома кап стадии — родная сила органа");
            Assert.IsTrue(machine.Begin(victim), "машина не взяла жертву");
            int before = victim.Current;
            yield return Hold(machine, 1f);
            Assert.AreEqual(rec.maxStage, machine.Stage, "сжатие по записи не дошло до партера");
            int choked = before - victim.Current;
            Assert.Greater(choked, 0, "на партере нет удушения");
            Assert.AreEqual(0, choked % rec.npcChokeDamage, "удушение бьёт не числом записи");
            machine.End();

            // тот же орган на чужом шасси — кап стадии из записи
            var guestTail = OrganWith("Хвост", "Хвост", rec);
            guestTail.nativeChassis = "Удав";
            var guestBody = Npc(Species("Человек", guestTail), expression: 1f);
            var victim2 = Dummy(new Vector3(3f, 0f, 1.2f));
            yield return null;
            var guest = guestBody.GetComponent<Constrict>();
            Assert.AreEqual(rec.foreignMaxStage, guestBody.Ability<ConstrictData>().maxStage, "в гостях кап — foreignMaxStage записи");
            Assert.IsTrue(guest.Begin(victim2), "машина гостя не взяла жертву");
            yield return Hold(guest, 1f);
            Assert.AreEqual(rec.foreignMaxStage, guest.Stage, "в гостях захват дожал выше капа записи");
        }

        [UnityTest]
        public IEnumerator Npc_Constrict_RescuerBreaksFromRecordThreshold_BleedOnGrab()
        {
            var rec = Grip();
            rec.stage2At = 100f; rec.stage3At = 200f; // держим на ст.1: там удар спасателя срывает хват разом
            rec.grabBleedStacks = 2;
            var holder = Npc(Species("Хищник", OrganWith("Пасть", "Пасть", rec)), expression: 1f);
            var victim = Dummy(new Vector3(0f, 0f, 1.2f));
            var rescuer = Dummy(new Vector3(0f, 0f, -1.2f));
            yield return null;

            var machine = holder.GetComponent<Constrict>();
            Assert.IsTrue(machine.Begin(victim), "машина не взяла жертву");
            Assert.AreEqual(rec.grabBleedStacks, BleedOf(victim), "кровь на входе в хват — не числом записи");

            var own = holder.GetComponent<Health>();
            yield return null;
            own.LastAttacker = rescuer;
            own.TakeDamage(rec.breakRawThreshold - 1, true);
            Assert.AreEqual(GrabTick.Holding, machine.Tick(), "удар слабее порога записи сорвал хват");

            yield return null;
            own.LastAttacker = rescuer;
            own.TakeDamage(rec.breakRawThreshold, true);
            Assert.AreEqual(GrabTick.Broken, machine.Tick(), "удар в порог записи не сорвал хват на ст.1");
        }

        [UnityTest]
        public IEnumerator Player_Constrict_SameRecord_GrabRangeSelfSlowCooldown()
        {
            var rec = Grip();
            var body = Player(Species("Человек", OrganWith("Хвост", "Хвост", rec)));
            var target = Dummy(new Vector3(0f, 0f, rec.grabRange * 0.5f));
            yield return null;
            Physics.SyncTransforms();

            var grip = body.GetComponent<PlayerConstrict>();
            Assert.IsNotNull(grip, "тело игрока не завело грань захвата по записи органа");
            Assert.IsTrue(grip.TryUse(), "захват не взял цель в досягаемости записи");
            Assert.AreEqual(1, grip.Stage, "захват начинается со ст.1");
            Assert.AreEqual(rec.selfSlow1, grip.SelfSlow, 1e-5f, "своё замедление на ст.1 — не из записи");
            yield return new WaitForSeconds(0.3f);
            Assert.GreaterOrEqual(grip.Stage, 2, "машина игрока не дожала по записи");
            Assert.AreEqual(rec.selfSlow2, grip.SelfSlow, 1e-5f, "своё замедление с ношей — не из записи");

            Assert.IsTrue(grip.TryUse(), "повторное F не отпустило");
            Assert.IsFalse(grip.Holding, "отпустили, а хват держится");
            Teleport(target, new Vector3(0f, 0f, rec.grabRange * 0.5f));
            Assert.IsFalse(grip.TryUse(), "захват сработал в перезарядке записи");
            yield return new WaitForSeconds(rec.cooldown + 0.05f);

            Teleport(target, new Vector3(0f, 0f, rec.grabRange + 1.5f));
            Assert.IsFalse(grip.TryUse(), "захват взял цель за досягаемостью записи");
            Teleport(target, new Vector3(0f, 0f, rec.grabRange * 0.5f));
            Assert.IsTrue(grip.TryUse(), "перезарядка записи прошла, а захват не берёт");
        }

        // ── АРБИТР АРСЕНАЛА ХИМЕРЫ-АЛЬФЫ ────────────────────────────────────────────────

        static IEnumerator Until(System.Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Alpha_WithLegsRecord_ChargesFromDistance()
        {
            var rec = Charge();
            var body = Npc(Species("Человек", OrganWith("Лосиные ноги", "Ноги", rec)), expression: 1f);
            Dummy(new Vector3(0f, 0f, (rec.minRange + rec.maxRange) * 0.5f));
            yield return null;

            var charge = body.GetComponent<ChargeAbility>();
            Assert.IsNotNull(charge, "тело не завело таран по записи ног");
            body.gameObject.AddComponent<ChimeraAlphaPsyche>();
            bool charged = false;
            yield return Until(() => charged |= charge.Busy, 3f);
            Assert.IsTrue(charged, "альфа с записью ног не таранила цель в окне разбега");
        }

        [UnityTest]
        public IEnumerator Alpha_WithoutMaw_StrikesWithLimbRecord()
        {
            var rec = Limb();
            var body = Npc(Species("Человек", OrganWith("Кисть", "Руки", rec)), expression: 1f);
            var target = Dummy(new Vector3(0f, 0f, rec.range * 0.6f));
            yield return null;

            Assert.IsNull(body.GetComponent<BiteAbility>(), "без Пасти укуса быть не должно");
            body.gameObject.AddComponent<ChimeraAlphaPsyche>();
            int before = target.Current;
            yield return Until(() => target.Current < before, 3f);
            Assert.AreEqual(rec.damage, before - target.Current, "альфа без Пасти не ударила конечностью по записи");
        }
    }
}
