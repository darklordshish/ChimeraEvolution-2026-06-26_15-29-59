using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// ForestBuilder как арена: фикс-сид повторяется, случайный — пишется в лог
    /// с инструкцией по воспроизведению. Слайс s10a (скелет, без полного Awake-пути).
    /// </summary>
    public class ForestBuilderTests
    {
        [UnityTest]
        public IEnumerator FixedSeed_ResolvesWithoutThrowing()
        {
            var go = new GameObject("~ForestBuilderProbe");
            go.SetActive(false);
            var builder = go.AddComponent<ForestBuilder>();
            builder.randomSeed = false;
            builder.seed = 4242;
            go.SetActive(true);
            yield return null;
            Assert.AreEqual(4242, builder.ResolvedSeed, "фикс-сид обязан разрешиться как вписан");
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator RandomSeed_LogsPhraseWithInstruction()
        {
            var go = new GameObject("~ForestBuilderProbe");
            LogAssert.Expect(LogType.Log,
                new Regex(@"ForestBuilder: случайный сид \d+ \(для воспроизведения — выключи randomSeed и впиши его\)"));
            go.AddComponent<ForestBuilder>();
            yield return null;
            Object.Destroy(go);
        }
    }
}
