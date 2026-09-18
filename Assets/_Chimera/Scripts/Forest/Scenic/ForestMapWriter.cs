using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Тонкий IO поверх ForestMapBuilder: два PNG (база + слой крутизны отдельно) + md-отчёт.
/// Без тестов — как ShotSpecies/Stand (вся математика под тестами в Builder).
/// Пишет в Docs/Диаграммы/Лес/ (генерируемый детектор, руками не править).
/// Лес, слайс s5.
/// </summary>
public static class ForestMapWriter
{
    public static void WriteMap(WorldGenConfigSO cfg, int res, float step, string dir,
        out string pngBase, out string pngSteep, out string md)
    {
        Color[] colors = ForestMapBuilder.BuildColors(cfg, res, step);
        bool[] steep = ForestMapBuilder.BuildSteepMask(cfg, res, step);
        ForestMapBuilder.Stats stats = ForestMapBuilder.BuildStats(cfg, res, step);

        Directory.CreateDirectory(dir);
        pngBase = Path.Combine(dir, "КАРТА_ЛЕСА.png");
        pngSteep = Path.Combine(dir, "КАРТА_ЛЕСА-крутизна.png");
        md = Path.Combine(dir, "КАРТА_ЛЕСА.md");

        File.WriteAllBytes(pngBase, Encode(colors, res, null));
        File.WriteAllBytes(pngSteep, Encode(colors, res, steep));

        var sb = new StringBuilder();
        sb.AppendLine("# КАРТА ЛЕСА (генерируется, руками не править)");
        sb.AppendLine();
        sb.AppendLine("Рельеф, не биомы. Слой логовищ — по реальным LairSite (s2b).");
        sb.AppendLine();
        sb.AppendLine($"- сид: {cfg.seed}, версия генератора: {cfg.version}");
        sb.AppendLine($"- сетка: {res}×{res}, шаг {step} м");
        sb.AppendLine($"- высота: min {stats.minH:F2} м, max {stats.maxH:F2} м");
        sb.AppendLine($"- макс. уклон: {stats.maxSlope:F1}° (лимит {cfg.maxSlopeDegrees}°)");
        sb.AppendLine($"- крутых клеток: {stats.steepCells} из {stats.totalCells}");
        sb.AppendLine("- файлы: КАРТА_ЛЕСА.png (база), КАРТА_ЛЕСА-крутизна.png (красный слой отдельно)");
        File.WriteAllText(md, sb.ToString(), Encoding.UTF8);
    }

    static byte[] Encode(Color[] cells, int res, bool[] steep)
    {
        const int scale = 4;
        var tex = new Texture2D(res * scale, res * scale, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        Color steepTint = new Color(0.85f, 0.15f, 0.1f);
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                Color c = cells[iz * res + ix];
                if (steep != null && steep[iz * res + ix]) c = Color.Lerp(c, steepTint, 0.65f);
                for (int dz = 0; dz < scale; dz++)
                    for (int dx = 0; dx < scale; dx++)
                        tex.SetPixel(ix * scale + dx, iz * scale + dz, c);
            }
        tex.Apply();
        return tex.EncodeToPNG();
    }
}
