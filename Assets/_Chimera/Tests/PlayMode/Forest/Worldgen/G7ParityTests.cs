using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// G7 шаг 0: A/B — ОДИН мир. Скрывается только POI-слой, только
    /// `SetActive`; `Fingerprint`, пол, камера, fog/climate и множество
    /// активных объектов возвращаются к A. Список POI — единственный
    /// <see cref="ForestBuilder.PoiWhitelist"/>; `Tools/Agent` не используется.
    /// </summary>
    public class G7ParityTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        GameObject host;
        ForestBuilder builder;

        [UnityTest]
        public IEnumerator A_B_IsOneWorld_OnlyPoiActive()
        {
            host = new GameObject("~G7ParityWorld");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            host.SetActive(true);
            builder.Build();
            Assert.IsNotNull(builder.BuiltRoot, "A/B: мир не собран");
            Assert.IsNotNull(builder.Plan, "G7 шаг 1: мир v2 не выразил MapPlan");
            StringAssert.Contains("plan:", builder.Trace.Text, "в GenTrace нет слоя plan");
            Assert.AreEqual(2, builder.TreeVersion, "v2-профиль обязан нести treeVersion=2");
            yield return null;

            var root = builder.BuiltRoot;
            ulong floor0 = FloorHash(builder);
            ulong fp0 = builder.Fingerprint;
            var cam = MakeCamera();
            var cam0 = CameraSnapshot(cam);
            string settings0 = SettingsSnapshot();
            string climate0 = ClimateSnapshot();

            var before = ActiveState(root);
            int hidden = 0;
            var kinds = new HashSet<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string kind = builder.PoiKind(t.gameObject);
                if (kind == null) continue;
                Assert.IsTrue(ForestBuilder.IsPoiKind(kind),
                    $"A/B: скрыт объект вне PoiWhitelist: {t.name} ({kind})");
                t.gameObject.SetActive(false);
                hidden++;
                kinds.Add(kind);
            }
            Assert.Greater(hidden, 0, "A/B: POI-слой не найден");
            Assert.GreaterOrEqual(kinds.Count, 2, "A/B: скрыт только один POI-слой");

            // A: тот же BuiltRoot, тот же мир, камера/погода/пол не поехали.
            Assert.AreSame(root, builder.BuiltRoot, "A/B: BuiltRoot подменён");
            Assert.AreEqual(fp0, builder.Fingerprint, "A/B: Fingerprint изменился после HidePoi");
            Assert.AreEqual(floor0, FloorHash(builder), "A/B: SampleGround изменился после HidePoi");
            Assert.AreEqual(cam0, CameraSnapshot(cam), "A/B: камера/FOV изменились после HidePoi");
            Assert.AreEqual(settings0, SettingsSnapshot(), "A/B: RenderSettings изменились");
            Assert.AreEqual(climate0, ClimateSnapshot(), "A/B: ForestClimate изменился");
            yield return null;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (ForestBuilder.IsPoiKind(builder.PoiKind(t.gameObject)))
                    t.gameObject.SetActive(true);

            // B: полный возврат активных объектов; мир не пересобран.
            Assert.AreEqual(fp0, builder.Fingerprint, "A/B: Fingerprint изменился после ShowPoi");
            Assert.AreEqual(floor0, FloorHash(builder), "A/B: пол изменился после ShowPoi");
            Assert.AreEqual(cam0, CameraSnapshot(cam), "A/B: камера/FOV после ShowPoi");
            Assert.AreEqual(settings0, SettingsSnapshot(), "A/B: RenderSettings после ShowPoi");
            Assert.AreEqual(climate0, ClimateSnapshot(), "A/B: ForestClimate после ShowPoi");
            AssertActiveState(before, root);
            Assert.AreSame(root, builder.BuiltRoot, "A/B: BuiltRoot подменён на B");

            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            builder.Clear();
            UnityEngine.Object.Destroy(host);
            builder = null;
            host = null;
            yield return null;
        }

        static Camera MakeCamera()
        {
            var go = new GameObject("~G7ParityCam");
            var c = go.AddComponent<Camera>();
            c.transform.SetPositionAndRotation(new Vector3(12f, 30f, -7f),
                Quaternion.Euler(12f, 35f, 0f));
            c.fieldOfView = 60f;
            c.nearClipPlane = 0.3f;
            c.farClipPlane = 6000f;
            return c;
        }

        static string CameraSnapshot(Camera c) =>
            $"{c.transform.position.x:F4};{c.transform.position.y:F4};{c.transform.position.z:F4};" +
            $"{c.transform.eulerAngles.x:F4};{c.transform.eulerAngles.y:F4};" +
            $"{c.transform.eulerAngles.z:F4};fov={c.fieldOfView:F4};" +
            $"near={c.nearClipPlane:F4};far={c.farClipPlane:F4}";

        static string SettingsSnapshot() =>
            $"fog={(RenderSettings.fog ? 1 : 0)};density={RenderSettings.fogDensity:F8};" +
            $"mode={(int)RenderSettings.fogMode};color={Color(RenderSettings.fogColor)};" +
            $"start={RenderSettings.fogStartDistance:F4};end={RenderSettings.fogEndDistance:F4};" +
            $"ambient={Color(RenderSettings.ambientLight)};ambientMode={(int)RenderSettings.ambientMode}";

        static string ClimateSnapshot()
        {
            var c = UnityEngine.Object.FindAnyObjectByType<ForestClimate>();
            if (c == null) return "none";
            return $"{c.timeMinutes:F4};{c.timeScale:F4};{c.autoWeather};" +
                   $"{(int)c.currentKind};{c.WetAmount:F4};{c.FogAmount:F4}";
        }

        static string Color(UnityEngine.Color c) =>
            $"{c.r:F4},{c.g:F4},{c.b:F4},{c.a:F4}";

        static ulong FloorHash(ForestBuilder b)
        {
            ulong h = 14695981039346656037UL;
            for (int iz = 0; iz < 10; iz++)
                for (int ix = 0; ix < 10; ix++)
                {
                    float x = Mathf.Lerp(-900f, 900f, ix / 9f);
                    float z = Mathf.Lerp(-900f, 900f, iz / 9f);
                    uint u = BitConverter.ToUInt32(BitConverter.GetBytes(b.SampleGround(x, z)), 0);
                    h ^= u;
                    h *= 1099511628211UL;
                }
            return h;
        }

        static Dictionary<int, bool> ActiveState(GameObject root)
        {
            var map = new Dictionary<int, bool>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                map[t.gameObject.GetInstanceID()] = t.gameObject.activeSelf;
            return map;
        }

        static void AssertActiveState(Dictionary<int, bool> before, GameObject root)
        {
            int now = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int id = t.gameObject.GetInstanceID();
                if (!before.TryGetValue(id, out bool was))
                    Assert.Fail($"A/B: после ShowPoi появился объект {t.name}");
                Assert.AreEqual(was, t.gameObject.activeSelf,
                    $"A/B: activeSelf не вернулся у {t.name}");
                now++;
            }
            Assert.AreEqual(before.Count, now, "A/B: список объектов изменился");
        }
    }
}
