using UnityEngine;

/// <summary>
/// Включение _EMISSION на shared-материалах существ (иначе emission через MPB молча
/// не работает — аудит s3d: keyword выключен во всех 12 .mat). Emission чёрный —
/// вид 1:1 как был. Только кодом/командой, руками в инспекторе не лазить.
/// Проверка — тестом EmissionKeywordTests, не grep. Лес, слайс s3d.
/// </summary>
public static class EmissionSetup
{
#if UNITY_EDITOR
    public static void EnableCreatureEmission()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Material",
            new[] { "Assets/_Chimera/Materials" });
        int n = 0;
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            if (mat.IsKeywordEnabled("_EMISSION")) continue;
            mat.EnableKeyword("_EMISSION");
            UnityEditor.EditorUtility.SetDirty(mat);
            n++;
        }
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[Forest] _EMISSION включён у {n} материалов (всего {guids.Length})");
    }
#endif
}
