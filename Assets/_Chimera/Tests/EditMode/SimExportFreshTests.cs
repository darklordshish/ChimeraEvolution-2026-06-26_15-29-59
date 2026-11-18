using System.IO;
using Chimera.Laws;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// СТОРОЖ ВЫГРУЗКИ ДЛЯ СИМУЛЯТОРА (план Ф0, С9). `Docs/Диаграммы/Эволюция/данные.json` — входы законов, которые читает
    /// симулятор вне Unity. Выгрузка, собранная здесь заново из ассетов, обязана равняться файлу в репозитории: правка вида,
    /// органа, записи приёма, `LawsConfig`, префаба или сцены без `unity command chimera-sim-export` — красный тест.
    ///
    /// ДВЕ ЧАСТИ. Ядро (виды, органы, конфиги, префабы, эволюция) сверяется тут же, в EditMode: сборка из ассетов без сцены,
    /// секунды. Логова (`ForestPavilion` → `ForestBuilder.Build` на Ø2000) — минуты и смена открытой сцены, поэтому их сверка
    /// помечена `[Explicit]` и идёт вручную (`run_tests --filter Lairs_ExplicitFresh`) вместе с выгрузкой. Лес в EditMode не
    /// собирается, ядро от него не зависит.
    /// </summary>
    public class SimExportFreshTests
    {
        static string FilePath => Path.Combine(Application.dataPath, "..", SimExport.OutPath);

        static SimData Load()
        {
            Assert.IsTrue(File.Exists(FilePath), "нет " + SimExport.OutPath + " — `unity command chimera-sim-export`");
            return JsonUtility.FromJson<SimData>(File.ReadAllText(FilePath));
        }

        // файл без логов и без отпечатка против свежего ядра без отпечатка: ядро — всё, кроме `lairs` и мира сцены
        static string CoreText(SimData d)
        {
            d.fingerprint = "";
            d.lairs = new SimLair[0];
            d.world.scene = ""; d.world.mapDiameter = 0f; d.world.domeArea = 0f;
            return JsonUtility.ToJson(d, true).Replace("\r\n", "\n");
        }

        [Test]
        public void Core_FreshExport_EqualsFileInRepo()
        {
            var file = Load();
            Assert.AreEqual(SimData.Schema, file.schema, "схема файла другая — перевыгрузить");
            string fresh = CoreText(SimExport.BuildCore());
            string onDisk = CoreText(file);
            if (fresh != onDisk)
            {
                int i = 0;
                while (i < fresh.Length && i < onDisk.Length && fresh[i] == onDisk[i]) i++;
                string around(string t) => t.Substring(System.Math.Max(0, i - 60), System.Math.Min(160, t.Length - System.Math.Max(0, i - 60)));
                Assert.Fail("данные.json устарел: входы законов в игре изменились. `unity command chimera-sim-export`.\nпервое расхождение на символе " + i
                            + "\nв файле : …" + around(onDisk) + "\nсвежее  : …" + around(fresh));
            }
        }

        [Test]
        public void File_FingerprintMatchesItsContent()
        {
            // отпечаток — FNV по тексту файла с пустым полем: правка файла руками ломает его, как и устаревание
            string text = File.ReadAllText(FilePath).Replace("\r\n", "\n");
            var d = JsonUtility.FromJson<SimData>(text);
            Assert.IsNotEmpty(d.fingerprint);
            string blank = text.Replace("\"fingerprint\": \"" + d.fingerprint + "\"", "\"fingerprint\": \"\"");
            Assert.AreEqual(d.fingerprint, SimData.Fnv64(blank), "отпечаток в шапке не сходится с содержимым файла");
        }

        [Test]
        public void Core_CoversEverySpecies_WithOrgansAndAbilities()
        {
            var core = SimExport.BuildCore();
            Assert.AreEqual(5, core.species.Length);
            foreach (var s in core.species)
            {
                Assert.Greater(s.baseHp, 0f, s.name);
                Assert.Greater(s.organs.Length, 0, s.name);
            }
            int abilities = 0;
            foreach (var s in core.species) foreach (var o in s.organs) abilities += o.abilities.Length;
            Assert.Greater(abilities, 5, "записей приёмов в выгрузке нет — рефлексия по `AbilityData` не сработала");
        }

        [Test, Explicit("Собирает мир ForestPavilion (Ø2000) и меняет открытую сцену: минуты. Запуск вручную после chimera-sim-export.")]
        public void Lairs_ExplicitFresh_EqualsFileInRepo()
        {
            var file = Load();
            var fresh = SimExport.BuildLairs(out var scene, out var diameter);
            Assert.AreEqual(file.world.scene, scene);
            Assert.AreEqual(file.world.mapDiameter, diameter, 0f);
            string a = JsonUtility.ToJson(new SimData { lairs = file.lairs }, true);
            string b = JsonUtility.ToJson(new SimData { lairs = fresh }, true);
            Assert.AreEqual(a, b, "логова в данные.json разошлись со сборкой мира — `unity command chimera-sim-export`");
        }
    }
}
