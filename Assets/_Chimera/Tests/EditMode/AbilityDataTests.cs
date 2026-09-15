using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ЗАКОН РАСКРЫТИЯ ЗАПИСИ ПРИЁМА (`AbilityData.Resolve` / `Sup`) — тот же, что у чисел органа в `CreatureBody.Express`:
    /// родной орган — значение × мощь, донорский — бленд от вытесненного органа, дубли — супремум (перезарядка — минимум).
    /// Числа ниже — числа самих тестовых записей, а не баланс игры.
    /// </summary>
    public class AbilityDataTests
    {
        static BiteData Rec(int damage, float range, float cooldown, int bleed = 0, float regen = 1f) => new BiteData
        {
            damage = damage, range = range, halfAngle = 50f, windupTime = 0.4f, cooldown = cooldown,
            bleedStacks = bleed, regenDebuff = regen,
        };

        [Test]
        public void Native_ExpressedScalesByPower_OtherNumbersAsWritten()
        {
            var r = (BiteData)Rec(20, 2f, 0.7f, bleed: 2).Resolve(null, native: true, power: 0.5f);
            Assert.AreEqual(10, r.damage, "урон [Expressed] у родного органа = запись × мощь");
            Assert.AreEqual(2f, r.range, 1e-5f, "форма удара экспрессией не раскрывается");
            Assert.AreEqual(2, r.bleedStacks, "стаки — как записаны");
            Assert.AreEqual(0.7f, r.cooldown, 1e-5f, "перезарядка — как записана");
        }

        [Test]
        public void Donor_BlendsFromDisplacedOrganRecord()
        {
            var r = (BiteData)Rec(20, 2f, 0.7f).Resolve(Rec(10, 1f, 1f), native: false, power: 0.5f);
            Assert.AreEqual(15, r.damage, "донорский орган: бленд от вытесненного 10 к своим 20 на мощи 0.5");
        }

        [Test]
        public void Donor_NothingDisplaced_BlendsFromZero()
        {
            var r = (BiteData)Rec(20, 2f, 0.7f).Resolve(null, native: false, power: 0.5f);
            Assert.AreEqual(10, r.damage, "в химерном слоте или над пустым родным органом бленд идёт от нуля");
        }

        [Test]
        public void Resolve_ReturnsCopy_RecordInAssetUntouched()
        {
            var rec = Rec(20, 2f, 0.7f);
            rec.Resolve(null, native: true, power: 2f);
            Assert.AreEqual(20, rec.damage, "раскрытие не должно менять запись в ассете");
        }

        [Test]
        public void Sup_TakesBestPerNumber_LowerIsBetterTakesMinimum()
        {
            var s = (BiteData)AbilityData.Sup(Rec(20, 1.5f, 0.9f, bleed: 1, regen: 0.8f), Rec(12, 2f, 0.6f, bleed: 3, regen: 0.5f));
            Assert.AreEqual(20, s.damage, "урон — максимум");
            Assert.AreEqual(2f, s.range, 1e-5f, "досягаемость — максимум");
            Assert.AreEqual(3, s.bleedStacks, "кровь — максимум");
            Assert.AreEqual(0.6f, s.cooldown, 1e-5f, "перезарядка — меньше лучше");
            Assert.AreEqual(0.5f, s.regenDebuff, 1e-5f, "сбив регена — меньший множитель сильнее");
        }
    }
}
