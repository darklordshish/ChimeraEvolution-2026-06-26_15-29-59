using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Логова: валидация, страх/истощение, очередь до капа, события, реестр.
    /// Слайс s2 (ветка forest/s2-lairs-areas, спека 2026-09-18-forest-s2).
    /// AddComponent — только для тестов сайта (конвенция PoolEconomyTests), чистые правила — без GO.
    /// </summary>
    public class LairRulesTests
    {
        readonly List<GameObject> garbage = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in garbage) Object.DestroyImmediate(go);
            garbage.Clear();
        }

        LairSite Site(string species, int capacity)
        {
            var go = new GameObject("~Lair");
            garbage.Add(go);
            var site = go.AddComponent<LairSite>();
            site.speciesName = species;
            site.capacity = capacity;
            return site;
        }

        static List<string> Check(LairSite s)
        {
            return LairRules.CheckSite(s.speciesName, s.homeRadius, s.spawnRadius,
                s.fearRadius, s.tier, s.capacity, s.respawnSeconds);
        }

        [Test]
        public void BadSite_IsReported()
        {
            var s = Site("", 0);
            s.homeRadius = 0f;
            s.tier = 5;
            s.respawnSeconds = 0f;
            Assert.IsNotEmpty(Check(s), "битый сайт обязан ловить LairRules.CheckSite");
        }

        [Test]
        public void GoodSite_IsClean()
        {
            Assert.IsEmpty(Check(Site("Волк", 4)), "дефолтный сайт обязан быть чистым");
        }

        [Test]
        public void Fear_ClampsAndDecays()
        {
            Assert.AreEqual(1f, LairRules.AddFear(0.6f, 0.6f), "страх клампится в 1");
            Assert.AreEqual(0.8f, LairRules.DecayFear(1f, 10f, 0.02f), 1e-5f, "спад 0.02/с за 10с");
            Assert.AreEqual(0.5f, LairRules.DecayFear(0.5f, -5f, 0.02f), "отрицательный dt не мотает время назад");
        }

        [Test]
        public void Exhausted_Threshold()
        {
            Assert.IsFalse(LairRules.IsExhausted(0.69f, 0.7f));
            Assert.IsTrue(LairRules.IsExhausted(0.7f, 0.7f));
        }

        [Test]
        public void Queue_FillsToCapacity_AndHoldsOnLongTicks()
        {
            var s = Site("Волк", 2);
            s.respawnSeconds = 10f;
            for (int i = 0; i < 5; i++) s.Tick(1f);
            Assert.AreEqual(1, s.pendingSpawns, "таймер с нуля: первый слот сразу");
            for (int i = 0; i < 25; i++) s.Tick(1f);
            Assert.AreEqual(2, s.pendingSpawns, "второй слот на 11-й секунде, дальше стоит на капе");
            Assert.AreEqual(1, s.ConsumeSpawn(), "хук забирает слот");
            Assert.AreEqual(1, s.population, "забранный слот — в популяции");
        }

        [Test]
        public void Fear_BlocksQueue_UntilDecay()
        {
            var s = Site("Волк", 2);
            s.fear = 1f;
            for (int i = 0; i < 10; i++) s.Tick(1f);
            Assert.AreEqual(0, s.pendingSpawns, "пока страх ≥0.7 очередь стоит");
            for (int i = 0; i < 100; i++) s.Tick(1f);
            Assert.Greater(s.pendingSpawns, 0, "после спада страха очередь пошла");
        }

        [Test]
        public void Kill_OutsideFearRadius_Ignored_TierScalesFear()
        {
            var s = Site("Волк", 4);
            s.tier = 2;
            s.ReportKill(s.transform.position + new Vector3(500f, 0f, 0f));
            Assert.AreEqual(0f, s.fear, "дальний килл логово не пугает");
            s.ReportKill(s.transform.position);
            Assert.AreEqual(0.34f, s.fear, 1e-5f, "ближний килл tier2: 0.34·2/2");
        }

        [Test]
        public void Events_FireOnThresholdCrossing()
        {
            var s = Site("Волк", 4);
            s.tier = 1;
            int exhausted = 0;
            int recovered = 0;
            s.Exhausted += () => exhausted++;
            s.Recovered += () => recovered++;
            s.ReportKill(s.transform.position);
            Assert.AreEqual(0, exhausted, "один килл tier1 (0.68) порога не достаёт");
            s.ReportKill(s.transform.position);
            Assert.AreEqual(1, exhausted, "второй килл кладёт в истощение — Exhausted раз");
            for (int i = 0; i < 100; i++) s.Tick(1f);
            Assert.AreEqual(1, recovered, "переход вниз стреляет Recovered раз");
        }

        [Test]
        public void Registry_TotalsAndNearest()
        {
            var regGo = new GameObject("~Registry");
            garbage.Add(regGo);
            var reg = regGo.AddComponent<LairRegistry>();
            var a = Site("Волк", 3);
            a.transform.position = new Vector3(0f, 0f, 0f);
            var b = Site("Волк", 4);
            b.transform.position = new Vector3(100f, 0f, 0f);
            reg.Register(a);
            reg.Register(b);
            reg.Register(a);
            Assert.AreEqual(2, reg.Count, "двойная регистрация не двоится");
            Assert.AreEqual(7, reg.TotalCapacity("Волк"), "кап вида = сумма капов логовищ");
            Assert.AreEqual(0, reg.TotalCapacity("Лось"), "чужого вида нет");
            Assert.AreSame(b, reg.NearestHome("Волк", new Vector3(90f, 0f, 0f)), "дом — ближайшее логово");
            Assert.IsNull(reg.NearestHome("Лось", Vector3.zero), "без логова — null");
            reg.Unregister(a);
            Assert.AreEqual(4, reg.TotalCapacity("Волк"), "unregister чистит кап");
        }
    }
}
