#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>ПЕРЕСАДКА БЕЗ РЫВКА (спека двух слоёв, шаг 0): в Play тело с готовой моделью не пересобирает оболочку
    /// в кадре пересадки — живёт старой, пока фон считает новую, и подменяет сама. Первая сборка — сразу.</summary>
    public class MorphDeferredTests
    {
        [UnityTest]
        public IEnumerator Graft_KeepsOldBody_UntilBackgroundShellIsReady()
        {
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset"));
            human.speciesName = "Человек~отлож~" + System.Guid.NewGuid().ToString("N");   // свой ключ: кэш не подскажет ответ
            var wolf = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");

            var go = new GameObject("отложенная");
            go.AddComponent<Health>();
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            var body = go.AddComponent<CreatureBody>();
            body.Configure(human, new[] { wolf });
            body.ExpandPool(9999);
            yield return null;

            Mesh Legs() => go.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name == BodySlots.Legs)?.sharedMesh;
            var before = Legs();
            Assert.IsNotNull(before, "первая сборка не дала ног — спавн обязан строиться сразу");

            int slot = -1;
            for (int i = 0; i < body.SlotCount; i++) if (!body.GetSlot(i).chimera && body.GetSlot(i).slot == BodySlots.Legs) slot = i;
            int v = body.GetVariants(slot).FindIndex(x => x.species == "Волк" && x.slotType == BodySlots.Legs);
            Assert.IsTrue(body.Install(slot, v), "волчьи ноги не встали");

            Assert.AreSame(before, Legs(), "оболочка пересобрана в кадре пересадки — рывок на главном потоке");

            float until = Time.realtimeSinceStartup + 20f;
            while (Legs() == before && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreNotSame(before, Legs(), "фон досчитал, а подмены не было — тело осталось со старыми ногами");

            Object.Destroy(go);
            Object.Destroy(human);
        }
    }
}
#endif
