using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>ОДНО ЗЕРНО ГРАНЕЙ НА ПРОЕКТ (решение геймдизайнера 17.09, поставка 5: лосю «0.084, как волк»). Клетка поля —
    /// это размер граней кожи, и у всех зверей мира он один. Сторож стоит на АССЕТАХ, а не на коде: бутстрап не
    /// обнуляет полей, которые перестал присваивать, а граф нового вида, пришедший на старую клетку 0.02, молча даёт
    /// ~100 тыс. треугольников (лось поставки 5 на 0.02 — 98 884).</summary>
    public class SkinGrainTests
    {
        [Test]
        public void EverySpeciesAsset_HasProjectGrain()
        {
            var guids = AssetDatabase.FindAssets("t:SpeciesSO", new[] { "Assets/_Chimera/Data" });
            Assert.Greater(guids.Length, 0, "ассетов видов не найдено");
            foreach (var g in guids)
            {
                var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(g));
                Assert.AreEqual(SpeciesSO.Grain, sp.skinCell, 1e-6f,
                    $"у вида «{sp.speciesName}» клетка поля {sp.skinCell}, а зерно проекта {SpeciesSO.Grain}: пересоздай виды");
            }
        }
    }
}
