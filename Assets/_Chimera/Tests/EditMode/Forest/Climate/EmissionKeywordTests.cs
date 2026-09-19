using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Все Lit-материалы существ несут keyword _EMISSION (иначе второй канал
    /// телеграфа молча не работает). Проверяем ассеты, не grep. Не-Lit
    /// (скайбокс ForestSky и т.п.) — вне инварианта: телеграф их не красит.
    /// </summary>
    public class EmissionKeywordTests
    {
        const string LitShader = "Universal Render Pipeline/Lit";

        [Test]
        public void AllCreatureMaterials_HaveEmissionKeyword()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material",
                new[] { "Assets/_Chimera/Materials" });
            Assert.GreaterOrEqual(guids.Length, 12, "материалов существ не меньше 12");
            int lit = 0;
            var off = new System.Collections.Generic.List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
                if (mat == null || mat.shader == null || mat.shader.name != LitShader) continue;
                lit++;
                if (!mat.IsKeywordEnabled("_EMISSION")) off.Add(path);
            }
            Assert.GreaterOrEqual(lit, 12, "Lit-материалов существ не меньше 12");
            Assert.IsEmpty(off, "без keyword emission не светится: " + string.Join(", ", off));
        }
    }
}
