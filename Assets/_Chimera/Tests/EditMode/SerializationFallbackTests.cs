using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace Chimera.Tests.EditMode
{
    public class SerializationFallbackTests
    {
        [Test]
        public void SpeciesSO_SkinCell_DefaultWhenZero()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.skinCell = 0f;
                Assert.AreEqual(0.02f, so.SkinCell, 1e-5f, "skinCell 0 должен дать дефолт 0.02 через свойство");
                so.skinCell = 0.05f;
                Assert.AreEqual(0.05f, so.SkinCell, 1e-5f);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpeciesSO_SkinBlend_DefaultWhenZero()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.skinBlend = 0f;
                Assert.AreEqual(0.05f, so.SkinBlend, 1e-5f, "skinBlend 0 должен дать дефолт 0.05");
                so.skinBlend = 0.1f;
                Assert.AreEqual(0.1f, so.SkinBlend, 1e-5f);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpeciesSO_BuildLayers_DefaultWhenZero()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.buildLayers = 0;
                Assert.AreEqual(4, so.BuildLayers, "buildLayers 0 должен дать дефолт 4");
                so.buildLayers = 2;
                Assert.AreEqual(2, so.BuildLayers);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpeciesSO_BaseHp_FallbackViaCreatureBody()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            GameObject go = null;
            try
            {
                so.speciesName = "Тест";
                so.sockets = new BodySocket[0];
                so.organs = new Organ[] { new Organ { organName = "Кисть", slot = "Руки", cost = 3 } };
                so.baseHp = 0; // непрогнанный ассет
                go = new GameObject("FallbackHp");
                go.AddComponent<Health>();
                var body = go.AddComponent<CreatureBody>();
                body.Configure(so, new SpeciesSO[0]);

                // читаем приватное свойство BaseHp через рефлексию
                var prop = typeof(CreatureBody).GetProperty("BaseHp", BindingFlags.NonPublic | BindingFlags.Instance);
                // если свойство не найдено — проверяем через Health.Max (должен быть 75)
                if (prop != null)
                {
                    float v = (float)prop.GetValue(body);
                    Assert.AreEqual(75f, v, 1e-4f, "baseHp 0 должен дать дефолт 75");
                }
                else
                {
                    var h = go.GetComponent<Health>();
                    Assert.AreEqual(75, h.Max, "baseHp 0 должен дать Health.Max 75");
                }

                // ненулевой baseHp должен пройти как есть
                var so2 = ScriptableObject.CreateInstance<SpeciesSO>();
                so2.speciesName = "Тест2";
                so2.sockets = new BodySocket[0];
                so2.organs = new Organ[] { new Organ { organName = "Кисть", slot = "Руки", cost = 3 } };
                so2.baseHp = 90;
                var go2 = new GameObject("FallbackHp2");
                go2.AddComponent<Health>();
                var body2 = go2.AddComponent<CreatureBody>();
                body2.Configure(so2, new SpeciesSO[0]);
                var prop2 = typeof(CreatureBody).GetProperty("BaseHp", BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop2 != null)
                {
                    float v2 = (float)prop2.GetValue(body2);
                    Assert.AreEqual(90f, v2, 1e-4f);
                }
                Object.DestroyImmediate(go2);
                Object.DestroyImmediate(so2);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }
    }
}
