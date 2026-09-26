using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>СТОРОЖ МАТРИЦЫ ХИМЕР (спека `2026-09-26-adresaciya-detaley-himery.md` §4).
    ///
    /// 26.09 химеры оказались сломаны все, и чинить их будут долго — по шагам, вместе с модельной линией. Поэтому сторож
    /// ведёт ДОЛГ, а не требует ноль: число разных поломок (строк отчёта `Docs/Диаграммы/ХИМЕРЫ.md`) не растёт. Новая
    /// строка — новая поломка, красный. Строк стало меньше — тоже красный, с просьбой опустить долг: иначе сломанное
    /// заново спряталось бы в запасе, оставшемся от починенного. Чистые виды — строго ноль: нарушение на чистом виде
    /// значит, что неверно ПРАВИЛО.</summary>
    public class ChimeraMatrixTests
    {
        const int Debt = 281; // первый прогон 26.09; только опускается

        [Test]
        public void PureSpecies_PassTheSameRules()
        {
            ChimeraMatrix.Run(out var native);
            Assert.IsEmpty(native, "правила матрицы кричат на чистый вид — чинить правило, а не вид:\n" + string.Join("\n", native));
        }

        [Test]
        public void ChimeraBreakages_DoNotGrow()
        {
            var res = ChimeraMatrix.Run(out _);
            int now = ChimeraMatrix.Breakages(res);
            Assert.LessOrEqual(now, Debt, $"поломок химер стало больше долга ({now} > {Debt}) — смотри Docs/Диаграммы/ХИМЕРЫ.md (unity command chimera-matrix)");
            Assert.AreEqual(Debt, now, $"поломок меньше долга ({now} < {Debt}) — опусти Debt до {now}, чтобы починенное не стало запасом");
        }
    }
}
