using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Защита игрока от провала (опора + пол под ногами, точка расширения IGroundProbe), мёртвый держатель,
    /// кнопки геймпада и адрес выхода босса (письмо лесной линии 13.11).</summary>
    public class PlayerGuardTests
    {
        readonly List<Object> trash = new();

        [TearDown]
        public void Clean()
        {
            GroundProbe.Provider = null;
            BossChimeraSpawner.ExitProvider = null;
            foreach (var o in trash) if (o != null) Object.DestroyImmediate(o);
            trash.Clear();
        }

        GameObject Box(Vector3 center, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.position = center; g.transform.localScale = size;
            trash.Add(g); return g;
        }

        PlayerController Player(Vector3 pos)
        {
            var go = new GameObject("PG_Player");
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = Vector3.zero; cc.radius = 0.4f;
            go.AddComponent<Health>();
            var pc = go.AddComponent<PlayerController>();
            trash.Add(go);
            return pc;
        }

        // CharacterController перебивает transform.position — на время переноса гасим его
        static void Warp(PlayerController pc, Vector3 p)
        {
            var cc = pc.GetComponent<CharacterController>();
            cc.enabled = false; pc.transform.position = p; cc.enabled = true;
            cc.Move(Vector3.zero); // обновить isGrounded: без этого он помнит прошлый кадр
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator Descent_FromLedge_DoesNotTeleportUp()
        {
            Box(new Vector3(0, -0.5f, 0), new Vector3(200, 1, 200));           // земля, верх y=0
            Box(new Vector3(0, 4.5f, 0), new Vector3(6, 1, 6));                // уступ, верх y=5
            var pc = Player(new Vector3(0, 6.1f, 0));
            yield return Frames(30);
            Assert.AreEqual(6f, pc.transform.position.y, 0.3f, "встал на уступ");
            Warp(pc, new Vector3(20, 1.5f, 0)); // спуск на землю, глубже 3 м
            yield return Frames(40);
            Assert.Less(pc.transform.position.y, 2f, "после спуска игрока не вернули на высоту уступа");
            Assert.AreEqual(pc.transform.position.y, pc.SupportY, 0.3f, "опора = высота, где стоит");
        }

        [UnityTest]
        public IEnumerator Fall_IntoVoid_DeeperThan15m_ReturnsToSupport()
        {
            Box(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
            var pc = Player(new Vector3(0, 1.1f, 0));
            yield return Frames(30);
            float support = pc.SupportY;
            Warp(pc, new Vector3(300, support - 20f, 0)); // вне пола — пустота
            pc.GroundGuard();
            Assert.Greater(pc.transform.position.y, support - 1f, "вернули на последнюю опору");
        }

        [UnityTest]
        public IEnumerator Fall_ThroughFloor_PutsOnThatFloor()
        {
            Box(new Vector3(0, -0.5f, 0), new Vector3(200, 1, 200));           // земля, верх y=0
            var pc = Player(new Vector3(0, 1.1f, 0));
            yield return Frames(30);
            Warp(pc, new Vector3(0, -3f, 0)); // провалились под землю
            pc.GroundGuard();
            Assert.Greater(pc.transform.position.y, 0f, "поставили на пол, а не оставили под ним");
            Assert.Less(pc.transform.position.y, 3f);
        }

        class FixedFloor : IGroundProbe
        {
            public float y;
            public bool TryFloor(Vector3 from, out float floorY) { floorY = y; return true; }
        }

        [UnityTest]
        public IEnumerator CustomProvider_IsUsed()
        {
            var pc = Player(new Vector3(0, 0f, 0));
            yield return null;
            GroundProbe.Provider = new FixedFloor { y = -1f };
            Warp(pc, new Vector3(0, -3f, 0)); // ниже опоры и ниже пола поставщика
            pc.GroundGuard();
            Assert.Greater(pc.transform.position.y, -1f, "перенесён на пол, который назвал поставщик");
            Assert.Less(pc.transform.position.y, 1.5f);
        }

        class FakeGrabber : MonoBehaviour, IGrabber { public bool BreakFree(int d) => true; }

        [UnityTest]
        public IEnumerator DeadGrabber_DoesNotHold()
        {
            var pc = Player(new Vector3(0, 1f, 0));
            var g = new GameObject("PG_Grabber"); trash.Add(g);
            var fg = g.AddComponent<FakeGrabber>();
            pc.ApplyGrab(fg, 0.3f);
            Assert.IsTrue(pc.IsGrabbed, "живой держит");
            Object.DestroyImmediate(fg);
            Assert.IsFalse(pc.IsGrabbed, "уничтоженный не держит");

            var g2 = new GameObject("PG_Grabber2"); trash.Add(g2);
            var fg2 = g2.AddComponent<FakeGrabber>();
            fg2.enabled = false;
            pc.ApplyGrab(fg2, 0.3f);
            Assert.IsFalse(pc.IsGrabbed, "выключенного держателя не принимаем");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gamepad_NoButton_BoundToTwoActions()
        {
            var pc = Player(new Vector3(0, 1f, 0));
            pc.gameObject.AddComponent<PlayerInputDriver>();
            yield return null;
            var seen = new Dictionary<string, string>();
            foreach (var comp in pc.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                foreach (var f in comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    object v = f.GetValue(comp);
                    if (v is InputAction a) Collect(a, seen);
                    else if (v is IEnumerable<(InputAction action, int slot)> list)
                        foreach (var t in list) Collect(t.action, seen);
                }
            }
            Assert.Greater(seen.Count, 5, "действия найдены");
            Assert.AreEqual("Antler", seen.GetValueOrDefault("<Gamepad>/buttonNorth"), "buttonNorth — только рога");
        }

        static void Collect(InputAction a, Dictionary<string, string> seen)
        {
            foreach (var b in a.bindings)
            {
                if (string.IsNullOrEmpty(b.path) || !b.path.StartsWith("<Gamepad>")) continue;
                if (b.path.ToLowerInvariant().EndsWith("stick")) continue; // стики — оси
                // buttonNorth — только рога (сторож письма 13.11); остальные пересечения здесь не меряем
                if (b.path == "<Gamepad>/buttonNorth" && seen.TryGetValue(b.path, out var other) && other != a.name)
                    Assert.Fail($"{b.path} привязан к двум действиям: {other} и {a.name}");
                seen[b.path] = a.name;
            }
        }

        class FixedExit : IBossExit
        {
            public Vector3 pos;
            public bool TryGetExit(out Vector3 p, out Quaternion r) { p = pos; r = Quaternion.Euler(0, 90, 0); return true; }
        }

        [UnityTest]
        public IEnumerator BossExit_WithAddress_SpawnsThere_WithoutItNearPlayer()
        {
            Perception.PlayerGhost = true;
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null; yield return null;
            var boss = Object.FindAnyObjectByType<BossChimeraSpawner>();
            var pb = CreatureBody.PlayerBody;
            Assert.IsNotNull(boss); Assert.IsNotNull(pb);
            boss.AutoSpawn = false;

            Vector3 exit = pb.transform.position + new Vector3(60f, 0f, 0f);
            BossChimeraSpawner.ExitProvider = new FixedExit { pos = exit };
            var b1 = boss.SpawnNearPlayer(BossChimeraSpawner.Recipe.RandomBeasts);
            Assert.IsNotNull(b1);
            Assert.Less(Vector2.Distance(new Vector2(b1.transform.position.x, b1.transform.position.z), new Vector2(exit.x, exit.z)), 0.5f,
                "босс вышел по адресу");
            Assert.AreEqual(90f, b1.transform.rotation.eulerAngles.y, 1f, "и повёрнут по адресу");
            Object.Destroy(b1.gameObject);
            yield return null;

            BossChimeraSpawner.ExitProvider = null;
            var b2 = boss.SpawnNearPlayer(BossChimeraSpawner.Recipe.RandomBeasts);
            Assert.IsNotNull(b2);
            float d = Vector3.Distance(b2.transform.position, pb.transform.position);
            Assert.Less(d, 40f, "без адреса — рядом с игроком");
            Object.Destroy(b2.gameObject);
            Perception.PlayerGhost = false;
            yield return null;
        }
    }
}
