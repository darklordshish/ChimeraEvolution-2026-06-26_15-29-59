using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// АРБИТР АРСЕНАЛА (`Arsenal`, спека 4b-2 §2.2): чем бить с дистанции. Окна срабатывания — из записей органов,
    /// дальние доставки спрашиваются раньше ближних, доставка без записи в арсенал не входит.
    /// Числа — тестовых записей, а не баланс игры.
    /// </summary>
    public class ArsenalTests
    {
        GameObject go;
        readonly List<WindupAbility> buffer = new();

        [SetUp] public void SetUp() => go = new GameObject("арсенал");
        [TearDown] public void TearDown() => Object.DestroyImmediate(go);

        T Carrier<T>(AbilityData record) where T : WindupAbility, IOrganAbility
        {
            var c = go.AddComponent<T>();
            c.Configure(record);
            return c;
        }

        [Test]
        public void Pick_FarTakesRangedWindow_NearTakesMelee_GapAndBeyondNothing()
        {
            var charge = Carrier<ChargeAbility>(new ChargeData { minRange = 4f, maxRange = 18f });
            var limb = Carrier<LimbStrikeAbility>(new LimbStrikeData { range = 1.8f });

            Arsenal.Collect(go, buffer);
            Assert.AreSame(charge, Arsenal.Pick(buffer, 10f), "цель в окне разбега — таран");
            Assert.AreSame(limb, Arsenal.Pick(buffer, 1f), "цель вплотную — удар конечностью");
            Assert.IsNull(Arsenal.Pick(buffer, 3f), "между окнами бить нечем");
            Assert.IsNull(Arsenal.Pick(buffer, 30f), "дальше всех окон бить нечем");
        }

        [Test]
        public void Pick_OverlappingWindows_RangedAskedFirst()
        {
            var leap = Carrier<LeapAbility>(new LeapData { minRange = 1f, maxRange = 6f });
            var bite = Carrier<BiteAbility>(new BiteData { range = 2f });

            Arsenal.Collect(go, buffer);
            Assert.AreSame(leap, Arsenal.Pick(buffer, 1.5f), "окна перекрылись — дальняя доставка спрашивается раньше ближней");
            Assert.AreSame(bite, Arsenal.Pick(buffer, 0.5f), "ниже окна наскока — укус");
        }

        [Test]
        public void Collect_CarrierWithoutRecord_NotInArsenal()
        {
            Carrier<BiteAbility>(null);
            var volley = Carrier<QuillVolley>(new VolleyData { minRange = 6f, maxRange = 15f });

            Arsenal.Collect(go, buffer);
            Assert.AreEqual(1, buffer.Count, "доставка без записи органа в арсенал не входит");
            Assert.AreSame(volley, buffer[0]);
            Assert.IsNull(Arsenal.Pick(buffer, 1f), "укус без записи не бьёт");
        }
    }
}
