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
    /// С 17.11 — и МАЯК (проба геймдизайнера «фонарь на мачте»): от входа и из дальнего угла купола, днём, ночью и в
    /// густой туман; камера смотрит прямо на фонарь. Это съёмка, а не сторож: [Explicit].
    /// </summary>
    public class ForestLandmarkFrames
    {
        const float Eye = 1.7f;
        const float FarCorner = 850f; // от центра купола в сторону, противоположную лаборатории (кольцо скал — с 900 м)

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
                yield return SetHour(clock, hour);
                foreach (var (what, at) in new[] { ("лаборатория", lab + Vector3.up * 6f), ("ясень", ash + Vector3.up * 40f) })
                {
                    Vector3 flat = at - eye; flat.y = 0f;
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(-4f, 0f, 0f));
                    Save(cam, Path.Combine(dir, $"{what}-{time}.png"));
                }
            }
            Object.Destroy(go);
        }

        /// <summary>МАЯК: от входа и из дальнего угла; день, ночь, густой туман (днём). Камера — прямо на фонарь.</summary>
        [UnityTest, Explicit("съёмка — вручную"), Timeout(600000)]
        public IEnumerator Shoot_Beacon_FromEntryAndFarCorner()
        {
            yield return SceneManager.LoadSceneAsync("ForestPavilion", LoadSceneMode.Single);
            yield return null;
            var b = Object.FindAnyObjectByType<ForestBuilder>();
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var clock = Object.FindAnyObjectByType<WorldClock>();
            var climate = Object.FindAnyObjectByType<ForestClimate>();
            Assert.IsNotNull(b); Assert.IsNotNull(pc); Assert.IsNotNull(clock); Assert.IsNotNull(climate);
            Assert.IsNotNull(b.Beacon, "маяк поставлен");
            b.buildBeacon = true; b.ApplyBeacon(); // в сцене выключен с 18.11 — съёмка включает галкой
            foreach (var s in Object.FindObjectsByType<ForestSpawner>()) s.autoTick = false;
            climate.autoWeather = false;

            var cc = pc.GetComponent<CharacterController>();
            Vector3 entry = pc.transform.position + Vector3.up * ((cc != null ? cc.center.y - cc.height * 0.5f : 0f) + Eye);
            var root = b.BuiltRoot.transform;
            Vector3 labLocal = root.InverseTransformPoint(b.Beacon.transform.position);
            Vector2 away = -new Vector2(labLocal.x, labLocal.z).normalized;
            Vector2 far = away * FarCorner;
            for (int k = 0; k < 24 && b.IsWater(root.TransformPoint(new Vector3(far.x, b.SampleGround(far.x, far.y), far.y))); k++)
                far = (Vector2)(Quaternion.Euler(0f, 0f, 7.5f * (k % 2 == 0 ? k + 1 : -(k + 1))) * (Vector3)(away * FarCorner));
            Vector3 corner = root.TransformPoint(new Vector3(far.x, b.SampleGround(far.x, far.y) + Eye, far.y));
            Vector3 lamp = b.Beacon.Lamp.position;
            Debug.Log($"[Forest] кадры маяка: от входа {Vector3.Distance(entry, lamp):0} м, из угла {Vector3.Distance(corner, lamp):0} м");

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес", "Кадры", "18-ориентиры");
            Directory.CreateDirectory(dir);
            var go = new GameObject("~BeaconCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 6000f;
            cam.enabled = false;

            foreach (var (time, hour, weather) in new[]
                     { ("день", 8f, WeatherKind.Clear), ("ночь", 21f, WeatherKind.Clear), ("туман", 8f, WeatherKind.Fog) })
            {
                climate.SetWeatherImmediate(weather);
                yield return SetHour(clock, hour);
                foreach (var (where, eye) in new[] { ("вход", entry), ("угол", corner) })
                {
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lamp - eye));
                    Save(cam, Path.Combine(dir, $"маяк-{where}-{time}.png"));
                }
            }
            Object.Destroy(go);
        }

        /// <summary>
        /// РЯД ВЫСОТ МАЯКА: с какой высоты фонарь виден от входа и из дальнего угла. Ночью, в ясную погоду камера смотрит
        /// на фонарь; считаются пиксели цвета фонаря в окне 120×120 у центра (0 — фонарь закрыт рельефом или кронами).
        /// Отчёт — `Docs/Диаграммы/Лес/МАЯК.md`, кадры ряда — `18-ориентиры/ряд/`.
        /// </summary>
        [UnityTest, Explicit("замер — вручную"), Timeout(600000)]
        public IEnumerator Measure_Beacon_HeightRow()
        {
            yield return SceneManager.LoadSceneAsync("ForestPavilion", LoadSceneMode.Single);
            yield return null;
            var b = Object.FindAnyObjectByType<ForestBuilder>();
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var clock = Object.FindAnyObjectByType<WorldClock>();
            var climate = Object.FindAnyObjectByType<ForestClimate>();
            Assert.IsNotNull(b.Beacon, "маяк поставлен");
            b.buildBeacon = true; b.ApplyBeacon(); // в сцене выключен с 18.11 — съёмка включает галкой
            foreach (var s in Object.FindObjectsByType<ForestSpawner>()) s.autoTick = false;
            climate.autoWeather = false;
            climate.SetWeatherImmediate(WeatherKind.Clear);

            var cc = pc.GetComponent<CharacterController>();
            Vector3 entry = pc.transform.position + Vector3.up * ((cc != null ? cc.center.y - cc.height * 0.5f : 0f) + Eye);
            var root = b.BuiltRoot.transform;
            Vector3 labLocal = root.InverseTransformPoint(b.Beacon.transform.position);
            Vector2 far = -new Vector2(labLocal.x, labLocal.z).normalized * FarCorner;
            Vector3 corner = root.TransformPoint(new Vector3(far.x, b.SampleGround(far.x, far.y) + Eye, far.y));

            string docs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес");
            string rowDir = Path.Combine(docs, "Кадры", "18-ориентиры", "ряд");
            Directory.CreateDirectory(rowDir);
            var go = new GameObject("~BeaconRowCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 6000f;
            cam.enabled = false;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Лес: маяк лаборатории — ряд высот");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestLandmarkFrames.Measure_Beacon_HeightRow` (PlayMode, [Explicit]); руками не правится.");
            sb.AppendLine("Сцена `ForestPavilion`, ночь 21 ч, ясно. Камера на высоте глаз смотрит на фонарь; «пикселей» — точки цвета фонаря");
            sb.AppendLine($"в окне 120×120 у центра кадра 1280×720 (0 — фонарь закрыт рельефом или кронами). Фонарь не меньше {b.BeaconSettings.minAngleDeg}°.");
            sb.AppendLine();
            sb.AppendLine($"| высота, м | от входа ({Vector3.Distance(entry, b.Beacon.transform.position):0} м), пикселей | из угла ({Vector3.Distance(corner, b.Beacon.transform.position):0} м), пикселей |");
            sb.AppendLine("|---|---|---|");

            yield return SetHour(clock, 21f);
            float keep = b.beaconHeight;
            foreach (float h in new[] { 25f, 35f, 45f, 50f, 55f, 70f })
            {
                b.beaconHeight = h;
                b.ApplyBeacon();
                yield return null;
                var counts = new int[2];
                int i = 0;
                foreach (var (where, eye) in new[] { ("вход", entry), ("угол", corner) })
                {
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(b.Beacon.Lamp.position - eye));
                    var tex = Render(cam);
                    counts[i++] = LampPixels(tex);
                    File.WriteAllBytes(Path.Combine(rowDir, $"маяк-{h:0}м-{where}-ночь.png"), tex.EncodeToPNG());
                    Object.Destroy(tex);
                }
                sb.AppendLine($"| {h:0} | {counts[0]} | {counts[1]} |");
            }
            b.beaconHeight = keep;
            b.ApplyBeacon();
            Object.Destroy(go);
            File.WriteAllText(Path.Combine(docs, "МАЯК.md"), sb.ToString(), new System.Text.UTF8Encoding(false));
            Debug.Log("[Forest] ряд высот маяка:\n" + sb);
        }

        /// <summary>
        /// ФОНАРЬ У ДВЕРИ (геймдизайнер 18.11: маяк 5 м, реального размера). Проверка смысла: ночь 21 ч, ясно; камера на высоте
        /// глаз в 50, 100 и 200 м от двери вдоль двух направлений — «по дороге» (больше всего клеток дороги и тропы растра
        /// карты в 200 м) и «сквозь лес» (больше всего клеток леса) — смотрит на фонарь. Пиксели фонаря — как в ряду высот.
        /// Отчёт — `Docs/Диаграммы/Лес/МАЯК-ДВЕРЬ.md`, кадры — `18-ориентиры/дверь/`.
        /// </summary>
        [UnityTest, Explicit("съёмка — вручную"), Timeout(600000)]
        public IEnumerator Shoot_DoorLamp_Night()
        {
            yield return SceneManager.LoadSceneAsync("ForestPavilion", LoadSceneMode.Single);
            yield return null;
            var b = Object.FindAnyObjectByType<ForestBuilder>();
            var clock = Object.FindAnyObjectByType<WorldClock>();
            var climate = Object.FindAnyObjectByType<ForestClimate>();
            Assert.IsNotNull(b.Beacon, "фонарь поставлен");
            Assert.IsNotNull(b.Map, "у павильона v2 есть карта");
            foreach (var s in Object.FindObjectsByType<ForestSpawner>()) s.autoTick = false;
            climate.autoWeather = false;
            climate.SetWeatherImmediate(WeatherKind.Clear);
            b.buildBeacon = true; b.beaconHeight = 5f; b.beaconMinAngle = 0f; b.ApplyBeacon(); // фонарь у двери — галкой, в сцене он выключен

            var raster = ForestMapRaster.Build(b.Map);
            var root = b.BuiltRoot.transform;
            Vector3 lampLocal = root.InverseTransformPoint(b.Beacon.Lamp.position);
            var door = new Vector2(lampLocal.x, lampLocal.z);
            Vector2 Dir(int k) => new Vector2(Mathf.Cos(k * Mathf.PI / 8f), Mathf.Sin(k * Mathf.PI / 8f));
            int Score(int k, System.Func<ForestMapRaster.Surface, bool> want)
            {
                int n = 0;
                for (float d = 10f; d <= 200f; d += 4f)
                {
                    if (!raster.Cell(door + Dir(k) * d, out int ix, out int iz)) continue;
                    int i = raster.Index(ix, iz);
                    if (!raster.Passable(i) || raster.cls[i] == ForestMapRaster.Cls.Water) return -1; // упирается в стену или воду
                    if (want(raster.surface[i])) n++;
                }
                return n;
            }
            int road = 0, forest = 0;
            for (int k = 1; k < 16; k++)
            {
                if (Score(k, s => s == ForestMapRaster.Surface.Road || s == ForestMapRaster.Surface.Trail) >
                    Score(road, s => s == ForestMapRaster.Surface.Road || s == ForestMapRaster.Surface.Trail)) road = k;
                if (Score(k, s => s == ForestMapRaster.Surface.Forest) > Score(forest, s => s == ForestMapRaster.Surface.Forest)) forest = k;
            }

            string docs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес");
            string dir = Path.Combine(docs, "Кадры", "18-ориентиры", "дверь");
            Directory.CreateDirectory(dir);
            var go = new GameObject("~DoorLampCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 6000f;
            cam.enabled = false;
            yield return SetHour(clock, 21f);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Лес: фонарь у двери лаборатории — видно ли ночью");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestLandmarkFrames.Shoot_DoorLamp_Night` ([Explicit]); руками не правится.");
            sb.AppendLine($"Павильон v2, ночь 21 ч, ясно. Фонарь {b.BeaconSettings.height:0} м, {b.BeaconSettings.lampSize:0.0} м реального размера, свет {b.BeaconSettings.glowRange:0} м.");
            sb.AppendLine("Камера на высоте глаз смотрит на фонарь; «пикселей» — точки цвета фонаря в окне 120×120 у центра кадра 1280×720.");
            sb.AppendLine($"Направления по растру карты: «по дороге» — {road * 22.5f:0}°, «сквозь лес» — {forest * 22.5f:0}° (от востока против часовой).");
            sb.AppendLine();
            sb.AppendLine("| откуда | 50 м | 100 м | 200 м |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var (what, k) in new[] { ("по дороге", road), ("сквозь лес", forest) })
            {
                var row = new System.Text.StringBuilder($"| {what} |");
                foreach (float d in new[] { 50f, 100f, 200f })
                {
                    var p = door + Dir(k) * d;
                    Vector3 eye = root.TransformPoint(new Vector3(p.x, b.Map.relief.Height(p.x, p.y) + Eye, p.y));
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(b.Beacon.Lamp.position - eye));
                    var tex = Render(cam);
                    int px = LampPixels(tex);
                    File.WriteAllBytes(Path.Combine(dir, $"фонарь-{(what == "по дороге" ? "дорога" : "лес")}-{d:0}м-ночь.png"), tex.EncodeToPNG());
                    Object.Destroy(tex);
                    row.Append($" {px} |");
                }
                sb.AppendLine(row.ToString());
            }
            Object.Destroy(go);
            File.WriteAllText(Path.Combine(docs, "МАЯК-ДВЕРЬ.md"), sb.ToString(), new System.Text.UTF8Encoding(false));
            Debug.Log("[Forest] фонарь у двери: " + sb);
        }

        /// <summary>Точки цвета фонаря (тёплый: красный ярок, синий слаб) в окне 120×120 у центра кадра.</summary>
        static int LampPixels(Texture2D tex)
        {
            int n = 0;
            for (int y = tex.height / 2 - 60; y < tex.height / 2 + 60; y++)
                for (int x = tex.width / 2 - 60; x < tex.width / 2 + 60; x++)
                {
                    Color c = tex.GetPixel(x, y);
                    if (c.r > 0.7f && c.r - c.b > 0.4f && c.g > 0.25f) n++;
                }
            return n;
        }

        static IEnumerator SetHour(WorldClock clock, float hour)
        {
            float delta = Mathf.Repeat(hour - clock.HourOfDay, 24f);
            clock.Advance(delta / clock.HoursPerSecond);
            for (int i = 0; i < 40; i++) yield return null; // климат применяет небо раз в 0,5 с
            yield return new WaitForSeconds(0.7f);
        }

        static void Save(Camera cam, string path)
        {
            var tex = Render(cam);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static Texture2D Render(Camera cam)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(rt);
            return tex;
        }
    }
}
