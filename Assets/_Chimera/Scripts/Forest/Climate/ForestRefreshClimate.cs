using UnityEngine;

/// <summary>
/// Служебное для пайплайна (дубли ForestEditorUtil/ForestRefresh из s1/s2 — там другие ветки,
/// на мердже в infra/forest останется один; см. спеку s3): принудительный импорт в batchmode.
/// Только редактор. Лес, инфра.
/// </summary>
public static class ForestRefreshClimate
{
#if UNITY_EDITOR
    public static void RefreshDatabase()
    {
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("[Forest] AssetDatabase.Refresh вызван");
    }
#endif
}
