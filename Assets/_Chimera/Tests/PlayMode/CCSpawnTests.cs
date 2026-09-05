using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class CCSpawnTests
    {
        GameObject MakePrefab(string name)
        {
            var prefab = new GameObject(name);
            var cc = prefab.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);
            prefab.AddComponent<CreatureBody>();
            return prefab;
        }

        [UnityTest]
        public IEnumerator Instantiate_WithPos_KeepsPosition_NotZero()
        {
            var prefab = MakePrefab("CC_Prefab_Pos");
            Vector3 pos = new Vector3(10f, 0.5f, -7f);
            Quaternion rot = Quaternion.Euler(0f, 45f, 0f);
            // правильный путь: позиция задаётся в самом Instantiate — CharacterController не сбрасывает в 0,0,0
            var instance = Object.Instantiate(prefab, pos, rot);
            yield return null;
            // CC перебивает transform.position если задавать ПОСЛЕ Instantiate, но не при перегрузке с pos
            Assert.AreEqual(pos.x, instance.transform.position.x, 0.05f, "Instantiate(prefab,pos,rot) должен сохранить x");
            Assert.AreEqual(pos.y, instance.transform.position.y, 0.2f, "Instantiate(prefab,pos,rot) должен сохранить y (CC может скорректировать по земле)");
            Assert.AreEqual(pos.z, instance.transform.position.z, 0.05f, "Instantiate(prefab,pos,rot) должен сохранить z");
            Assert.Greater(instance.transform.position.magnitude, 0.1f, "Спавн не должен улететь в 0,0,0");
            // rotation тоже сохраняется
            Assert.AreEqual(rot.eulerAngles.y, instance.transform.rotation.eulerAngles.y, 0.5f);

            Object.Destroy(instance);
            Object.Destroy(prefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Instantiate_WithCC_AtOrigin_StaysAtOrigin()
        {
            var prefab = MakePrefab("CC_Prefab_Origin");
            Vector3 pos = Vector3.zero;
            var instance = Object.Instantiate(prefab, pos, Quaternion.identity);
            yield return null;
            Assert.AreEqual(0f, instance.transform.position.x, 0.05f);
            Assert.AreEqual(0f, instance.transform.position.z, 0.05f);

            Object.Destroy(instance);
            Object.Destroy(prefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Instantiate_MultiplePositions_AllKept()
        {
            var prefab = MakePrefab("CC_Prefab_Multi");
            Vector3[] positions = { new Vector3(5f, 0f, 5f), new Vector3(-12f, 0f, 3f), new Vector3(0f, 0f, 15f) };
            foreach (var pos in positions)
            {
                var inst = Object.Instantiate(prefab, pos, Quaternion.identity);
                yield return null;
                Assert.AreEqual(pos.x, inst.transform.position.x, 0.05f, $"x для pos {pos}");
                Assert.AreEqual(pos.z, inst.transform.position.z, 0.05f, $"z для pos {pos}");
                Object.Destroy(inst);
                yield return null;
            }
            Object.Destroy(prefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Instantiate_ThenMove_DoesNotSnapToZero()
        {
            var prefab = MakePrefab("CC_Prefab_Move");
            Vector3 pos = new Vector3(8f, 0f, 8f);
            var inst = Object.Instantiate(prefab, pos, Quaternion.identity);
            yield return null;
            // последующее движение через CC.Move не сбрасывает в ноль
            var cc = inst.GetComponent<CharacterController>();
            Vector3 before = inst.transform.position;
            // лёгкий сдвиг
            cc.Move(new Vector3(1f, 0f, 0f) * Time.deltaTime);
            yield return null;
            Assert.Greater(inst.transform.position.x, 7f, "После CC.Move позиция должна остаться около спавна, не в нуле");
            Assert.AreNotEqual(0f, inst.transform.position.x, "Не должно сбросить в 0");

            Object.Destroy(inst);
            Object.Destroy(prefab);
            yield return null;
        }
    }
}
