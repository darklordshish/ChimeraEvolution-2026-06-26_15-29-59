using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>БРЮХО ЗМЕИ НА ЗЕМЛЕ (поставка 13, 22.09). Центры звеньев стояли на 0.30 над путём при толщине тела 0.30 —
    /// змея ползла, вися над землёй на 0.15 по всей длине. Высота оси цепи — это РАДИУС самого толстого звена, и берётся
    /// она из сборки, а не числом: сменят толщину тела — ось поедет следом. Сборка тут нарочно ставит цепь на 0.40, чтобы
    /// проверить, что высоту даёт звено, а не место.</summary>
    public class SnakeChainGroundTests
    {
        [UnityTest]
        public IEnumerator ChainBelly_LiesOnGround_AxisIsThickestLinkRadius()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "~змея-на-земле";
            so.tint = Color.green;
            so.baseHp = 50;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.4f, 0f), solid = true, linkDiameter = 0.30f, linkLength = 0.36f, linkTaper = 0.9f, chain = 4 },
            };
            so.organs = new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true } };
            so.bones = new Bone[0];

            var go = new GameObject("~ЗмеяНаЗемле");
            go.transform.position = new Vector3(3f, 0f, -2f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 0.8f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.4f, 0f);   // корень змеи — на земле
            var chain = go.AddComponent<SnakeBodyChain>();
            MorphBuilder.Build(go.transform, so, so.organs.ToList());
            chain.RebuildFromMorph();
            yield return null;
            yield return null;   // LateUpdate расставил звенья по пути

            var links = go.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("хребет") && !(r is SkinnedMeshRenderer)).ToArray();
            Assert.Greater(links.Length, 0, "звеньев нет");
            float belly = links.Min(r => r.bounds.min.y);
            Assert.AreEqual(0f, belly, 0.01f, $"брюхо цепи на {belly:0.000} над землёй — ось цепи не равна радиусу звена");

            var fi = typeof(SnakeBodyChain).GetField("height", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.AreEqual(0.15f, (float)fi.GetValue(chain), 0.005f, "ось цепи — радиус самого толстого звена (0.30 / 2)");

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }
    }
}
