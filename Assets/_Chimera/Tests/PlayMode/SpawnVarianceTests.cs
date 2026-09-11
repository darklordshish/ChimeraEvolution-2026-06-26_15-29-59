using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>РАЗБРОС ОСОБИ — ОТ ТЕЛА, А НЕ ОТ ПСИХИКИ (геймдизайнер, 11.09: «такой же для всех видов»). Раньше
    /// `SpawnVariance` тянули за собой волчья, лосиная и змеиная психики, а ёж из диспатча и истинные химеры
    /// оставались клонами. Сторожим два обещания: разброс получает любое NPC-тело — даже с психикой, которая о нём
    /// не знает (химера-альфа), — и он ровно ОДИН: видовая психика своего больше не вешает.</summary>
    public class SpawnVarianceTests
    {
        readonly List<Object> trash = new();

        SpeciesSO MakeSpecies(string name, params string[] slots)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 60;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            var organs = new List<Organ>();
            foreach (var s in slots) organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 2 });
            so.organs = organs.ToArray();
            trash.Add(so);
            return so;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator EveryNpcBody_GetsExactlyOneSpawnVariance_FromBody()
        {
            var human = MakeSpecies("Человек", "Пасть", "Сердце");
            var wolf = MakeSpecies("Волк", "Пасть", "Сердце");

            // чистое шасси → доминанта Человек → химера-альфа: психика, которая о разбросе ничего не знает
            var alpha = ChimeraFactory.Spawn(human, new[] { wolf }, new Vector3(0f, 0.5f, 20f), "Альфа");
            // весь волчий набор → волчья психика: раньше разброс приносила именно она
            var werewolf = ChimeraFactory.Spawn(human, new[] { wolf }, new Vector3(10f, 0.5f, 20f), "Оборотень-волк",
                compose: b => ChimeraFactory.InstallAllFrom(b, "Волк"));
            trash.Add(alpha.gameObject);
            trash.Add(werewolf.gameObject);
            yield return null;

            Assert.IsNotNull(alpha.GetComponent<ChimeraAlphaPsyche>(), "предпосылка: доминанта Человек даёт химеру-альфу");
            Assert.AreEqual(1, alpha.GetComponents<SpawnVariance>().Length, "химера-альфа получила разброс от тела");
            Assert.IsNotNull(werewolf.GetComponent<WolfPsyche>(), "предпосылка: волчий набор даёт волчью психику");
            Assert.AreEqual(1, werewolf.GetComponents<SpawnVariance>().Length, "разброс ровно один: психика своего больше не вешает");
        }
    }
}
