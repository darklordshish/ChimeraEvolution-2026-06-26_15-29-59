using Chimera.Laws;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>Сторож конфига законов (С5): ассет `Resources/LawsConfig` равен значениям по умолчанию из `Laws/`, подмена
    /// возвращается. Новое поле структуры приходит в старый ассет нулём — тест красный, пока ассет не дополнен.</summary>
    public class LawsConfigTests
    {
        [TearDown] public void TearDown() => LawsConfig.ResetCurrent();

        [Test]
        public void Asset_Exists_AndEqualsDefaults()
        {
            var asset = Resources.Load<LawsConfig>("LawsConfig");
            Assert.IsNotNull(asset, "Resources/LawsConfig.asset не найден");
            Assert.AreEqual(JsonUtility.ToJson(VenomParams.Default), JsonUtility.ToJson(asset.venom));
            Assert.AreEqual(JsonUtility.ToJson(BleedParams.Default), JsonUtility.ToJson(asset.bleed));
            Assert.AreEqual(JsonUtility.ToJson(RageParams.Default), JsonUtility.ToJson(asset.rage));
            Assert.AreEqual(JsonUtility.ToJson(SlowParams.Default), JsonUtility.ToJson(asset.slow));
            Assert.AreEqual(JsonUtility.ToJson(ThornsParams.Default), JsonUtility.ToJson(asset.thorns));
            Assert.AreEqual(JsonUtility.ToJson(HungerParams.Default), JsonUtility.ToJson(asset.hunger));
        }

        [Test]
        public void Override_ThenReset_RestoresLazyLoad()
        {
            var original = LawsConfig.Current;
            var tmp = ScriptableObject.CreateInstance<LawsConfig>();
            try
            {
                LawsConfig.Override(tmp);
                Assert.AreSame(tmp, LawsConfig.Current);
                LawsConfig.ResetCurrent();
                Assert.AreEqual(JsonUtility.ToJson(original.venom), JsonUtility.ToJson(LawsConfig.Current.venom));
            }
            finally { Object.DestroyImmediate(tmp); }
        }
    }
}
