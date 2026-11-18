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
        bool sceneBeaconOn;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            b = Object.FindAnyObjectByType<ForestBuilder>();
            Assert.IsNotNull(b, "в павильоне нет ForestBuilder");
            foreach (var s in Object.FindObjectsByType<ForestSpawner>()) s.autoTick = false;
            // В сцене фонарь выключен (18.11); сторожа устройства включают его галкой — так, как его включит человек.
            sceneBeaconOn = b.buildBeacon;
            b.buildBeacon = true;
            b.ApplyBeacon();
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
            Assert.IsFalse(sceneBeaconOn, "в сцене павильона фонарь выключен по умолчанию (проверка смысла 18.11 не пройдена)");
            Assert.IsNotNull(b.Beacon, "маяк поставлен");
            Assert.IsTrue(b.Beacon.gameObject.activeInHierarchy, "маяк горит");
            Assert.IsTrue(b.TryGetExit(out Vector3 door, out _), "у мира есть дверь лаборатории");
            Vector3 mast = b.Beacon.transform.position;
            float off = Vector2.Distance(new Vector2(mast.x, mast.z), new Vector2(door.x, door.z));
            Assert.LessOrEqual(off, 8f, $"фонарь у двери выхода босса (на площадке перед скальным входом): в {off:0.0} м от неё");
            Assert.AreEqual(door.y, mast.y, 0.01f, "основание мачты — на земле у двери");
            Assert.AreEqual(b.BeaconSettings.height, b.Beacon.LampHeightAboveGround, 0.01f, "фонарь на высоте из поля");
            yield break;
        }

        /// <summary>
        /// Фонарь у двери (геймдизайнер 18.11: «максимум 5»): высота — из поля (5 м), размер — реальный с любого расстояния
        /// (подстройка угла выключена). Прежние сторожа «выше крон» и «угол на 900/1900 м» сняты вместе с ролью ориентира.
        /// </summary>
        [UnityTest]
        public IEnumerator Lamp_AtDoor_HeightFromField_RealSize()
        {
            Assert.IsNotNull(b.Beacon);
            Assert.AreEqual(5f, b.BeaconSettings.height, 1e-5f, "фонарь у двери — 5 м");
            Assert.AreEqual(b.BeaconSettings.height, b.Beacon.LampHeightAboveGround, 0.01f, "высота из поля");
            Assert.AreEqual(0f, b.BeaconSettings.minAngleDeg, 1e-6f, "подстройка угла выключена");
            var lamp = b.Beacon.Lamp;
            foreach (float d in new[] { 5f, 200f, 1900f })
            {
                b.Beacon.FitFor(lamp.position + Vector3.right * d);
                Assert.AreEqual(b.BeaconSettings.lampSize, lamp.lossyScale.x, 0.01f, $"с {d:0} м фонарь своего размера");
            }
            Assert.IsTrue(b.Beacon.Glow.enabled, "свет у фонаря есть");
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
    }
}
