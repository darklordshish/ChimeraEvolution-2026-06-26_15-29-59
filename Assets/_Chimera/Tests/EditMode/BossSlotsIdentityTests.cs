using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СТОРОЖ ЧИСЛА ХИМЕРНЫХ СЛОТОВ БОССА (решение геймдизайнера 11.09: два). Число выбрано не на глаз, а счётом
    /// на НАСТОЯЩИХ видах: волк на шасси человека весит 0.9, каждая примесь в химерном слоте разбавляет его долю.
    /// Два слота держат оборотня-волка волком при любых органах (худший случай — две змеиные, 0.662 ≥ 0.65), три
    /// размывают его всегда (лучший случай — три лосиные, 0.649 &lt; 0.65). Поменяются органы видов, их веса или пороги
    /// признания — дефолт `Bossness` перестанет значить то, ради чего его выбрали, и этот тест скажет об этом первым.</summary>
    public class BossSlotsIdentityTests
    {
        const int Trials = 40; // органы в химерные слоты ложатся случайно — гоняем разные сочетания

        static SpeciesSO Load(string name) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");

        static string WerewolfDominant(SpeciesSO chassis, SpeciesSO[] donors, int chimeraSlots)
        {
            var go = new GameObject("BossSlotsIdentity");
            try
            {
                var body = go.AddComponent<CreatureBody>();
                body.Configure(chassis, donors);
                body.ExpandPool(9999);
                ChimeraFactory.InstallAllFrom(body, "Волк");
                ChimeraFactory.GrantAndFillChimeraSlots(body, chimeraSlots);
                var dom = body.MostKin(out _);
                return dom != null ? dom.speciesName : "истинная химера";
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DefaultChimeraSlots_KeepWerewolfAWolf_OneMoreDissolvesIt()
        {
            var human = Load("Человек");
            var donors = new[] { Load("Волк"), Load("Змея"), Load("Лось"), Load("Ёж") };
            Assert.IsNotNull(human, "нет ассета вида Человек");
            foreach (var d in donors) Assert.IsNotNull(d, "нет ассета одного из доноров");

            // копия шасси БЕЗ мест: иначе каждая прививка строила бы модель, а тесту нужна только идентичность
            var chassis = Object.Instantiate(human);
            chassis.sockets = new BodySocket[0];
            try
            {
                int slots = new Bossness.Settings().chimeraSlots;
                for (int t = 0; t < Trials; t++)
                    Assert.AreEqual("Волк", WerewolfDominant(chassis, donors, slots),
                        $"оборотень-волк с {slots} химерными слотами (дефолт Bossness) перестал быть волком — дефолт больше не держит идентичность");
                for (int t = 0; t < Trials; t++)
                    Assert.AreEqual("истинная химера", WerewolfDominant(chassis, donors, slots + 1),
                        $"{slots + 1} слота уже не размывают оборотня-волка — потолок сдвинулся, запись в подсказке Bossness и в GUIDE устарела");
            }
            finally { Object.DestroyImmediate(chassis); }
        }
    }
}
