using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// РУБИЛЬНИК ИНДИВИДУАЛЬНОСТИ (`IndividualityConfig`): без галочки особь средняя — множители разброса равны 1,
    /// черты личности в середине своих диапазонов; с галочкой разброс снова катается. Середины берутся из самих
    /// диапазонов-ручек, а не из чисел в тесте: поменяли диапазон — тест не краснеет.
    /// </summary>
    public class IndividualityTests
    {
        readonly List<Object> trash = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
            IndividualityConfig.On = true;
        }

        static float Mid(Personality p, string range)
        {
            var r = (Vector2)typeof(Personality).GetField(range, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(p);
            return (r.x + r.y) * 0.5f;
        }

        [UnityTest]
        public IEnumerator Off_EveryIndividualIsAverage()
        {
            IndividualityConfig.On = false;
            var go = new GameObject("Средняя особь");
            trash.Add(go);
            var variance = go.AddComponent<SpawnVariance>();
            var person = go.AddComponent<Personality>();
            yield return null;

            Assert.AreEqual(1f, variance.DamageMult, "разброс урона при выключенной индивидуальности");
            Assert.AreEqual(1f, variance.SpeedMult, "разброс скорости при выключенной индивидуальности");
            Assert.AreEqual(1f, variance.HpMult, "разброс HP при выключенной индивидуальности");
            Assert.AreEqual(Mid(person, "braveryRange"), person.Bravery, 1e-5f, "храбрость не в середине диапазона");
            Assert.AreEqual(Mid(person, "aggressionRange"), person.Aggression, 1e-5f, "агрессия не в середине диапазона");
            Assert.AreEqual(Mid(person, "curiosityRange"), person.Curiosity, 1e-5f, "любопытство не в середине диапазона");
            Assert.AreEqual(Mid(person, "cautionRange"), person.Caution, 1e-5f, "осторожность не в середине диапазона");
        }

        [UnityTest]
        public IEnumerator On_IndividualsRollSpread()
        {
            IndividualityConfig.On = true;
            bool rolled = false;
            for (int i = 0; i < 8 && !rolled; i++)
            {
                var go = new GameObject("Особь " + i);
                trash.Add(go);
                var v = go.AddComponent<SpawnVariance>();
                rolled = v.DamageMult != 1f || v.SpeedMult != 1f || v.HpMult != 1f;
            }
            yield return null;
            Assert.IsTrue(rolled, "с галочкой разброс особи не катается");
        }
    }
}
