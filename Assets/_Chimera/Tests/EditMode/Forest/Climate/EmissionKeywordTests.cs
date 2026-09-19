using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Все shared-материалы существ несут keyword _EMISSION (иначе второй канал
    /// телеграфа молча не работает). Проверяем ассеты, не grep. Слайс s3d.
    /// </summary>
    public class EmissionKeywordTests
    {
        [Test]
        public void AllCreatureMaterials_HaveEmissionKeyword()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material",
                new[] { "Assets/_Chimera/Materials" });
            Assert.GreaterOrEqual(guids.Length, 12, "материалов существ не меньше 12");
            var off = new System.Collections.Generic.List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
                if (mat == null || !mat.IsKeywordEnabled("_EMISSION")) off.Add(path);
            }
            Assert.IsEmpty(off, "без keyword emission не светится: " + string.Join(", ", off));
        }
    }
}
