using UnityEditor;

/// <summary>Импорт новых файлов и компиляция. Ничего не меняет в мире.</summary>
public static class Refresh
{
    public static string Run()
    {
        AssetDatabase.Refresh();
        return "refresh requested";
    }
}
