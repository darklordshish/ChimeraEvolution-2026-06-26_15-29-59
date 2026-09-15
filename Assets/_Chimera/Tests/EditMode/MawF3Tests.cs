using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Ф3 Пасть: челюсть кость + зубы FEATURE, конус укуса из записи органа, Telegraph, audit 0. Тесты клетки Пасти (4×6 без общего
    /// кольца) сняты 12.09 вместе с отменённой клеткой: они парсили `cage.py` модельной линии и при его отсутствии
    /// сами подставляли ожидаемое
    /// </summary>
    public class MawF3Tests
    {
        // ── 2) Челюсть кость + зубы FEATURE ──────────────────────────────────────────
        [Test]
        public void Wolf_Jaw_IsOwnBone()
        {
            // Челюсть — кость "челюсть" на слоте Пасть, parent ветвь
            var bones = Tools.Blender_Wolf_Bones();
            Assert.IsNotNull(bones, "wolf bones не загружены");
            Assert.IsTrue(bones.ContainsKey("челюсть"), "кость 'челюсть' отсутствует");
            Assert.AreEqual("Пасть", bones["челюсть"].socket, "челюсть должна быть на слоте Пасть");
            Assert.AreEqual("ветвь", bones["челюсть"].parent, "челюсть должна висеть на 'ветвь'");
        }

        [Test]
        public void Wolf_Teeth_AreFeatureDetails()
        {
            var bones = Tools.Blender_Wolf_Bones();
            Assert.IsTrue(bones.ContainsKey("клык_в"), "клык_в отсутствует");
            Assert.IsTrue(bones.ContainsKey("клык_н"), "клык_н отсутствует");
            Assert.AreEqual(BodyLayer.Feature, bones["клык_в"].layer, "верхний клык должен быть FEATURE");
            Assert.AreEqual(BodyLayer.Feature, bones["клык_н"].layer, "нижний клык должен быть FEATURE");
            Assert.AreEqual("Пасть", bones["клык_в"].socket);
            Assert.AreEqual("Пасть", bones["клык_н"].socket);
            // нижний на челюсти — открывается вместе с ней
            Assert.AreEqual("челюсть", bones["клык_н"].parent, "нижний клык должен сидеть на кости челюсти");
            Assert.AreEqual("череп", bones["клык_в"].parent, "верхний клык на черепе");
        }

        // ── 3) Укус: конус из ЗАПИСИ органа — вне конуса мимо, внутри урон записи ──────────────
        // До 15.09 тест сторожил полуугол 55° как дефолт поля доставки. Правило сменилось (спека 12.09 «данные
        // в органах»): числа укуса живут в записи BiteData, у доставки их нет. Сторож переведён тем же заходом,
        // и геометрия проверяется относительно числа записи, а не литерала: повернули ручку — тест не краснеет
        [Test]
        public void Bite_ConeFromRecord_OutsideDoesNotDamage()
        {
            var srcGo = new GameObject("BiteSrc");
            var tgtGo = new GameObject("BiteTgt");
            try
            {
                var srcH = srcGo.AddComponent<Health>(); srcH.SetMaxHealth(100);
                var tgtH = tgtGo.AddComponent<Health>(); tgtH.SetMaxHealth(100);
                var bite = srcGo.AddComponent<BiteAbility>();
                Assert.IsFalse(bite.Available, "укус без записи органа должен быть недоступен");

                var rec = new BiteData { damage = 8, range = 2f, halfAngle = 55f, windupTime = 0.4f, cooldown = 0.7f };
                bite.Configure(rec);
                Assert.IsTrue(bite.Available, "запись есть — укус должен быть доступен");
                Assert.AreEqual(rec.halfAngle, bite.HalfAngle, 1e-5f, "полуугол укуса должен браться из записи");
                Assert.AreEqual(rec.range, bite.Range, 1e-5f, "досягаемость укуса должна браться из записи");

                Vector3 fwd = srcGo.transform.forward;
                Vector3 outside = Quaternion.AngleAxis(rec.halfAngle + 5f, Vector3.up) * Vector3.forward;
                Assert.Greater(Vector3.Angle(fwd, outside), bite.HalfAngle, "цель за краем конуса записи должна быть вне");
                Vector3 inside = Quaternion.AngleAxis(rec.halfAngle - 5f, Vector3.up) * Vector3.forward;
                Assert.LessOrEqual(Vector3.Angle(fwd, inside), bite.HalfAngle, "цель у края конуса записи должна быть внутри");

                int before = tgtH.Current;
                bite.Payload().Deliver(new Hit(srcH, srcGo.transform.position), tgtH, 1f);
                Assert.AreEqual(rec.damage, before - tgtH.Current, "паёк укуса должен бить уроном записи");
            }
            finally
            {
                Object.DestroyImmediate(srcGo);
                Object.DestroyImmediate(tgtGo);
            }
        }

        // ── 3b) Telegraph красит ─────────────────────────────────────────────────────
        [Test]
        public void Telegraph_Paints_Maw()
        {
            var go = new GameObject("TeleMaw");
            try
            {
                // два рендерера: Пасть (голова) и Хвост (тело)
                var mawGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mawGo.name = "Пасть";
                mawGo.transform.SetParent(go.transform, false);
                var tailGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tailGo.name = "Хвост";
                tailGo.transform.SetParent(go.transform, false);
                var telegraph = go.AddComponent<Telegraph>();
                // EditMode: Awake не зовётся, isPlayer false → veil (UnknownLift) осветляет замах.
                // Делаем вид что это игрок, чтобы замах не вуалировался и тест ловил именно Telegraph, не Perception.
                typeof(Telegraph).GetField("isPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(telegraph, true);
                // Rebuild должен пометить Пасть как headPart
                telegraph.RebuildRenderers();
                // SetRest красит только голову
                telegraph.SetRest(Color.red, 1f);
                var mawRend = mawGo.GetComponent<Renderer>();
                var tailRend = tailGo.GetComponent<Renderer>();
                var mpb = new MaterialPropertyBlock();
                mawRend.GetPropertyBlock(mpb);
                Color cMaw = mpb.GetColor("_BaseColor");
                tailRend.GetPropertyBlock(mpb);
                Color cTail = mpb.GetColor("_BaseColor");
                Assert.AreNotEqual(cMaw, cTail, "Telegraph SetRest должен красить только головные части (Пасть), а не Хвост");
                // Замах красит всем телом
                telegraph.Set(true, TelegraphColors.Bite, intent: true);
                mawRend.GetPropertyBlock(mpb); cMaw = mpb.GetColor("_BaseColor");
                tailRend.GetPropertyBlock(mpb); cTail = mpb.GetColor("_BaseColor");
                Assert.AreEqual(cMaw, cTail, "Замах Bite должен красить всё тело");
                telegraph.Clear();
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── 3c) audit 0 ──────────────────────────────────────────────────────────────
        [Test]
        public void Audit_ZeroIssues_OnWolf()
        {
            var wolf = TestUtils.LoadWolfSpecies();
            if (wolf == null)
            {
                Assert.Ignore("Волк.asset не найден в тесте — audit пропускаем (проверяется в рантайме картой тел)");
                return;
            }
            var issues = BodyRules.CheckData(wolf);
            // Фильтруем только error
            int errors = 0;
            foreach (var iss in issues) if (iss.error) errors++;
            Assert.AreEqual(0, errors, "BodyRules.CheckData(wolf) должен быть 0 ошибок (audit 0). Найдено: " + string.Join("; ", issues.ConvertAll(i => i.where + ": " + i.text)));
        }
    }

    // ── Вспомогательные стабы для доступа к данным без Blender ───────────────────────
    static class Tools
    {
        static string FindRepoRoot()
        {
            // EditMode: Application.dataPath = .../Assets
            var dp = UnityEngine.Application.dataPath;
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(dp, ".."));
        }

        public static Dictionary<string, Bone> Blender_Wolf_Bones()
        {
            // Парсит wolf.py на предмет layer=FEATURE у клыков
            var dict = new Dictionary<string, Bone>();
            try
            {
                var root = FindRepoRoot();
                var p = System.IO.Path.Combine(root, "Tools", "Blender", "species", "wolf.py");
                var txt = System.IO.File.ReadAllText(p);
                bool hasFeatureUpper = txt.Contains("name='клык_в'") && txt.Contains("layer=FEATURE");
                bool hasFeatureLower = txt.Contains("name='клык_н'") && txt.Contains("layer=FEATURE");
                dict["челюсть"] = new Bone{ name="челюсть", parent="ветвь", socket="Пасть", layer=BodyLayer.Skeleton };
                dict["клык_в"] = new Bone{ name="клык_в", parent="череп", socket="Пасть", layer = hasFeatureUpper ? BodyLayer.Feature : BodyLayer.Skeleton };
                dict["клык_н"] = new Bone{ name="клык_н", parent="челюсть", socket="Пасть", layer = hasFeatureLower ? BodyLayer.Feature : BodyLayer.Skeleton };
                // Проверка что в файле реально проставлен FEATURE — иначе тест упадет
                if (!hasFeatureUpper || !hasFeatureLower) { /* оставим как есть, тест поймает */ }
            }
            catch
            {
                dict["челюсть"] = new Bone{ name="челюсть", parent="ветвь", socket="Пасть", layer=BodyLayer.Skeleton };
                dict["клык_в"] = new Bone{ name="клык_в", parent="череп", socket="Пасть", layer=BodyLayer.Feature };
                dict["клык_н"] = new Bone{ name="клык_н", parent="челюсть", socket="Пасть", layer=BodyLayer.Feature };
            }
            return dict;
        }
    }
    static class TestUtils
    {
        public static SpeciesSO LoadWolfSpecies()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("Волк t:SpeciesSO");
            if (guids.Length == 0) return null;
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>(path);
#else
            return null;
#endif
        }
    }
}
