using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Ловушка: срабатывает на чужого (HP −урон, исход), владельца игнорирует.
    /// Болванка телепортируется (триггер, не детекция) + SyncTransforms + 2 кадра.
    /// Слайс s4c (ветка forest/s4c-snare-trap).
    /// </summary>
    public class TrapTests
    {
        readonly List<Object> trash = new List<Object>();

        static TrapDef Def(int uses = 1)
        {
            return new TrapDef { id = "snare", damage = 12, bleedStacks = 0, slowStacks = 0, radius = 2f, uses = uses };
        }

        GameObject Body(Vector3 pos, string name)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            trash.Add(go);
            go.AddComponent<Health>();
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = false;
            return go;
        }

        PlacedTrap Trap(Vector3 pos, GameObject ownerGo, TrapDef def)
        {
            var go = new GameObject("~Trap");
            go.transform.position = pos;
            trash.Add(go);
            var trap = go.AddComponent<PlacedTrap>();
            trap.Arm(ownerGo.GetComponent<Health>(), def);
            return trap;
        }

        [TearDown]
        public void Clean()
        {
            Time.timeScale = 1f;
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator Trigger_HitsVictim_Consumes()
        {
            var owner = Body(new Vector3(10f, 0f, 0f), "~Owner");
            var trapGo = new GameObject("~Trap");
            trash.Add(trapGo);
            var trap = trapGo.AddComponent<PlacedTrap>();
            trap.Arm(owner.GetComponent<Health>(), Def(2));
            var victim = Body(new Vector3(10f, 0f, 0f), "~Victim");
            var health = victim.GetComponent<Health>();
            float before = health.Current;

            victim.transform.position = trapGo.transform.position; // жертва внутри с кадра 0
            Physics.SyncTransforms();
            yield return null; // кадр 1: удар №1
            Assert.AreEqual(before - 12f, health.Current, 1e-3f, "кадр 1: паёк владельца");
            Assert.AreEqual(1, trap.UsesLeft, "заряд потрачен без сноса (2→1)");
            yield return null; // кадр 2: удар №2
            Assert.AreEqual(before - 24f, health.Current, 1e-3f, "кадр 2: второй заряд");
            Assert.AreEqual(0, trap.UsesLeft, "заряды исчерпаны");
            yield return null; // кадр 3: снос выполнен (Destroy отложен)
            Assert.IsTrue(trapGo == null, "исход — снос ловушки");
            yield return null; // кадр 4: посмертных ударов нет
            Assert.AreEqual(before - 24f, health.Current, 1e-3f, "пустая/снесённая не бьёт");
        }

        [UnityTest]
        public IEnumerator IgnoresOwner()
        {
            var owner = Body(new Vector3(0f, 0f, 0f), "~Owner");
            var ownerHealth = owner.GetComponent<Health>();
            float before = ownerHealth.Current;
            Trap(owner, Def(1));
            yield return null;
            yield return null;
            Assert.AreEqual(before, ownerHealth.Current, 1e-3f, "владелец в радиусе — цел");
        }

        PlacedTrap Trap(GameObject ownerGo, TrapDef def)
        {
            var go = new GameObject("~Trap");
            go.transform.position = ownerGo.transform.position;
            trash.Add(go);
            var trap = go.AddComponent<PlacedTrap>();
            trap.Arm(ownerGo.GetComponent<Health>(), def);
            return trap;
        }
    }
}
