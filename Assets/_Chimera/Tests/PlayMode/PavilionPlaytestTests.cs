using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Правки по плейтесту павильона (14.11): камера не лезет в стволы, подъём на сушу, дом по доминанте.</summary>
    public class PavilionPlaytestTests
    {
        readonly List<Object> trash = new List<Object>();

        T Track<T>(T o) where T : Object { trash.Add(o); return o; }

        [TearDown]
        public void Clean()
        {
            PlayerDeath.IsWaterAt = null; PlayerDeath.FallbackPoint = null;
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        CameraFollow MakeCamera(Transform tgt, out GameObject camGo)
        {
            camGo = Track(new GameObject("~камера", typeof(Camera)));
            var cam = camGo.AddComponent<CameraFollow>();
            typeof(CameraFollow).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cam, tgt);
            return cam;
        }

        // ── камера ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Camera_StopsBeforeTrunk_AndRecoversAfter()
        {
            var tgt = Track(new GameObject("~цель")).transform;
            tgt.position = new Vector3(0f, 0f, 700f);
            MakeCamera(tgt, out var camGo);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 10f, 696f);   // между головой (z 700) и камерой (z 693)
            wall.transform.localScale = new Vector3(10f, 20f, 1f);

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null; yield return null; yield return null;
            Assert.Greater(camGo.transform.position.z, 696.5f, "камера встала перед преградой, а не за ней");

            Object.Destroy(wall);
            yield return new WaitForSeconds(2f);
            Assert.Less(camGo.transform.position.z, 694f, "преграды нет — камера выехала на своё место");
        }

        [UnityTest]
        public IEnumerator Camera_IgnoresCreatureBodies()
        {
            var tgt = Track(new GameObject("~цель")).transform;
            tgt.position = new Vector3(0f, 0f, 800f);
            MakeCamera(tgt, out var camGo);
            var npc = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            npc.transform.position = new Vector3(0f, 4f, 796f);
            npc.transform.localScale = new Vector3(10f, 20f, 1f);
            npc.AddComponent<Health>();
            Physics.SyncTransforms();
            yield return null; yield return null; yield return null;
            Assert.Less(camGo.transform.position.z, 794f, "зверь камеру не толкает");
        }

        // ── подъём на сушу ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RisePoint_OffNavMesh_GoesToNearestWalkable()
        {
            var plane = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            plane.transform.position = new Vector3(1000f, -0.5f, 1000f);
            plane.transform.localScale = new Vector3(20f, 1f, 20f);   // x 990..1010
            var surf = plane.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.BuildNavMesh();
            yield return null;

            var on = PlayerDeath.RisePoint(new Vector3(1000f, 0f, 1000f));
            Assert.AreEqual(1000f, on.x, 0.01f, "на навмеше — остаёмся на точке смерти");

            var water = new Vector3(975f, 0f, 1000f);   // в 15 м от берега
            var up = PlayerDeath.RisePoint(water, 1f);
            Assert.GreaterOrEqual(up.x, 989.5f, "вынесло на проходимое");
            Assert.AreEqual(1f, up.y, 0.3f, "подошвы на земле: к точке прибавлен вылет корня");

            var far = PlayerDeath.RisePoint(new Vector3(500f, 0f, 1000f));
            Assert.AreEqual(500f, far.x, 0.01f, "дальше 30 м от навмеша — как раньше, на точку смерти");
        }

        // ── дом по доминанте ─────────────────────────────────────────────────

        SpeciesSO Species(string name)
        {
            var so = Track(ScriptableObject.CreateInstance<SpeciesSO>());
            so.speciesName = name; so.tint = Color.gray; so.mutagenPool = 100; so.baseHp = 60;
            so.baseStamina = 100; so.baseStaminaRegen = 10f; so.sockets = new BodySocket[0];
            so.organs = new[] { new Organ { organName = name + "-shard", slot = "Руки", cost = 2 } };
            return so;
        }

        LairSite Site(string species, Vector3 pos, int cap = 4)
        {
            var go = Track(new GameObject("~Логово-" + species));
            go.transform.position = pos;
            var s = go.AddComponent<LairSite>();
            s.speciesName = species; s.capacity = cap;
            return s;
        }

        [UnityTest]
        public IEnumerator Rehome_TakesNearestSuitableLair_SkipsFullAndForeign()
        {
            var wolf = Species("Волк");
            var far = Site("Волк", new Vector3(2000f, 0f, 2060f));
            var near = Site("Волк", new Vector3(2000f, 0f, 2020f));
            var hedge = Site("Ёж", new Vector3(2000f, 0f, 2005f));
            var body = ChimeraFactory.Spawn(wolf, new[] { wolf }, new Vector3(2000f, 0.5f, 2000f), "~волк");
            Track(body.gameObject);
            yield return null;
            Assert.AreSame(near, body.home, "психика взяла ближайшее логово своего вида, ёж не подошёл");

            hedge.Claim(body);   // дом на чужое логово (как после смены доминанты)
            Assert.AreSame(hedge, body.home);
            HomeLaw.Rehome(body);
            Assert.AreSame(near, body.home, "доминанта волк — дом пересмотрен на логово волка");
            Assert.AreEqual(0, hedge.population, "старый дом отпущен");
            Assert.AreEqual(1, near.population);

            near.capacity = 1;
            var other = ChimeraFactory.Spawn(wolf, new[] { wolf }, new Vector3(2000f, 0.5f, 2000f), "~волк2");
            Track(other.gameObject);
            yield return null;
            Assert.AreSame(far, other.home, "ближнее полно — берёт следующее подходящее");
        }

        [UnityTest]
        public IEnumerator Rehome_NoSuitableLair_LeavesBodyHomeless()
        {
            var wolf = Species("Волк");
            var hedge = Site("Ёж", new Vector3(3000f, 0f, 3000f));
            var body = ChimeraFactory.Spawn(wolf, new[] { wolf }, new Vector3(3000f, 0.5f, 3000f), "~волк");
            Track(body.gameObject);
            hedge.Claim(body);
            yield return null;
            HomeLaw.Rehome(body);
            Assert.IsNull(body.home, "логова вида-доминанты нет — бездомный");
            Assert.AreEqual(0, hedge.population);
        }
    }
}
