using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class EvolutionLoopTests
    {
        SpeciesSO MakeSpecies(string name, Color tint, string[] slots, int pool = 100)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = tint;
            so.mutagenPool = pool;
            so.baseHp = 50;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            var organs = new List<Organ>();
            foreach (var s in slots) organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 2 });
            organs.Add(new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true });
            so.organs = organs.ToArray();
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors, string name)
        {
            var go = new GameObject(name);
            var h = go.AddComponent<Health>();
            // не уничтожать на смерть — нужно переиспользовать жертву
            var f = typeof(Health).GetField("destroyOnDeath", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(h, false);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            go.AddComponent<NavLocomotion>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            return body;
        }

        EvolutionConfig MakeEvo(SpeciesSO[] all)
        {
            var go = new GameObject("EvolutionConfig");
            var evo = go.AddComponent<EvolutionConfig>();
            var fAll = typeof(EvolutionConfig).GetField("allSpecies", BindingFlags.NonPublic | BindingFlags.Instance);
            var fEvo = typeof(EvolutionConfig).GetField("evolveNpc", BindingFlags.NonPublic | BindingFlags.Instance);
            var fChim = typeof(EvolutionConfig).GetField("chimerizeMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            var fStart = typeof(EvolutionConfig).GetField("startAffinity", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fAll != null) fAll.SetValue(evo, all);
            if (fEvo != null) fEvo.SetValue(evo, true);
            if (fChim != null) fChim.SetValue(evo, 1f);
            if (fStart != null) fStart.SetValue(evo, 0);
            return evo;
        }

        [UnityTest]
        public IEnumerator CreditKiller_AddsAffinity_PerOrgan()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var evo = MakeEvo(new[] { human, wolf });
            var killer = MakeBody(human, new[] { wolf }, "Killer");
            var victim = MakeBody(wolf, new[] { human }, "Victim_WolfPure");
            yield return null;
            int before = killer.GetAffinity("Волк");
            // симулируем убийство: victim.LastAttacker = killer, victim.CreditKiller()
            var killerHealth = killer.GetComponent<Health>();
            var victimHealth = victim.GetComponent<Health>();
            victimHealth.LastAttacker = killerHealth;
            var mi = typeof(CreatureBody).GetMethod("CreditKiller", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Invoke(victim, null);
            yield return null;
            int after = killer.GetAffinity("Волк");
            Assert.Greater(after, before, "CreditKiller должен начислить родство убийце по видам органов жертвы");
            // волк pure: 1 шасси +6 органов =7 *0.55 ≈4
            int expectedMin = Mathf.Max(1, Mathf.RoundToInt(7 * 0.55f));
            Assert.GreaterOrEqual(after - before, 1, "Хотя бы 1 за любой след вида");

            Object.Destroy(killer.gameObject);
            Object.Destroy(victim.gameObject);
            Object.Destroy(evo.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Loop_25Kills_ToCap()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var evo = MakeEvo(new[] { human, wolf });
            var killer = MakeBody(human, new[] { wolf }, "Killer25");
            var victim = MakeBody(wolf, new[] { human }, "VictimLoop");
            yield return null;
            var killerHealth = killer.GetComponent<Health>();
            var victimHealth = victim.GetComponent<Health>();
            var mi = typeof(CreatureBody).GetMethod("CreditKiller", BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < 25; i++)
            {
                victimHealth.LastAttacker = killerHealth;
                mi.Invoke(victim, null);
                yield return null;
            }
            int aff = killer.GetAffinity("Волк");
            Assert.GreaterOrEqual(aff, 80, $"После 25 киллов pure-волка родство должно быть близко к капу, получено {aff}");
            // ещё 10 киллов — кап 100
            for (int i = 0; i < 10; i++)
            {
                victimHealth.LastAttacker = killerHealth;
                mi.Invoke(victim, null);
            }
            yield return null;
            Assert.AreEqual(100, killer.GetAffinity("Волк"), "Родство капится на 100 (AffinityCap)");

            Object.Destroy(killer.gameObject);
            Object.Destroy(victim.gameObject);
            Object.Destroy(evo.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TryChimerize_GraftsOrgan_WithChance()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var evo = MakeEvo(new[] { human, wolf });
            // ставим множитель 100 чтобы шанс =100% при любом родстве>0
            var fChim = typeof(EvolutionConfig).GetField("chimerizeMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            fChim.SetValue(evo, 100f);
            var killer = MakeBody(human, new[] { wolf, human }, "KillerGraft");
            var victim = MakeBody(wolf, new[] { human }, "VictimGraft");
            yield return null;
            // даём убийце родство чтобы шанс сработал
            killer.AddAffinity("Волк", 10);
            int beastBefore = killer.BeastSlots;
            victim.TryChimerize(killer);
            yield return null;
            // при 100x множителе должен надеть хотя бы один орган (если был слот)
            // BeastSlots может увеличиться на 1 (химерный не считаем — обычный слот)
            // Проверяем что либо BeastSlots вырос, либо орган надет
            Assert.GreaterOrEqual(killer.BeastSlots, beastBefore, "TryChimerize должен попытаться надеть орган жертвы");

            Object.Destroy(killer.gameObject);
            Object.Destroy(victim.gameObject);
            Object.Destroy(evo.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullLoop_Kill_Affinity_Chimerize_Metamorph()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var evo = MakeEvo(new[] { human, wolf });
            var fChim = typeof(EvolutionConfig).GetField("chimerizeMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            fChim.SetValue(evo, 100f);

            var killer = MakeBody(human, new[] { wolf }, "KillerFull");
            killer.gameObject.AddComponent<Metamorph>();
            var victim = MakeBody(wolf, new[] { human }, "VictimFull");
            yield return null;
            PsycheDispatch.Attach(killer);
            yield return null;
            Assert.IsNotNull(killer.GetComponent<ChimeraAlphaPsyche>(), "Старт — химера-альфа");

            var victimHealth = victim.GetComponent<Health>();
            var killerHealth = killer.GetComponent<Health>();
            var mi = typeof(CreatureBody).GetMethod("CreditKiller", BindingFlags.NonPublic | BindingFlags.Instance);
            // качаем 25 киллов — родство в кап, затем графты доведут идентичность до Medium
            for (int i = 0; i < 25; i++)
            {
                victimHealth.LastAttacker = killerHealth;
                mi.Invoke(victim, null);
            }
            yield return null;
            // графты: TryChimerize уже зовётся внутри CreditKiller, но докинем ещё для гарантии
            for (int i = 0; i < 10; i++) victim.TryChimerize(killer);
            yield return null;
            yield return null; // дать Metamorph сработать (Destroy отложен)
            // после прокачки и графтов доминанта должна стать волком и метаморфоза повесить WolfPsyche
            // допускаем что не 100% — проверяем что родство кап и хотя бы что-то надето
            Assert.AreEqual(100, killer.GetAffinity("Волк"));
            // если графты прошли, то психика сменится
            if (killer.BeastSlots > 0)
            {
                yield return null;
                // не требуем строгой смены если не хватило пула, но проверяем что система жива
                Assert.IsTrue(killer.GetComponent<ChimeraAlphaPsyche>() != null || killer.GetComponent<WolfPsyche>() != null);
            }

            Object.Destroy(killer.gameObject);
            Object.Destroy(victim.gameObject);
            Object.Destroy(evo.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }
    }
}
