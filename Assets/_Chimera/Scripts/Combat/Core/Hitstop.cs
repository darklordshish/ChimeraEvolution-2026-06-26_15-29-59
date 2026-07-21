using System.Collections;
using UnityEngine;

/// <summary>
/// Глобальный хитстоп: на миг роняет Time.timeScale — то самое «мясо» при ударе.
/// Вызов: Hitstop.Do(0.06f);
/// </summary>
public static class Hitstop
{
    static HitstopRunner runner;

    public static void Do(float seconds, float timeScale = 0f)
    {
        if (runner == null)
        {
            var go = new GameObject("~HitstopRunner");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<HitstopRunner>();
        }
        runner.Begin(seconds, timeScale);
    }
}

public class HitstopRunner : MonoBehaviour
{
    Coroutine current;

    public void Begin(float seconds, float timeScale)
    {
        if (current != null) StopCoroutine(current);
        current = StartCoroutine(Run(seconds, timeScale));
    }

    IEnumerator Run(float seconds, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(seconds); // реальное время, на него timeScale не влияет
        Time.timeScale = 1f;
        current = null;
    }
}
