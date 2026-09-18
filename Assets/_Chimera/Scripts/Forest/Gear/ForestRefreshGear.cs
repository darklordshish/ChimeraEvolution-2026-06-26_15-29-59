using UnityEngine;

/// <summary>
/// Служебное для пайплайна (дубли из s1/s2/s3 — там другие ветки, на мердже
/// в infra/forest останется один; см. спеку s4): принудительный импорт в batchmode.
/// Только редактор. Лес, инфра.
/// </summary>
public static class ForestRefreshGear
{
#if UNITY_EDITOR
    public static void RefreshDatabase()
    {
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("[Forest] AssetDatabase.Refresh вызван");
    }
#endif
}
