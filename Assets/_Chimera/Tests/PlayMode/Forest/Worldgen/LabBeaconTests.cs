using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// МАЯК ЛАБОРАТОРИИ (решение геймдизайнера 17.11, проба «фонарь на мачте»). Сторожит то, что меряется числом:
    /// мачта стоит над дверью, из которой выходит босс (`ForestBuilder` как `IBossExit`); фонарь выше самой высокой
    /// кроны леса; фонарь на шейдере без тумана; галка гасит маяк без пересборки мира; издалека фонарь не меньше
    /// заданного угла. Как он ЧИТАЕТСЯ — кадры `ForestLandmarkFrames`, не этот тест. Сцена — настоящий павильон.
    /// </summary>
    public class LabBeaconTests
    {
        const string SceneName = "ForestPavilion";
        ForestBuilder b;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            b = Object.FindAnyObjectByType<ForestBuilder>();
            Assert.IsNotNull(b, "в павильоне нет ForestBuilder");
            foreach (var s in Object.FindObjectsByType<ForestSpawner>()) s.autoTick = false;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Time.timeScale = 1f;
            ForestClimate.ResetStatic();
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Пусто-после-маяка"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Beacon_StandsOverLabDoor_AndIsLit()
        {
            Assert.IsTrue(b.buildBeacon, "в павильоне маяк включён (постановщик ForestPavilionFirstZone)");
            Assert.IsNotNull(b.Beacon, "маяк поставлен");
            Assert.IsTrue(b.Beacon.gameObject.activeInHierarchy, "маяк горит");
            Assert.IsTrue(b.TryGetExit(out Vector3 door, out _), "у мира есть дверь лаборатории");
            Vector3 mast = b.Beacon.transform.position;
            float off = Vector2.Distance(new Vector2(mast.x, mast.z), new Vector2(door.x, door.z));
            Assert.LessOrEqual(off, 3f, $"мачта над дверью выхода босса: в {off:0.0} м от неё");
            Assert.AreEqual(door.y, mast.y, 0.01f, "основание мачты — на земле у двери");
            Assert.AreEqual(b.BeaconSettings.height, b.Beacon.LampHeightAboveGround, 0.01f, "фонарь на высоте из поля");
            yield break;
        }

        [UnityTest]
        public IEnumerator Lamp_IsAboveEveryCrown()
        {
            Assert.IsNotNull(b.Beacon);
            var root = b.BuiltRoot.transform;
            Vector3 ash = b.HasAsh ? root.TransformPoint(new Vector3(b.Ash.pos.x, b.Ash.groundY, b.Ash.pos.y)) : Vector3.zero;
            float tallest = 0f; int trees = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name != "DomeFlora") continue; // деревья — по одному объекту, подлесок — пачками DomeFlora_*
                Vector3 c = r.bounds.center;
                // Ясень — нарочно выше всех (85 м): ориентир сам по себе, с маяком не соревнуется.
                if (b.HasAsh && Vector2.Distance(new Vector2(c.x, c.z), new Vector2(ash.x, ash.z)) < 100f) continue;
                Vector3 local = root.InverseTransformPoint(c);
                float ground = root.TransformPoint(new Vector3(local.x, b.SampleGround(local.x, local.z), local.z)).y;
                float h = r.bounds.max.y - ground;
                if (h > tallest) tallest = h;
                trees++;
            }
            Assert.Greater(trees, 500, "деревья леса найдены");
            float lamp = b.Beacon.LampHeightAboveGround;
            Debug.Log($"[Forest] маяк: фонарь {lamp:0.0} м, самая высокая крона {tallest:0.0} м (деревьев {trees})");
            Assert.Greater(lamp, tallest + 3f, $"фонарь ({lamp:0.0} м) выше самой высокой кроны ({tallest:0.0} м) с запасом 3 м");
            yield break;
        }

        [UnityTest]
        public IEnumerator Lamp_IgnoresFog()
        {
            Assert.IsNotNull(b.Beacon);
            var shader = b.Beacon.LampRenderer.sharedMaterial.shader;
            Assert.AreEqual(LabBeacon.ShaderName, shader.name, "фонарь — на шейдере маяка");
            Assert.IsTrue(shader.isSupported, "шейдер маяка компилируется");
            string path = Path.Combine(Application.dataPath, "_Chimera", "Resources", "Лес", "Маяк.shader");
            string src = File.ReadAllText(path);
            StringAssert.DoesNotContain("MixFog", src, "шейдер маяка не подмешивает туман");
            StringAssert.DoesNotContain("#pragma multi_compile_fog", src, "у шейдера маяка нет вариантов тумана");
            yield break;
        }

        [UnityTest]
        public IEnumerator Checkbox_TurnsBeaconOffAndOn()
        {
            Assert.IsNotNull(b.Beacon);
            b.buildBeacon = false;
            b.ApplyBeacon();
            Assert.IsFalse(b.Beacon.gameObject.activeInHierarchy, "галка снята — маяка нет");
            b.buildBeacon = true;
            b.beaconHeight = 30f;
            b.ApplyBeacon();
            Assert.IsTrue(b.Beacon.gameObject.activeInHierarchy, "галка стоит — маяк горит");
            Assert.AreEqual(30f, b.Beacon.LampHeightAboveGround, 0.01f, "высота из инспектора доходит до мачты без пересборки");
            yield break;
        }

        [UnityTest]
        public IEnumerator Lamp_KeepsMinimumAngle_FromFarCorner()
        {
            Assert.IsNotNull(b.Beacon);
            var lamp = b.Beacon.Lamp;
            float minAngle = b.BeaconSettings.minAngleDeg;
            foreach (float d in new[] { 50f, 900f, 1900f })
            {
                Vector3 eye = lamp.position + new Vector3(d, 0f, 0f);
                b.Beacon.FitFor(eye);
                float angle = 2f * Mathf.Atan(lamp.lossyScale.x * 0.5f / d) * Mathf.Rad2Deg;
                Assert.GreaterOrEqual(angle, minAngle - 1e-3f, $"с {d:0} м фонарь не меньше {minAngle}°");
            }
            b.Beacon.FitFor(lamp.position + Vector3.right * 5f);
            Assert.AreEqual(b.BeaconSettings.lampSize, lamp.lossyScale.x, 0.01f, "вблизи фонарь своего размера");
            yield break;
        }
    }
}
