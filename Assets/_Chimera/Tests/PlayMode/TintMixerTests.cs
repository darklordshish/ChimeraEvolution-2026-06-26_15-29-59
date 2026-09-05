using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// TintMixer vs Telegraph MPB _BaseColor, Rebase не стирает состав, IsHeadName по сокету
    /// (Telegraph.cs:30, TintMixer.cs, CreatureBody.Tint.cs:22)
    /// </summary>
    public class TintMixerTests
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        Material MakeMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, c);
            return mat;
        }

        SpeciesSO MakeSpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "Тест";
            so.tint = Color.gray;
            so.mutagenPool = 10;
            so.baseHp = 50;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f,0.48f,1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f,0.22f,0.46f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f,0.09f,0.22f) },
                new BodySocket { name = "Хвост", parent = "хребет", attach = 0f, baseSize = new Vector3(0.2f,0.2f,0.4f) },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3 },
                new Organ { organName = "Хвост", slot = "Хвост", cost = 3 },
            };
            so.bones = new Bone[0];
            return so;
        }

        [UnityTest]
        public IEnumerator TintMixer_Apply_Writes_BaseColor_ViaMPB()
        {
            var so = MakeSpecies();
            var go = new GameObject("TintMixer_MPB");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f,1f,0f);
            // примитивы вместо морф-сборки: один куб "голова" и один "Хвост" чтобы разные роли
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "голова";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f,1f,0f);
            head.GetComponent<Renderer>().sharedMaterial = MakeMat(Color.white);
            // отметим голову как PartRole.Head чтобы эмоция ложилась только туда
            var pmHead = head.AddComponent<PartMark>();
            pmHead.role = PartRole.Head;

            var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tail.name = "Хвост";
            tail.transform.SetParent(go.transform, false);
            tail.transform.localPosition = new Vector3(0f,1f,-0.5f);
            tail.GetComponent<Renderer>().sharedMaterial = MakeMat(Color.white);

            var mixer = go.AddComponent<TintMixer>();
            var telegraph = go.AddComponent<Telegraph>();
            yield return null;

            // TintMixer должен собрать оба рендерера
            mixer.Rebuild();
            telegraph.RebuildRenderers();

            Color comp = new Color(0.2f, 0.55f, 0.85f);
            mixer.SetComposition(comp);
            mixer.Apply();
            yield return null;

            // проверяем что MPB _BaseColor стал comp для обоих рендереров
            var mpb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is not MeshRenderer && r is not SkinnedMeshRenderer) continue;
                r.GetPropertyBlock(mpb);
                Color c = mpb.GetColor(BaseColor);
                Assert.AreEqual(comp.r, c.r, 0.02f, $"MPB _BaseColor r у {r.name} должен быть составом");
                Assert.AreEqual(comp.g, c.g, 0.02f);
                Assert.AreEqual(comp.b, c.b, 0.02f);
            }

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebase_DoesNotWipeComposition()
        {
            var go = new GameObject("TintRebase");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "голова";
            cube.transform.SetParent(go.transform, false);
            cube.GetComponent<Renderer>().sharedMaterial = MakeMat(Color.white);

            var mixer = go.AddComponent<TintMixer>();
            var tg = go.AddComponent<Telegraph>();
            yield return null;
            mixer.Rebuild();
            tg.RebuildRenderers();

            Color comp = new Color(0.75f, 0.2f, 0.3f);
            mixer.SetComposition(comp);
            mixer.Apply();
            tg.Rebase(); // телеграф снимает текущий MPB как базу (Telegraph.cs:93)
            yield return null;

            // замах поверх состава
            tg.Set(true, Color.red, intent: true);
            Assert.IsTrue(tg.IsShowing);
            // смена состава во время замаха: Rebase + Reapply сохраняют замах
            Color comp2 = new Color(0.15f, 0.78f, 0.42f);
            mixer.SetComposition(comp2);
            mixer.Apply();
            tg.Rebase();
            tg.Reapply();
            yield return null;
            Assert.IsTrue(tg.IsShowing, "Reapply не должен сбрасывать активный замах (CreatureBody.Tint.cs:25)");
            // baseColors[0] должен стать comp2, а visible цвет — красный замах, не comp2/серый
            var fi = typeof(Telegraph).GetField("baseColors", BindingFlags.NonPublic | BindingFlags.Instance);
            var baseColors = (Color[])fi.GetValue(tg);
            Assert.AreEqual(comp2.r, baseColors[0].r, 0.03f, "Rebase должен переснять новый состав как базу");
            Assert.AreEqual(comp2.g, baseColors[0].g, 0.03f);
            Assert.AreEqual(comp2.b, baseColors[0].b, 0.03f);

            // гасим замах — должен вернуться состав comp2, а не стартанутый материал (white) и не comp1
            tg.Clear();
            yield return null;
            var mpb = new MaterialPropertyBlock();
            cube.GetComponent<Renderer>().GetPropertyBlock(mpb);
            Color after = mpb.GetColor(BaseColor);
            Assert.AreEqual(comp2.r, after.r, 0.03f, "после Clear должен вернуться актуальный состав, а не старый");
            Assert.AreEqual(comp2.g, after.g, 0.03f);
            Assert.AreEqual(comp2.b, after.b, 0.03f);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IsHeadName_MapsToSocketNames()
        {
            var mi = typeof(Telegraph).GetMethod("IsHeadName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "Telegraph.IsHeadName должен существовать (Telegraph.cs:30)");
            System.Func<string,bool> call = n => (bool)mi.Invoke(null, new object[]{ n });

            // головные по конвенции имён (Blender + сокеты)
            Assert.IsTrue(call("Head"));
            Assert.IsTrue(call("Muzzle"));
            Assert.IsTrue(call("Nose"));
            Assert.IsTrue(call("Jaw"));
            Assert.IsTrue(call("EarL"));
            Assert.IsTrue(call("Ear_R"));
            Assert.IsTrue(call("голова"), "морф-часть по сокету голова — головная");
            Assert.IsTrue(call("Пасть"));
            Assert.IsTrue(call("уши"));
            Assert.IsTrue(call("глаза"));

            // не головные
            Assert.IsFalse(call("Хвост"));
            Assert.IsFalse(call("Ноги"));
            Assert.IsFalse(call("хребет"));
            Assert.IsFalse(call("Шкура"));
            Assert.IsFalse(call("Коготь"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TintLayers_Priority_Order()
        {
            var go = new GameObject("TintLayers");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "хребет";
            cube.transform.SetParent(go.transform, false);
            cube.GetComponent<Renderer>().sharedMaterial = MakeMat(Color.white);

            var mixer = go.AddComponent<TintMixer>();
            yield return null;
            mixer.Rebuild();

            Color comp = Color.gray;
            mixer.SetComposition(comp);
            // два слоя: низкий приоритет синий, высокий — красный
            mixer.Set("low", 10, Color.blue, 1f);
            mixer.Set("high", 20, Color.red, 0.5f);
            mixer.Apply();
            yield return null;

            // итог = Lerp(Lerp(comp, blue,1), red,0.5) = Lerp(blue, red,0.5)
            Color expected = Color.Lerp(Color.blue, Color.red, 0.5f);
            var mpb = new MaterialPropertyBlock();
            cube.GetComponent<Renderer>().GetPropertyBlock(mpb);
            Color got = mpb.GetColor(BaseColor);
            Assert.AreEqual(expected.r, got.r, 0.02f);
            Assert.AreEqual(expected.g, got.g, 0.02f);
            Assert.AreEqual(expected.b, got.b, 0.02f);

            // свой цвет детали не перетирается составом (PartMark.HasOwn)
            var pm = cube.AddComponent<PartMark>();
            pm.own = new Color(0.1f,0.9f,0.1f,1f); // alpha>0 => HasOwn
            mixer.Rebuild();
            mixer.Apply();
            yield return null;
            cube.GetComponent<Renderer>().GetPropertyBlock(mpb);
            Color withOwnBase = mpb.GetColor(BaseColor);
            // база теперь own, не comp, но поверх те же слои
            Color expectedOwn = Color.Lerp(Color.Lerp(pm.own, Color.blue, 1f), Color.red, 0.5f);
            Assert.AreEqual(expectedOwn.r, withOwnBase.r, 0.03f, "своя окраска должна быть базой вместо состава");

            Object.Destroy(go);
            yield return null;
        }
    }
}
