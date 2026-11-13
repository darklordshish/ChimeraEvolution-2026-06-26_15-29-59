using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>ПАКЕТ А письма Лесника 12.11 (<c>Docs/les/FEEDBACK-2026-11-12-les-opis-vne-lesa.md</c>, ветка
    /// <c>les/runtime-builder</c>): химеризуется ли тело, рождённое ФАБРИКОЙ. Подозрение: эволюция NPC
    /// (все виды в доноры, стартовое родство, <see cref="Metamorph"/>) стоит в <c>CreatureBody.Awake</c>, а фабрика
    /// задаёт шасси позже — через <c>Configure</c>, и на Awake шасси ещё null.
    /// Рядом сторож утечки: сцена арены из <c>EndlessLoopSmokeTests</c> не должна переживать тест.</summary>
    public class FactoryEvolutionTests
    {
        const int Start = 7;
        readonly List<Object> trash = new List<Object>();

        SpeciesSO MakeSpecies(string name)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 50;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            var organs = new List<Organ>();
            foreach (var s in new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" })
                organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 2 });
            organs.Add(new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true });
            so.organs = organs.ToArray();
            trash.Add(so);
            return so;
        }

        EvolutionConfig MakeEvo(SpeciesSO[] all)
        {
            var go = new GameObject("EvolutionConfig");
            trash.Add(go);
            var evo = go.AddComponent<EvolutionConfig>();
            void Set(string f, object v) =>
                typeof(EvolutionConfig).GetField(f, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(evo, v);
            Set("allSpecies", all); Set("evolveNpc", true); Set("chimerizeMultiplier", 1f); Set("startAffinity", Start);
            return evo;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator FactoryBody_IsEvolvable_LikePrefabBody()
        {
            var a = MakeSpecies("ТестА");
            var b = MakeSpecies("ТестБ");
            MakeEvo(new[] { a, b });

            var body = ChimeraFactory.Spawn(a, new SpeciesSO[0], new Vector3(500f, 0f, 500f), "~ФабрикаА");
            Assert.IsNotNull(body);
            trash.Add(body.gameObject);
            yield return null;

            Assert.IsNotNull(body.GetComponent<Metamorph>(),
                "у фабричного тела нет Metamorph: эволюция NPC инициализируется в Awake, где шасси ещё null (Configure зовётся позже)");
            Assert.AreEqual(Start, body.GetAffinity("ТестБ"),
                "стартовое родство чужому виду не выдано фабричному телу (EvolutionConfig.StartAffinity)");
            CollectionAssert.Contains(body.Donors, b, "ветка-донор из EvolutionConfig.AllSpecies не попала в доноры фабричного тела");
        }

        [UnityTest]
        public IEnumerator FactoryKiller_Chimerizes_OnKillingFactoryVictim()
        {
            var a = MakeSpecies("ТестА");
            var b = MakeSpecies("ТестБ");
            MakeEvo(new[] { a, b });

            var killer = ChimeraFactory.Spawn(a, new SpeciesSO[0], new Vector3(500f, 0f, 500f), "~УбийцаА");
            var victim = ChimeraFactory.Spawn(b, new SpeciesSO[0], new Vector3(520f, 0f, 500f), "~ЖертваБ");
            trash.Add(killer.gameObject); trash.Add(victim.gameObject);
            yield return null;

            killer.SetAffinity("ТестБ", 100); // шанс = 1 × множитель 1: выпадает всегда
            for (int i = 0; i < 12; i++) victim.TryChimerize(killer);

            bool grafted = false;
            for (int s = 0; s < killer.SlotCount; s++)
                if (killer.GetSlot(s).installed && killer.GetSlot(s).species == "ТестБ") grafted = true;
            Assert.IsTrue(grafted, "убийство не привело к графту: фабричный убийца не получил орган вида жертвы (TryChimerize)");
        }

        [UnityTest]
        public IEnumerator FactoryBody_InitEvolution_IsIdempotent()
        {
            var a = MakeSpecies("ТестА");
            var b = MakeSpecies("ТестБ");
            MakeEvo(new[] { a, b });
            var body = ChimeraFactory.Spawn(a, new SpeciesSO[0], new Vector3(500f, 0f, 500f), "~ИдемпотентА");
            trash.Add(body.gameObject);
            yield return null;
            body.Configure(a, new SpeciesSO[0], true); // повторная сборка не должна дублировать платформу
            yield return null;
            Assert.AreEqual(1, body.GetComponents<Metamorph>().Length);
        }

        [UnityTest]
        public IEnumerator FactorySnake_IsVisible_WithChain()
        {
            var snake = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Змея.asset");
            Assert.IsNotNull(snake);
            var body = ChimeraFactory.Spawn(snake, new SpeciesSO[0], new Vector3(500f, 0f, 500f), "~ФабричнаяЗмея");
            trash.Add(body.gameObject);
            yield return null;
            Assert.IsNotNull(body.GetComponent<SnakeBodyChain>(), "фабричная змея без цепи звеньев");
            Assert.IsNotNull(body.GetComponent<Health>());
            // «видима» = тело нарисовано (меши с габаритом). enabled не меряем: неподвижная змея прячется Camouflage — так задумано
            int visible = 0; string names = "";
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.name == "HeatGhost" || r.name == "Sphere") continue;
                names += r.name + ",";
                if (r.bounds.size.sqrMagnitude > 1e-6f) visible++;
            }
            Assert.Greater(visible, 1, "у фабричной змеи нет видимой геометрии: " + names);
        }

        // ── сторож утечки сцены арены (EndlessLoopSmokeTests грузит SampleScene в Single) ──
        [Test]
        public void ArenaScene_DoesNotLeakIntoOtherTests()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.AreNotEqual("SampleScene", SceneManager.GetSceneAt(i).name,
                    "SampleScene осталась загруженной после EndlessLoopSmokeTests: арена протекает в следующие тесты прогона");
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController>(), "в сцене чужой игрок (арена не выгружена)");
        }
    }
}
