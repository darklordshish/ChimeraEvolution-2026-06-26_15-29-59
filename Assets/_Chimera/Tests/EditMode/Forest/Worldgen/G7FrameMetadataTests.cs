using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 0: подпись кадра — не пересказ. Манифест 6/19 лежит рядом с кадрами;
    /// в G7-метаданные.txt у каждого блока есть все поля, `treeVersion=absent`.
    /// </summary>
    public class G7FrameMetadataTests
    {
        const string Rel = "Docs/Диаграммы/Лес/Кадры/15-G7";

        static string File(string name) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", Rel, name));

        [Test]
        public void Manifest_HasNineteenRows_AndSixExemplar()
        {
            string path = File("МАНИФЕСТ.md");
            Assert.IsTrue(System.IO.File.Exists(path), "манифест 15-G7/МАНИФЕСТ.md не найден");
            var lines = System.IO.File.ReadAllLines(path);
            var rows = lines.Where(l => l.TrimStart().StartsWith("| G7-")).ToList();
            Assert.AreEqual(19, rows.Count, "манифест должен перечислять 19 кадров");
            Assert.GreaterOrEqual(rows.Count(l => l.Contains("exemplar")), 6,
                "exemplar-минимум — 6");
            Assert.IsTrue(lines.Any(l => l.Contains("6 exemplar")),
                "в манифесте не назван минимум 6 exemplar");
        }

        [Test]
        public void Metadata_HasEveryField_ForEachFrame()
        {
            string path = File("G7-метаданные.txt");
            Assert.IsTrue(System.IO.File.Exists(path),
                "G7-метаданные.txt не заведён рядом с кадрами");
            string text = System.IO.File.ReadAllText(path);
            var blocks = text.Split(new[] { "\n---" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(b => b.Contains("frame=")).ToList();
            Assert.GreaterOrEqual(blocks.Count, 6, "нужны подписи минимум шести кадров A/B");
            string[] keys =
            {
                "frame=", "image=", "seed=", "worldVersion=", "treeVersion=",
                "path=", "scheme=", "signature=", "profile=", "camera=", "height=",
                "time=", "weather=", "fog=", "hidden=", "pixelDiff=", "proves=",
            };
            foreach (var block in blocks)
            {
                foreach (string key in keys)
                    Assert.IsTrue(block.Contains(key), $"в подписи нет поля {key}");
                Assert.IsTrue(block.Contains("treeVersion=absent"),
                    "до шага 1 поле treeVersion обязано быть absent");
            }
        }
    }
}
