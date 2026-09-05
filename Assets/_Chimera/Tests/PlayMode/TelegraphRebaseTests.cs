using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class TelegraphRebaseTests
    {
        SpeciesSO MakeSpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "Волк";
            so.tint = new Color(0.5f, 0.5f, 0.52f);
            so.mutagenPool = 16;
            so.baseHp = 50;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3 },
            };
            so.bones = new Bone[0];
            return so;
        }

        int CountMeshRenderers(Transform root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (r is MeshRenderer || r is SkinnedMeshRenderer) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator RebuildRenderers_AfterMorph_PicksNewRenderers()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("TelegraphRebase");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            var telegraph = go.AddComponent<Telegraph>();
            var mixer = go.AddComponent<TintMixer>();
            yield return null; // Awake снял пустой набор

            MorphBuilder.Build(go.transform, so, worn);
            // тело зовёт Rebuild после сборки (CreatureBody.cs:430-431)
            mixer.Rebuild();
            telegraph.RebuildRenderers();
            yield return null;

            int expected = CountMeshRenderers(go.transform);
            var fi = typeof(Telegraph).GetField("renderers", BindingFlags.NonPublic | BindingFlags.Instance);
            var renderers = (Renderer[])fi.GetValue(telegraph);
            Assert.AreEqual(expected, renderers.Length, "RebuildRenderers должен подобрать все Mesh/SkinnedMesh под Morph");

            // TintMixer также должен видеть те же рендереры
            var fi2 = typeof(TintMixer).GetField("rends", BindingFlags.NonPublic | BindingFlags.Instance);
            var rends = (Renderer[])fi2.GetValue(mixer);
            Assert.AreEqual(expected, rends.Length, "TintMixer.Rebuild должен видеть те же рендереры");

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebase_AfterTint_UpdatesBaseColors()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("TelegraphRebase_Tint");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            var telegraph = go.AddComponent<Telegraph>();
            var mixer = go.AddComponent<TintMixer>();
            MorphBuilder.Build(go.transform, so, worn);
            mixer.Rebuild();
            telegraph.RebuildRenderers();
            yield return null;

            // задаём цвет по составу через микшер (CreatureBody.Tint.cs:22-25)
            Color comp = new Color(0.8f, 0.2f, 0.3f);
            mixer.SetComposition(comp);
            mixer.Apply();
            // телеграф снимает «родные» цвета из MPB (Telegraph.cs:93)
            telegraph.Rebase();
            // после Rebase baseColors должны стать comp (если MPB не empty)
            var fiBase = typeof(Telegraph).GetField("baseColors", BindingFlags.NonPublic | BindingFlags.Instance);
            var baseColors = (Color[])fiBase.GetValue(telegraph);
            Assert.IsNotNull(baseColors);
            Assert.Greater(baseColors.Length, 0);
            // MPB был записан микшером, поэтому baseColors[0] должен быть comp (допуск по цвету)
            Assert.AreEqual(comp.r, baseColors[0].r, 0.02f, "Rebase должен снять текущий цвет-по-составу как родной");
            Assert.AreEqual(comp.g, baseColors[0].g, 0.02f);
            Assert.AreEqual(comp.b, baseColors[0].b, 0.02f);

            // активный замах не стирает состав при Reapply (CreatureBody.cs:25)
            telegraph.Set(true, Color.red, intent: true);
            Assert.IsTrue(telegraph.IsShowing, "Set(true) должен поднять IsShowing");
            mixer.Apply();
            telegraph.Rebase();
            telegraph.Reapply();
            Assert.IsTrue(telegraph.IsShowing, "Reapply не должен сбрасывать активный замах");

            // смена состава во время замаха: Rebase + Reapply сохраняют замах поверх нового состава
            Color comp2 = new Color(0.2f, 0.8f, 0.4f);
            mixer.SetComposition(comp2);
            mixer.Apply();
            telegraph.Rebase();
            telegraph.Reapply();
            Assert.IsTrue(telegraph.IsShowing);

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebuild_AfterSecondMorph_UpdatesCount()
        {
            var so = MakeSpecies();
            var so2 = MakeSpecies();
            so2.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Хвост", parent = "хребет", attach = 0f, baseSize = Vector3.one, linkDiameter = 0.1f, linkLength = 0.08f, linkTaper = 0.9f, chain = 2 },
            };
            var worn1 = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("TelegraphRebuild_Second");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            var tg = go.AddComponent<Telegraph>();
            var mixer = go.AddComponent<TintMixer>();
            MorphBuilder.Build(go.transform, so, worn1);
            mixer.Rebuild(); tg.RebuildRenderers();
            yield return null;
            int n1 = CountMeshRenderers(go.transform);
            // вторая сборка с хвостом — рендереров больше
            MorphBuilder.Build(go.transform, so2, worn1);
            mixer.Rebuild(); tg.RebuildRenderers();
            yield return null;
            int n2 = CountMeshRenderers(go.transform);
            Assert.GreaterOrEqual(n2, n1, "Вторая морф-сборка с доп сокетом должна дать >= рендереров");
            var fi = typeof(Telegraph).GetField("renderers", BindingFlags.NonPublic | BindingFlags.Instance);
            var rends = (Renderer[])fi.GetValue(tg);
            Assert.AreEqual(n2, rends.Length);

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            Object.DestroyImmediate(so2);
            yield return null;
        }
    }
}
