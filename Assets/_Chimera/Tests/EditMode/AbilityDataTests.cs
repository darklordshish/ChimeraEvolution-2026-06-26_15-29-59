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
            Assert.AreEqual(1f, rec.power, 1e-5f, "мощь раскрытия не должна оседать в записи ассета");
        }

        [Test]
        public void Resolve_CarriesOrganPower_SupKeepsStrongest()
        {
            var strong = Rec(20, 2f, 0.7f).Resolve(null, native: true, power: 1.5f);
            var weak = Rec(20, 2f, 0.7f).Resolve(null, native: true, power: 0.5f);
            Assert.AreEqual(1.5f, strong.power, 1e-5f, "раскрытая запись несёт мощь своего органа — модификатор для носителя");
            Assert.AreEqual(1.5f, AbilityData.Sup(weak, strong).power, 1e-5f, "при дублях приёма — мощь сильнейшего органа");
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

        [Test]
        public void HowlData_ReachNotBelowBase_StunsOnlyFromPowerThreshold()
        {
            var weak = (HowlData)new HowlData { radius = 14f, stunAt = 2f }.Resolve(null, native: true, power: 0.45f);
            Assert.AreEqual(14f, weak.Reach, 1e-5f, "рядовой волк воет как волк: радиус не ниже базы");
            Assert.IsFalse(weak.Stuns, "мощь ниже порога — вой только зовёт, не станит");

            var strong = (HowlData)new HowlData { radius = 14f, stunAt = 2f }.Resolve(null, native: true, power: 2f);
            Assert.AreEqual(28f, strong.Reach, 1e-5f, "радиус растёт с мощью органа");
            Assert.IsTrue(strong.Stuns, "мощь доросла до порога — вой станит");

            var mute = (HowlData)new HowlData { radius = 14f, stunAt = 0f }.Resolve(null, native: true, power: 5f);
            Assert.IsFalse(mute.Stuns, "порог 0 — стана нет вовсе, при любой мощи");
        }

        [Test]
        public void ConstrictData_ForeignChassisCapsStage_SupTakesStrongestGrip()
        {
            var guest = new ConstrictData { maxStage = 3, foreignMaxStage = 2 };
            guest.OnForeignChassis();
            Assert.AreEqual(2, guest.maxStage, "в гостях захват держит не выше foreignMaxStage");

            var jaw = new ConstrictData { maxStage = 1, foreignMaxStage = 2 };
            jaw.OnForeignChassis();
            Assert.AreEqual(1, jaw.maxStage, "кап режет только сверху: слабый захват в гостях не усиливается");

            var home = new ConstrictData { maxStage = 3, foreignMaxStage = 2 };
            Assert.AreEqual(3, ((ConstrictData)AbilityData.Sup(jaw, home)).maxStage, "при двух грэпл-органах держит сильнейший");
            var slow = (ConstrictData)AbilityData.Sup(new ConstrictData { grabSlow1 = 0.5f }, new ConstrictData { grabSlow1 = 0.35f });
            Assert.AreEqual(0.35f, slow.grabSlow1, 1e-5f, "слоу жертвы — меньший множитель сильнее");
        }
    }
}
