using UnityEngine;

/// <summary>
/// Служебное для пайплайна: принудительный импорт (в batchmode автоимпорт бывает
/// остановлен StopAssetImporting — новые файлы висят неимпортированными).
/// Только редактор; в сборку не попадает. Лес, инфра.
/// </summary>
public static class ForestEditorUtil
{
#if UNITY_EDITOR
    public static void RefreshDatabase()
    {
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("[Forest] AssetDatabase.Refresh вызван");
    }
#endif
}
