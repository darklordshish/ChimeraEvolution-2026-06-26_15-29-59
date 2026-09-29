using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СВОИ ПОРОГИ ПРИЗНАНИЯ У БОССА (решение геймдизайнера 29.09: «боссу давали менять, а не всем»). Сторожит две
    /// вещи: ручка `Bossness` двигает пороги ТОЛЬКО на своём теле, а у рядового существа они прежние (0.65 / 0.85); и
    /// оборотень с четырьмя волчьими органами (доля волка 0.60) для босса — волк, а для рядового — истинная химера.</summary>
    public class BossKinThresholdTests
    {
        static SpeciesSO Load(string name) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");

        GameObject go;
        SpeciesSO chassis;

        [TearDown] public void TearDown() { if (go != null) Object.DestroyImmediate(go); if (chassis != null) Object.DestroyImmediate(chassis); }

        // человек с волчьими органами везде, кроме Чутья и Рук — вариант геймдизайнера 29.09
        CreatureBody Werewolf4(bool boss)
        {
            var human = Load("Человек");
            chassis = Object.Instantiate(human);
            chassis.sockets = new BodySocket[0]; // тесту нужна только идентичность, не модель
            go = new GameObject("BossKin");
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, new[] { Load("Волк") });
            body.ExpandPool(9999);
            ChimeraFactory.InstallAllFrom(body, "Волк");
            for (int i = 0; i < body.SlotCount; i++)
            {
                var s = body.GetSlot(i).slot;
                if (s != BodySlots.Sense && s != BodySlots.Arms) continue;
                var v = body.GetVariants(i);
                int native = v.FindIndex(x => x.native);
                if (native >= 0) body.Install(i, native);
            }
            if (boss) body.SetKinThresholds(new Bossness.Settings().kinWeakAt, new Bossness.Settings().kinMediumAt);
            return body;
        }

        [Test]
        public void RegularBody_KeepsCommonThresholds()
        {
            var body = Werewolf4(boss: false);
            Assert.AreEqual(0.65f, body.WeakAt, 1e-4f, "у рядового существа порог слабого признания сдвинулся");
            Assert.AreEqual(0.85f, body.MediumAt, 1e-4f, "у рядового существа порог среднего признания сдвинулся");
            Assert.AreEqual(0.60f, body.Identity(Load("Волк")), 0.005f, "доля волка у оборотня-4 не 0.60 — счёт идентичности изменился");
            Assert.IsNull(body.MostKin(out _), "рядовой с долей волка 0.60 — истинная химера");
        }

        [Test]
        public void Boss_OwnThreshold_MakesWerewolf4AWolf()
        {
            var body = Werewolf4(boss: true);
            Assert.Less(body.WeakAt, 0.65f, "ручка босса не сдвинула порог");
            Assert.Greater(body.WeakAt, 0.5f, "порог босса — строго больше половины");
            var dom = body.MostKin(out var tier);
            Assert.IsNotNull(dom, "босс-оборотень с долей волка 0.60 должен быть волком");
            Assert.AreEqual("Волк", dom.speciesName);
            Assert.AreEqual(KinTier.Weak, tier);
        }
    }
}
