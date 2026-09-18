using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Хук рук: предмет бьёт вместо записи (спереди/сзади/сломанный/двуручное).
    /// Изолированные тело+болванка на кейс, полный удар по Health (before/after Current),
    /// хитстоп отпускаем в TearDown, темп ждём. Слайс s4b (ветка forest/s4b-hands-hook).
    /// </summary>
    public class HandsBridgeTests
    {
        readonly List<Object> trash = new List<Object>();

        static HandItemDef Spear(bool twoHanded = false)
        {
            return new HandItemDef
            {
                id = "spear", kind = HandItemKind.Spear, tier = 1,
                damage = 8, range = 2.2f, halfAngle = 35f,
                staminaCost = 20f, cooldown = 1.2f, windupTime = 0.5f,
                backstabMult = 3f, durabilityMax = 40, twoHanded = twoHanded
            };
        }

        static HandItemDef Torch()
        {
            return new HandItemDef
            {
                id = "torch", kind = HandItemKind.Torch, tier = 1,
                damage = 2, range = 1.6f, halfAngle = 60f,
                backstabMult = 1f, fuelSeconds = 120f, lightRadius = 8f
            };
        }

        GameObject Fighter(Vector3 pos, Vector3 forward)
        {
            var go = new GameObject("~Fighter");
            go.transform.position = pos;
            go.transform.forward = forward;
            trash.Add(go);
            go.AddComponent<Health>();
            var attack = go.AddComponent<PlayerAttack>();
            attack.Configure(new LimbStrikeData { damage = 5, range = 1.6f, halfAngle = 60f });
            var bridge = go.AddComponent<HandsBridge>();
            bridge.power = 1f;
            return go;
        }

        GameObject Dummy(Vector3 pos)
        {
            var go = new GameObject("~Dummy");
            go.transform.position = pos;
            trash.Add(go);
            go.AddComponent<Health>();
            go.AddComponent<SphereCollider>(); // скан идёт OverlapSphere — без коллайдера болванку не видно
            return go;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        IEnumerator StrikeOnce(GameObject fighter)
        {
            yield return null;
            Physics.SyncTransforms();
            Assert.IsTrue(fighter.GetComponent<PlayerAttack>().TryUse(), "замах обязан начаться");
            yield return new WaitForSecondsRealtime(0.15f);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Spear_Front()
        {
            var fighter = Fighter(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));
            fighter.GetComponent<HandsBridge>().EquipMain(Spear());
            var dummy = Dummy(new Vector3(0f, 0f, 1.5f));
            float before = dummy.GetComponent<Health>().Current;
            yield return StrikeOnce(fighter);
            Assert.AreEqual(before - 8f, dummy.GetComponent<Health>().Current, 1e-3f,
                "копьё спереди: базовый урон 8");
        }

        [UnityTest]
        public IEnumerator Spear_BackstabX3()
        {
            var fighter = Fighter(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));
            var bridge = fighter.GetComponent<HandsBridge>();
            bridge.EquipMain(Spear());
            bridge.IsBackstab = true;
            var dummy = Dummy(new Vector3(0f, 0f, 1.5f));
            float before = dummy.GetComponent<Health>().Current;
            yield return StrikeOnce(fighter);
            Assert.AreEqual(before - 24f, dummy.GetComponent<Health>().Current, 1e-3f,
                "копьё в спину: 8×3=24 (флаг снаружи, TODO(чувства))");
        }

        [UnityTest]
        public IEnumerator Broken_FallsBackToBare10()
        {
            var spear = Spear();
            spear.durabilityMax = 1;
            var fighter = Fighter(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));
            fighter.GetComponent<HandsBridge>().EquipMain(spear);
            var dummy = Dummy(new Vector3(0f, 0f, 1.5f));
            var health = dummy.GetComponent<Health>();
            float before = health.Current;
            yield return StrikeOnce(fighter); // первый замах: копьё 8, ломается
            yield return new WaitForSecondsRealtime(0.6f); // темп 0.45 + запас
            yield return StrikeOnce(fighter); // второй: голые 10
            Assert.AreEqual(before - 18f, health.Current, 1e-3f, "8 копьём + 10 голыми");
        }

        [UnityTest]
        public IEnumerator TwoHanded_MutesOffhand()
        {
            var fighter = Fighter(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));
            var bridge = fighter.GetComponent<HandsBridge>();
            Assert.IsTrue(bridge.EquipMain(Spear()), "одноручное в main");
            Assert.IsTrue(bridge.EquipOff(Torch()), "факел в off доступен");
            Assert.IsTrue(bridge.EquipMain(Spear(true)), "двуручное в main");
            Assert.IsFalse(bridge.CanEquip(HandsLoadout.HandSlot.Off, Torch()), "двуручное глушит off");
            var dummy = Dummy(new Vector3(0f, 0f, 1.5f));
            float before = dummy.GetComponent<Health>().Current;
            yield return StrikeOnce(fighter);
            Assert.AreEqual(before - 8f, dummy.GetComponent<Health>().Current, 1e-3f,
                "бьёт main-предмет");
        }
    }
}
