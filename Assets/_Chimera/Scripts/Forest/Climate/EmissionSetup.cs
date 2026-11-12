using UnityEngine;

/// <summary>
/// Включение _EMISSION на shared-материалах существ (иначе emission через MPB молча
/// не работает — аудит s3d: keyword выключен во всех 12 .mat). Только кодом/командой, руками в инспекторе не лазить.
/// ПОЧТИ-чёрный emission и флаг BakedEmissive, а не чистый ноль: валидация материала URP (срабатывает, когда
/// материал в редакторе пачкается — например, PlayMode-тест со сценой арены) выставляет keyword по флагу
/// «emission чёрный» и снимала его с змеиных .mat прямо в ассете (письмо Лесника 12.11). 0.002 в линейном пространстве
/// глазом не виден, а телеграф всё равно перекрывает цвет через MPB.
/// Проверка — тестом EmissionKeywordTests, не grep. Лес, слайс s3d.
/// </summary>
public static class EmissionSetup
{
    public const float NearBlack = 0.002f;

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
            if (mat.shader == null || mat.shader.name != "Universal Render Pipeline/Lit") continue;
            bool stable = mat.IsKeywordEnabled("_EMISSION")
                && (mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
            if (stable) continue;
            mat.SetColor("_EmissionColor", new Color(NearBlack, NearBlack, NearBlack, 1f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            mat.EnableKeyword("_EMISSION");
            UnityEditor.EditorUtility.SetDirty(mat);
            n++;
        }
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[Forest] _EMISSION включён у {n} материалов (всего {guids.Length})");
    }
#endif
}
