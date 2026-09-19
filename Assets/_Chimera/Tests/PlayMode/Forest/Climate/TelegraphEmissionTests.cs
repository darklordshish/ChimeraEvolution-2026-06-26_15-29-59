using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Второй канал телеграфа: в тумане emission осциллирует, в ясную и на статусах — чёрный.
    /// Читаем MPB обратно (прецедент Rebase). Insight пиним (иначе veil), сбрасываем в TearDown.
    /// Чтения разнесены на полупериод (0.25с при 2Гц) — детерминированно разные фазы.
    /// Слайс s3d (ветка forest/s3d-emission-outline).
    /// </summary>
    public class TelegraphEmissionTests
    {
        readonly List<Object> trash = new List<Object>();
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        GameObject Rig()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "~EmissionRig";
            trash.Add(go);
            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.EnableKeyword("_EMISSION");
            renderer.sharedMaterial = mat;
            trash.Add(mat);
            var telegraph = go.AddComponent<Telegraph>();
            telegraph.RebuildRenderers();
            return go;
        }

        static Color ReadEmission(GameObject go)
        {
            var mpb = new MaterialPropertyBlock();
            go.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            return mpb.GetColor(EmissionColor);
        }

        static TelegraphChannels Channels()
        {
            return new TelegraphChannels { pulseColor = Color.white, pulseAmp = 1f, pulseFreq = 2f };
        }

        [TearDown]
        public void Clean()
        {
            ForestClimate.ResetStatic();
            Perception.Insight = false;
            Time.timeScale = 1f;
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator Emission_OscillatesInFog()
        {
            Perception.Insight = true;
            ForestClimate.CurrentState = new WeatherState { kind = WeatherKind.Fog };
            ForestClimate.CurrentTimeMinutes = 10f;
            var go = Rig();
            var telegraph = go.GetComponent<Telegraph>();
            telegraph.SetPulse(Channels());
            telegraph.Set(true, Color.red, true);
            yield return null;
            // спред по 6 семплам за ~0.5с (двухточечный замер — лотерея фазы)
            var samples = new List<Color>();
            for (int i = 0; i < 6; i++)
            {
                samples.Add(ReadEmission(go));
                yield return new WaitForSecondsRealtime(0.1f);
            }
            float spread = 0f;
            float brightness = 0f;
            foreach (var s in samples) brightness = Mathf.Max(brightness, s.r + s.g + s.b);
            for (int i = 0; i < samples.Count; i++)
                for (int j = i + 1; j < samples.Count; j++)
                    spread = Mathf.Max(spread, Vector4.Distance(samples[i], samples[j]));
            Assert.Greater(brightness, 0.01f, "в тумане emission не чёрный");
            Assert.Greater(spread, 0.1f, "осцилляция видна в разбросе семплов");
        }

        [UnityTest]
        public IEnumerator Emission_BlackInClear()
        {
            Perception.Insight = true;
            ForestClimate.ResetStatic();
            var go = Rig();
            var telegraph = go.GetComponent<Telegraph>();
            telegraph.SetPulse(Channels());
            telegraph.Set(true, Color.red, true);
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            Color b = ReadEmission(go);
            Assert.Less(b.r + b.g + b.b, 0.01f, "в ясную emission чёрный");
        }

        [UnityTest]
        public IEnumerator Emission_BlackForStatuses()
        {
            Perception.Insight = true;
            ForestClimate.CurrentState = new WeatherState { kind = WeatherKind.Fog };
            ForestClimate.CurrentTimeMinutes = 10f;
            var go = Rig();
            var telegraph = go.GetComponent<Telegraph>();
            telegraph.SetPulse(Channels());
            telegraph.Set(true, Color.white, false);
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            Color b = ReadEmission(go);
            Assert.Less(b.r + b.g + b.b, 0.01f, "статусы (intent=false) не светятся");
        }
    }
}
