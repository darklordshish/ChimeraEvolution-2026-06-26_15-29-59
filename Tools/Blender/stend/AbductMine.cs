using UnityEngine;

/// Отвод руки поверх позы покоя: кости `плечо` (правая, X > 0) и `плечо.L` поворачиваются наружу вокруг оси анфас (мировой Z)
/// через свой сустав. Аналог `Abduct.Run` ГеймБосса для самопроверки подмышки детали.
public static class AbductMine
{
    public static string Run(float deg)
    {
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>())
        {
            if (t.name == "плечо") { t.RotateAround(t.position, Vector3.forward, deg); n++; }
            else if (t.name == "плечо.L") { t.RotateAround(t.position, Vector3.forward, -deg); n++; }
        }
        return "повёрнуто костей " + n;
    }
}
