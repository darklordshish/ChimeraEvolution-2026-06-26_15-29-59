/// <summary>ЗАКРЫТЬ ФОНОВЫЙ РЕДАКТОР (22.10). `unity command quit` в режиме редактора падает (400: PipelineQuitScheduler и
/// DontDestroyOnLoad), а брошенный headless съел всю оперативную память. Выход — после ответа команды, иначе пайплайн не дождётся.
///   unity command set_autotick --enable false
///   unity command run_script --file Tools/Agent/Quit.cs --entry Quit.Run</summary>
public static class Quit
{
    public static string Run()
    {
        UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.Exit(0);
        return "редактор закрывается";
    }
}
