using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// ForestBuilder как арена: фикс-сид разрешается как вписан, случайный — пишется в лог
    /// с инструкцией по воспроизведению (как ArenaWalls). Срез A (24.10).
    /// <para>
    /// Сборка в Awake выключена намеренно: раньше эти тесты тянули полную карту Ø2000
    /// в Awake и весили десятки секунд. Теперь мир собирается явным Build() и малым
    /// диаметром (Ø300, 6 квадов) — проверяется разрешение сида, а не витрина.
    /// </para>
    /// <para>
    /// Замечание сторожа: ResolvedSeed разрешается в BuildInternal, а не в Awake, поэтому
    /// «активировать и прочитать сид» без сборки нельзя — тест зовёт Build() сам. Если
    /// понадобится сид без мира, нужен отдельный публичный Resolve (см. отчёт).
    /// </para>
    /// </summary>
    public class ForestBuilderTests
    {
        GameObject host;
        ForestBuilder builder;

        /// <summary>Пробный объект выключен на время настройки — иначе Awake стартует с дефолтами.</summary>
        ForestBuilder Probe(bool randomSeed)
        {
            host = new GameObject("~ForestBuilderProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = randomSeed;
            builder.seed = 4242;
            builder.mapDiameter = 300f;
            builder.terrainQuads = 6;
            host.SetActive(true);
            return builder;
        }

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        [UnityTest]
        public IEnumerator FixedSeed_ResolvesWithoutThrowing()
        {
            var b = Probe(randomSeed: false);
            yield return null;

            Assert.IsNull(b.BuiltRoot, "buildOnAwake=false: Awake мир не строит");
            Assert.AreEqual(0, b.ResolvedSeed, "до Build сид ещё не разрешён — резолв живёт в сборке");

            b.Build();
            yield return null;

            Assert.AreEqual(4242, b.ResolvedSeed, "фикс-сид обязан разрешиться как вписан");
            Assert.IsNotNull(b.BuiltRoot, "мир собран");
        }

        [UnityTest]
        public IEnumerator RandomSeed_LogsPhraseWithInstruction()
        {
            var b = Probe(randomSeed: true);
            yield return null;

            LogAssert.Expect(LogType.Log,
                new Regex(@"ForestBuilder: случайный сид \d+ \(для воспроизведения — выключи randomSeed и впиши его\)"));

            b.Build();
            yield return null;

            Assert.IsNotNull(b.BuiltRoot, "случайный мир собран (сид найден и записан в лог)");
        }
    }
}
