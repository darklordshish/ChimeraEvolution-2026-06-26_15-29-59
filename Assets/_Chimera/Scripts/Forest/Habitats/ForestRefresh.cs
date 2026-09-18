using UnityEngine;

/// <summary>
/// Служебное для пайплайна (дубль ForestEditorUtil из s1 — там другая ветка, на мердже
/// в infra/forest останется один; см. спеку s2): принудительный импорт в batchmode.
/// Только редактор. Лес, инфра.
/// </summary>
public static class ForestRefresh
{
#if UNITY_EDITOR
    public static void RefreshDatabase()
    {
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("[Forest] AssetDatabase.Refresh вызван");
    }
#endif
}
