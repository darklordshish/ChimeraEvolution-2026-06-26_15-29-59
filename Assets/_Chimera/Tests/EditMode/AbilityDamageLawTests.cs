using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ЗАКОН УРОНА ОДИН НА ВСЕ ПРИЁМЫ (спека 16.09): урон записи раскрывается экспрессией — у природного зверя это доля
    /// записи, у игрока он растёт с мощью органа. До 16.09 так раскрывались только укус и удар конечностью, а рога, таран,
    /// залп, наскок, пинок, перекат и удушение били как записано — неоднородность признана недоработкой.
    /// Поле урона узнаётся по имени: начинается на `damage` или кончается на `Damage`. «…PerDamage» — коэффициент
    /// на единицу урона (откат сжатия захвата), а не урон.
    /// </summary>
    public class AbilityDamageLawTests
    {
        static readonly Regex DamageField = new(@"^damage|(?<!Per)Damage$");

        static System.Collections.Generic.IEnumerable<(System.Type type, FieldInfo field)> DamageFields() =>
            typeof(AbilityData).Assembly.GetTypes()
                .Where(t => t.IsClass && typeof(AbilityData).IsAssignableFrom(t))
                .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Select(f => (t, f)))
                .Where(p => (p.f.FieldType == typeof(int) || p.f.FieldType == typeof(float)) && DamageField.IsMatch(p.f.Name));

        [Test]
        public void EveryDamageField_IsExpressed()
        {
            var raw = DamageFields().Where(p => !p.field.IsDefined(typeof(ExpressedAttribute)))
                .Select(p => p.type.Name + "." + p.field.Name).OrderBy(n => n).ToList();
            Assert.IsEmpty(raw, "урон приёма обязан раскрываться экспрессией — пометь поле [Expressed]:\n  " + string.Join("\n  ", raw));
        }

        [Test]
        public void Detector_SeesDamageFields()
        {
            Assert.Greater(DamageFields().Count(), 0, "сторож закона урона не видит ни одного поля урона — поменялись имена полей?");
        }
    }
}
