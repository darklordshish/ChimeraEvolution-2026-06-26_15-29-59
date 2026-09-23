using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Пилот возврата: сытый волк с домом и флагом идёт домой (Homing + цель),
    /// без дома — бродит как раньше. Изолированная сцена (никого в радиусах),
    /// движение не меряем (NavMesh нет) — только решение пилота.
    /// Слайс s6 (ветка forest/s6-home-return).
    /// </summary>
    public class HomeHomingTests
    {
        readonly List<Object> trash = new List<Object>();

        SpeciesSO MakeSpecies(string name)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 60;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            so.organs = new[] { new Organ { organName = name + "-shard", slot = "Руки", cost = 2 } };
            trash.Add(so);
            return so;
        }

        CreatureBody SpawnWolf(Vector3 pos, List<SpeciesSO> pool)
        {
            var wolf = pool[0];
            var body = ChimeraFactory.Spawn(wolf, pool.ToArray(), pos, "Волк-lair", null, null);
            trash.Add(body.gameObject);
            return body;
        }

        LairSite Site(Vector3 pos)
        {
            var go = new GameObject("~Lair");
            go.transform.position = pos;
            trash.Add(go);
            return go.AddComponent<LairSite>();
        }

        /// <summary>
        /// Декой-игрок вдали (вне зрения/слыху/нюха): даёт психике непустой target,
        /// иначе Update висит в ранней ветке target==null и пилот недостижим.
        /// Полноценное тело фабрики + контроллер (свои InputAction, в простое молчит).
        /// </summary>
        void Decoy(Vector3 pos, List<SpeciesSO> pool)
        {
            var human = MakeSpecies("Человек");
            pool.Add(human);
            var body = ChimeraFactory.Spawn(human, pool.ToArray(), pos, "Декой", null, null);
            trash.Add(body.gameObject);
            body.gameObject.AddComponent<PlayerController>();
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator SatedWolf_HomesToOwnLair()
        {
            var pool = new List<SpeciesSO> { MakeSpecies("Волк") };
            Decoy(new Vector3(200f, 0.5f, 0f), pool);
            yield return null; // декой встал (Start пробежал, target у волка будет непустым)
            var site = Site(new Vector3(0f, 0f, 0f));
            var body = SpawnWolf(new Vector3(30f, 0.5f, 0f), pool);
            var psyche = body.GetComponent<WolfPsyche>();
            Assert.IsNotNull(psyche, "диспатч по доминанте дал волчью психику");
            body.home = site;
            psyche.returnHomeEnabled = true;
            body.GetComponent<Satiety>().Feed(100f);
            Assert.IsTrue(body.GetComponent<Satiety>().IsSated, "волк сыт");

            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(psyche.Homing, "пилот активен (идём домой)");
            Assert.AreEqual(site.transform.position, psyche.homeTarget, "цель — свой дом");
        }

        [UnityTest]
        public IEnumerator NoHome_NoHoming()
        {
            var pool = new List<SpeciesSO> { MakeSpecies("Волк") };
            Decoy(new Vector3(200f, 0.5f, 0f), pool);
            yield return null;
            var body = SpawnWolf(new Vector3(30f, 0.5f, 0f), pool);
            var psyche = body.GetComponent<WolfPsyche>();
            Assert.IsNotNull(psyche);
            psyche.returnHomeEnabled = true;
            body.GetComponent<Satiety>().Feed(100f);

            yield return null;
            yield return null;
            Assert.IsFalse(psyche.Homing, "без дома — брожение как раньше, пилота нет");
        }
    }
}
