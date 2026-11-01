using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ИНВАРИАНТЫ ПРАВОК ПЕРЕД ПЛЕЙТЕСТОМ (бой/сенсорика). Здесь только то, что проверяется
    /// без сцены и физики: арифметика конуса, вертикальный допуск, контракт имён головы и клич,
    /// который обязан заводить ярость. Сцена/стены/CharacterController — отдельные PlayMode-тесты
    /// (перечислены в отчёте, здесь их нет намеренно).
    /// </summary>
    public class PlaytestCombatFixesTests
    {
        [Test]
        public void CombatReach_InCone_UsesXZRangeAndAngle()
        {
            Vector3 o = Vector3.zero;
            Vector3 f = Vector3.forward;

            Assert.IsTrue(CombatReach.InCone(o, f, new Vector3(0f, 0f, 2f), 2f, 45f), "прямо впереди в досягаемости — достаёт");
            Assert.IsFalse(CombatReach.InCone(o, f, new Vector3(0f, 0f, 2.1f), 2f, 45f), "за радиусом — нет");
            Assert.IsFalse(CombatReach.InCone(o, f, new Vector3(0f, 0f, -1f), 2f, 45f), "за спиной — нет");
            Assert.IsFalse(CombatReach.InCone(o, f, new Vector3(2f, 0f, 0f), 2f, 45f), "сбоку за полууглом — нет");
            Assert.IsTrue(CombatReach.InCone(o, f, Vector3.zero, 2f, 45f), "цель вплотную — угол не определён, достаёт");

            // Конус намеренно ПЛОСКИЙ: высоту проверяет отдельное правило, а не угол.
            Assert.IsTrue(CombatReach.InCone(o, f, new Vector3(0f, 8f, 1f), 2f, 45f),
                "XZ-конус не знает про Y — вертикаль вынесена в WithinVertical");
        }

        [Test]
        public void CombatReach_WithinVertical_RejectsAnotherLevel()
        {
            Assert.IsTrue(CombatReach.WithinVertical(0f, 1.5f, 2f), "в пределах допуска — достаёт");
            Assert.IsFalse(CombatReach.WithinVertical(0f, 5f, 2f), "на другом ярусе — не достаёт");
            Assert.IsTrue(CombatReach.WithinVertical(0f, -2f, 2f), "ниже в пределах допуска — достаёт");
        }

        [Test]
        public void HeadNames_CoverMorphNoseMuzzleJaw()
        {
            foreach (var name in new[] { "нос", "морда", "челюсть", "зубы", "голова", "Пасть", "уши", "глаза" })
            {
                Assert.IsTrue(CallPrivateStatic(typeof(Telegraph), "IsHeadName", name),
                    $"Telegraph.IsHeadName: «{name}» — часть головы (иначе эмоция не красит морду)");
                Assert.IsTrue(CallPrivateStatic(typeof(PlayerController), "IsOwnFace", name),
                    $"PlayerController.IsOwnFace: «{name}» — часть головы (иначе торчит в камере от первого лица)");
            }

            Assert.IsFalse(CallPrivateStatic(typeof(Telegraph), "IsHeadName", "Хвост"), "хвост — не голова");
            Assert.IsFalse(CallPrivateStatic(typeof(PlayerController), "IsOwnFace", "Хвост"), "хвост — не голова");
        }

        [Test]
        public void Scream_AddsRageComponent_OnFirstCry()
        {
            var go = new GameObject("~клич");
            try
            {
                var scream = go.AddComponent<PlayerScream>();
                Assert.IsFalse(scream.Available, "без записи органа клича нет");
                scream.Configure(new ScreamData());
                Assert.IsTrue(scream.Available, "запись органа включила клич");

                Assert.IsTrue(scream.TryUse(), "первый клич должен сработать");
                var rage = go.GetComponent<Rage>();
                Assert.IsNotNull(rage, "клич обязан завести статус ярости, иначе он молча берёт кровь и ничего не даёт");
                Assert.IsTrue(rage.IsEnraged, "клич входит в ярость, а не просто звучит");
                Assert.IsNotNull(go.GetComponent<Bleed>(), "цена клича — собственная кровь");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static bool CallPrivateStatic(System.Type type, string method, string arg)
        {
            var mi = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, $"{type.Name}.{method} должен существовать");
            return (bool)mi.Invoke(null, new object[] { arg });
        }
    }
}
