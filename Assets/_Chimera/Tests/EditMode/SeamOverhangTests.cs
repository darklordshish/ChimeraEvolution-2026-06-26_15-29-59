using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>НИЧТО НЕ СВИСАЕТ ЧЕРЕЗ ШОВ С ЧУЖОЙ СТОРОНЫ (предложение модельной линии 02.10). Деталь выключает поле
    /// поддерева корня своей цепи — кости, начинающейся на шве. Узел, который висит на кости ВЫШЕ шва, но тянется за шов
    /// вдоль цепи, в это поддерево не входит и остаётся рисоваться поверх детали: так три пучка дельты человека висели на
    /// лопатке и давали вторую дельту поверх плеча оборотня (щербина под плечом, поставка v6f). Сторож: у кости с меткой
    /// шва на конце братья корня цепи не уходят за шов дальше четверти длины корня.</summary>
    public class SeamOverhangTests
    {
        static readonly string[] Species = { "Волк", "Лось", "Ёж", "Змея", "Человек" };
        // ШВЫ — места, где начинается деталь (спека конструктора §3); метки внутри цепи (колено, локоть) швами не являются:
        // мышца бедра законно ложится на колено, деталь там не режет
        static readonly HashSet<string> Seams = new() { "основание шеи", "основание черепа", "плечо", "бедро", "корень хвоста", "пояс груди", "запястье", "скакательный" };
        const float Allowed = 0.25f;   // доля длины корня цепи: мышца у сустава заходит за него, пучок вдоль кости — нет

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        /// <summary>Нарушения: «вид: узел < родитель свисает за шов «тип» на X длины корня».</summary>
        public static List<string> Overhangs(SpeciesSO sp, float allowed = Allowed)
        {
            var bad = new List<string>();
            if (sp?.bones == null) return bad;
            var by = new Dictionary<string, Bone>();
            foreach (var b in sp.bones) if (!by.ContainsKey(b.name)) by[b.name] = b;
            var placed = new Dictionary<string, (Vector3, Quaternion)>();
            (Vector3 pos, Vector3 tip, Vector3 dir) At(Bone b)
            {
                var (p, r) = SkeletonBuilder.Place(b, by, placed);
                return (p, SkeletonBuilder.Tip(b, p, r), r * Vector3.up);
            }
            foreach (var p in sp.bones)
            {
                if (string.IsNullOrEmpty(p.mark?.b) || !Seams.Contains(p.mark.b)) continue;
                var kids = sp.bones.Where(k => k.parent == p.name && k.limb == p.limb).ToList();
                if (kids.Count < 2) continue;
                var seam = At(p).tip;
                var root = kids.OrderBy(k => (At(k).pos - seam).sqrMagnitude).ThenByDescending(k => k.length).First();
                var (_, _, dir) = At(root);
                foreach (var k in kids)
                {
                    if (k == root) continue;
                    float over = Vector3.Dot(At(k).tip - seam, dir) / Mathf.Max(1e-4f, root.length);
                    if (over > allowed) bad.Add($"{sp.speciesName}: {k.name} < {p.name} свисает за шов «{p.mark.b}» на {over:0.00} длины «{root.name}»");
                }
            }
            return bad;
        }

        [Test]
        public void NoNodeHangsOverSeamFromAbove()
        {
            var bad = Species.SelectMany(n => Overhangs(Load(n))).ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Detector_CatchesSiblingAlongChainRoot()
        {
            var human = Object.Instantiate(Load("Человек"));
            try
            {
                var shoulder = human.bones.First(b => b.name == "плечо");
                var twin = JsonUtility.FromJson<Bone>(JsonUtility.ToJson(shoulder));
                twin.name = "дубль";   // брат корня вдоль всей его длины — как дельта на лопатке до v6f
                human.bones = human.bones.Append(twin).ToArray();
                Assert.IsNotEmpty(Overhangs(human), "сторож не видит узел, свисающий за шов");
            }
            finally { Object.DestroyImmediate(human); }
        }
    }
}
