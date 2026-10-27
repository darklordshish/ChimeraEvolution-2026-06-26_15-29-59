using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>БАГИ МЕХАНИК ИЗ АУДИТА ДИЗАЙН-ЛИНИИ (26.10, §10): общий множитель исходящего урона у всех доставок, восстановление
    /// HP сквозь подавление регена, выбор добычи по наблюдению.</summary>
    public class OutgoingDamageTests
    {
        readonly List<GameObject> made = new();
        GameObject New(string n) { var g = new GameObject(n); made.Add(g); return g; }

        [TearDown] public void TearDown() { foreach (var g in made) if (g != null) Object.DestroyImmediate(g); made.Clear(); }

        /// <summary>Ярость делателя поднимает урон любой его доставки — и игрока тоже (клич человека давал ярость, которая
        /// повышала только входящий урон).</summary>
        [Test]
        public void Rage_RaisesOutgoingDamage_OfAnyDealer()
        {
            var dealer = New("~делатель");
            Assert.AreEqual(1f, OutgoingDamage.Mult(dealer.transform), 1e-5f, "без статусов — ×1");
            var rage = dealer.AddComponent<Rage>();
            rage.Enrage(5f);
            Assert.Greater(rage.DamageMult, 1f, "ярость не включилась — проверять нечего");
            Assert.AreEqual(rage.DamageMult, OutgoingDamage.Mult(dealer.transform), 1e-5f);
            Assert.AreEqual(Mathf.RoundToInt(10 * rage.DamageMult), OutgoingDamage.Apply(dealer.transform, 10));
        }

        /// <summary>Подавление регена (яд, укус Пасти) видно одним числом — через него идёт и восстановление сытостью.</summary>
        [Test]
        public void RegenSuppression_IsOneFactor_ForAnyHealing()
        {
            var h = New("~жертва").AddComponent<Health>();
            Assert.AreEqual(1f, h.RegenFactor, 1e-5f);
            h.SuppressRegen(0f, 5f);
            Assert.AreEqual(0f, h.RegenFactor, 1e-5f, "яд останавливает восстановление");
            h.SuppressRegen(0.5f, 5f);
            Assert.AreEqual(0.5f, h.RegenFactor, 1e-5f, "укус Пасти — свой множитель");
        }

        /// <summary>Нос — канал профиля: в дальности запаха цель замечена и затаившейся, за дальностью — нет.</summary>
        [Test]
        public void Smell_IsBoundByScentRange_NotBySearchRadius()
        {
            var obs = New("~нос"); var senses = obs.AddComponent<Senses>();
            senses.Set(SenseKind.Scent, 5f);
            senses.Set(SenseKind.Sight, 0f);
            var near = New("~рядом"); near.transform.position = new Vector3(0f, 0f, 3f);
            var far = New("~далеко"); far.transform.position = new Vector3(0f, 0f, 12f);
            Assert.IsTrue(Perception.Notices(obs.transform, near.transform, senses), "в дальности запаха — чует");
            Assert.IsFalse(Perception.Notices(obs.transform, far.transform, senses), "за дальностью запаха и без глаз — не знает");
            senses.Set(SenseKind.Scent, 0f);
            Assert.IsFalse(Perception.Notices(obs.transform, near.transform, senses), "нос погашен (дождь) — не чует");
        }
    }
}
