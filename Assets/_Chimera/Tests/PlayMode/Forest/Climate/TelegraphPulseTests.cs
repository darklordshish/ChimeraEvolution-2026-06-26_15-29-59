using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Пульс-акцент телеграфа: в тумане замах осциллирует, в ясную — статика,
    /// статусы не пульсируют. Читаем MPB обратно (прецедент Rebase).
    /// Слайс s3c (ветка forest/s3c-telegraph-outline).
    /// </summary>
    public class TelegraphPulseTests
    {
        readonly List<Object> trash = new List<Object>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        GameObject Rig(Color bodyColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "~TelegraphRig";
            trash.Add(go);
            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = bodyColor;
            renderer.sharedMaterial = mat;
            trash.Add(mat);
            var telegraph = go.AddComponent<Telegraph>();
            telegraph.RebuildRenderers();
            return go;
        }

        static Color ReadApplied(GameObject go)
        {
            var mpb = new MaterialPropertyBlock();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(mpb);
            return mpb.isEmpty ? new Color(-1f, -1f, -1f, -1f) : mpb.GetColor(BaseColor);
        }

        static TelegraphChannels Channels()
        {
            return new TelegraphChannels { pulseColor = Color.white, pulseAmp = 1f, pulseFreq = 2f };
        }

        [TearDown]
        public void Clean()
        {
            ForestClimate.ResetStatic();
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator Pulse_OscillatesInFog()
        {
            ForestClimate.CurrentState = new WeatherState { kind = WeatherKind.Fog };
            ForestClimate.CurrentTimeMinutes = 10f;
            var go = Rig(Color.gray);
            var telegraph = go.GetComponent<Telegraph>();
            telegraph.SetPulse(Channels());
            telegraph.Set(true, Color.red, true);
            yield return null;
            // спред по 6 семплам за ~0.5с (двухточечный замер — лотерея фазы)
            var samples = new System.Collections.Generic.List<Color>();
            for (int i = 0; i < 6; i++)
            {
                samples.Add(ReadApplied(go));
                yield return new WaitForSecondsRealtime(0.1f);
            }
            float spread = 0f;
            for (int i = 0; i < samples.Count; i++)
                for (int j = i + 1; j < samples.Count; j++)
                    spread = Mathf.Max(spread, Vector4.Distance(samples[i], samples[j]));
            Assert.Greater(spread, 0.1f,
                "в тумане применённый цвет осциллирует (макс. разброс семплов)");
        }

        [UnityTest]
        public IEnumerator NoPulse_InClear()
        {
            ForestClimate.ResetStatic();
            var go = Rig(Color.gray);
            var telegraph = go.GetComponent<Telegraph>();
            telegraph.SetPulse(Channels());
            telegraph.Set(true, Color.red, true);
            yield return null;
            Color a = ReadApplied(go);
            yield return new WaitForSecondsRealtime(0.3f);
            Color b = ReadApplied(go);
            Assert.AreEqual(a.r, b.r, 1e-4f, "в ясную — статика");
            Assert.AreEqual(a.g, b.g, 1e-4f);
            Assert.AreEqual(a.b, b.b, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Statuses_DontPulse()
        {
            ForestClimate.CurrentState = new WeatherState { kind = WeatherKind.Fog };
            ForestClimate.CurrentTimeMinutes = 10f;
            var go = Rig(Color.gray);
            var telegraph = go.GetComponent<Telegraph>();
            telegraph.SetPulse(Channels());
            telegraph.Set(true, Color.white, false);
            yield return null;
            Color a = ReadApplied(go);
            yield return new WaitForSecondsRealtime(0.3f);
            Color b = ReadApplied(go);
            Assert.AreEqual(a.r, b.r, 1e-4f, "статусы (intent=false) не пульсируют");
            Assert.AreEqual(a.g, b.g, 1e-4f);
            Assert.AreEqual(a.b, b.b, 1e-4f);
        }
    }
}
