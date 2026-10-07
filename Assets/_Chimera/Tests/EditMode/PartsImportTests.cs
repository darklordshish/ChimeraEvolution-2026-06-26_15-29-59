using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>ДЕТАЛЬ ВИДА НЕ ОТВЕРГНУТА МОЛЧА (предложение модельной линии 07.10): `ReadParts` отбрасывает деталь, чьи
    /// кости не узлы графа, — переименуй узел, и нога волка пропадает без единой ошибки в тестах (силуэт 0.70 → 0.68).
    /// Сторож: каждый паспорт детали вида читается без замечаний, и в ассете вида деталей столько же, сколько паспортов.</summary>
    public class PartsImportTests
    {
        static readonly string[] Species = { "Волк", "Лось", "Ёж", "Змея", "Человек" };

        [Test]
        public void EveryPartPassport_IsImported()
        {
            foreach (var n in Species)
            {
                var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");
                string prefix = SpeciesHandoff.Translit(sp.speciesName) + "-";
                int passports = Directory.Exists(SpeciesHandoff.PartsDir)
                    ? Directory.GetFiles(SpeciesHandoff.PartsDir, prefix + "*.json").Length : 0;
                var read = SpeciesHandoff.ReadParts(sp, out var problems);
                Assert.IsEmpty(problems, $"{n}: детали отвергнуты при чтении:\n" + string.Join("\n", problems));
                Assert.AreEqual(passports, read.Length, $"{n}: паспортов {passports}, прочитано деталей {read.Length}");
                Assert.AreEqual(passports, sp.parts?.Count(p => p != null) ?? 0, $"{n}: в ассете деталей не столько, сколько паспортов — пересоздай виды");
            }
        }
    }
}
