using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// СТОРОЖ ВЫГРУЗКИ КАРТЫ ДЛЯ СИМУЛЯТОРА (ГеймБосс 18.11; спека карты §9). `Docs/Диаграммы/Эволюция/карта.json` обязан
    /// равняться свежей выгрузке. До шага 5б карта — снимок собранного мира, свежая выгрузка собирает павильон v2 (≈10 с),
    /// поэтому сверка содержимого — [Explicit] (`run_tests --filter MapExportFreshTests --include_explicit true`), как у
    /// логов `SimExportFreshTests`. Отпечаток против текста самого файла — дёшево, идёт всегда.
    /// </summary>
    public class MapExportFreshTests
    {
        static string FilePath => Path.Combine(Application.dataPath, "..", MapExport.OutPath);

        [Test]
        public void File_FingerprintMatchesItsContent()
        {
            Assert.IsTrue(File.Exists(FilePath), "нет " + MapExport.OutPath + " — `unity command chimera-map-export`");
            string text = File.ReadAllText(FilePath).Replace("\r\n", "\n");
            var d = JsonUtility.FromJson<ForestMapExportData>(text);
            Assert.AreEqual(ForestMapExportData.Schema, d.schema, "схема файла другая — перевыгрузить");
            Assert.IsNotEmpty(d.fingerprint);
            string fp = d.fingerprint;
            Assert.AreEqual(text, ForestMapExportData.Serialize(d), "файл = своя сериализация (правка руками ломает отпечаток)");
            Assert.AreEqual(fp, d.fingerprint, "отпечаток — FNV по содержимому");
        }

        [Test, Explicit("собирает павильон v2 — вручную вместе с выгрузкой")]
        public void FreshExport_EqualsFileInRepo()
        {
            string fresh = MapExport.BuildText(out _);
            string onDisk = File.ReadAllText(FilePath).Replace("\r\n", "\n");
            if (fresh == onDisk) return;
            int i = 0;
            while (i < fresh.Length && i < onDisk.Length && fresh[i] == onDisk[i]) i++;
            string around(string t) => t.Substring(System.Math.Max(0, i - 60), System.Math.Min(160, t.Length - System.Math.Max(0, i - 60)));
            Assert.Fail("карта.json устарела: карта леса изменилась. `unity command chimera-map-export`.\nпервое расхождение на символе " + i
                        + "\nв файле : …" + around(onDisk) + "\nсвежее  : …" + around(fresh));
        }
    }
}
