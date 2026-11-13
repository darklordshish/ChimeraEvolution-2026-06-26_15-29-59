using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАДРЫ ОРИЕНТИРОВ первой зоны (14.11): что видит игрок, встав у входа в павильон. Настоящая сцена в Play — лес,
    /// туман и небо такие, как в игре. Камера на высоте глаз (1,7 м) в точке появления игрока смотрит на дверь
    /// лаборатории и на ясень; часы — день (8 ч) и ночь (21 ч). Кадры — `Docs/Диаграммы/Лес/Кадры/18-ориентиры/`.
    /// Это съёмка, а не сторож: [Explicit].
    /// </summary>
    public class ForestLandmarkFrames
    {
        const float Eye = 1.7f;

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Time.timeScale = 1f;
            ForestClimate.ResetStatic();
            var scene = SceneManager.GetSceneByName("ForestPavilion");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Пусто-после-кадров"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest, Explicit("съёмка — вручную"), Timeout(600000)]
        public IEnumerator Shoot_FromPlayerEye_DayAndNight()
        {
            yield return SceneManager.LoadSceneAsync("ForestPavilion", LoadSceneMode.Single);
            yield return null;
            var b = Object.FindAnyObjectByType<ForestBuilder>();
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var clock = Object.FindAnyObjectByType<WorldClock>();
            Assert.IsNotNull(b); Assert.IsNotNull(pc); Assert.IsNotNull(clock);
            foreach (var s in Object.FindObjectsByType<ForestSpawner>()) s.autoTick = false; // в кадре без зверей

            var cc = pc.GetComponent<CharacterController>();
            Vector3 eye = pc.transform.position + Vector3.up * ((cc != null ? cc.center.y - cc.height * 0.5f : 0f) + Eye);
            Assert.IsTrue(b.TryGetExit(out Vector3 lab, out _), "лаборатория");
            Vector3 ash = b.BuiltRoot.transform.TransformPoint(new Vector3(b.Ash.pos.x, b.Ash.groundY, b.Ash.pos.y));

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес", "Кадры", "18-ориентиры");
            Directory.CreateDirectory(dir);
            var go = new GameObject("~LandmarkCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 6000f;
            cam.enabled = false; // рендерим руками

            foreach (var (time, hour) in new[] { ("день", 8f), ("ночь", 21f) })
            {
                float now = clock.HourOfDay;
                float delta = Mathf.Repeat(hour - now, 24f);
                clock.Advance(delta / clock.HoursPerSecond);
                for (int i = 0; i < 40; i++) yield return null; // климат применяет небо раз в 0,5 с
                yield return new WaitForSeconds(0.7f);

                foreach (var (what, at) in new[] { ("лаборатория", lab + Vector3.up * 6f), ("ясень", ash + Vector3.up * 40f) })
                {
                    Vector3 flat = at - eye; flat.y = 0f;
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(-4f, 0f, 0f));
                    Save(cam, Path.Combine(dir, $"{what}-{time}.png"));
                }
            }
            Object.Destroy(go);
        }

        static void Save(Camera cam, string path)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
