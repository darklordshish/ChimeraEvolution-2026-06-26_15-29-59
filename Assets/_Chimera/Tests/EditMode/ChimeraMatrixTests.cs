using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>СТОРОЖ МАТРИЦЫ ХИМЕР (спека `2026-09-26-adresaciya-detaley-himery.md` §4).
    ///
    /// 26.09 химеры оказались сломаны все, и чинить их будут долго — по шагам, вместе с модельной линией. Поэтому сторож
    /// ведёт ДОЛГ, а не требует ноль: список разных поломок (строк отчёта `Docs/Диаграммы/ХИМЕРЫ.md`) не растёт. Новая
    /// строка — новая поломка, красный. Строк стало меньше — тоже красный, с просьбой опустить долг: иначе сломанное
    /// заново спряталось бы в запасе, оставшемся от починенного. Чистые виды — строго ноль: нарушение на чистом виде
    /// значит, что неверно ПРАВИЛО.</summary>
    public class ChimeraMatrixTests
    {
        // ДОЛГ — СПИСОК, А НЕ ЧИСЛО (06.10, аудит Codex): строки `ChimeraMatrixDebt.txt` рядом с тестом. История числа:
        // 281 → 275 (поставка 25) → 216 (26) → 204 (П4, дотяжка) → 182 (28, шток) → 178 (подстановка цепи) → 176 (голова, грудь;
        // голова-корень змеи на земле) → 172 (кисть человека только своему шасси); только опускается
        static List<string> Debt() => File.ReadAllLines(ChimeraMatrix.DebtFile).Where(l => l.Trim().Length > 0).ToList();

        [Test]
        public void PureSpecies_PassTheSameRules()
        {
            ChimeraMatrix.Run(out var native);
            Assert.IsEmpty(native, "правила матрицы кричат на чистый вид — чинить правило, а не вид:\n" + string.Join("\n", native));
        }

        [Test]
        public void ChimeraBreakages_DoNotGrow()
        {
            var now = ChimeraMatrix.BreakageKeys(ChimeraMatrix.Run(out _));
            var debt = Debt();
            var fresh = now.Except(debt).ToList();
            var fixedOnes = debt.Except(now).ToList();
            Assert.IsEmpty(fresh, $"НОВЫЕ поломки химер ({fresh.Count}) — смотри Docs/Диаграммы/ХИМЕРЫ.md (unity command chimera-matrix):\n" + string.Join("\n", fresh));
            Assert.IsEmpty(fixedOnes, $"поломки ушли ({fixedOnes.Count}) — вычеркни их из {ChimeraMatrix.DebtFile} (меню «Chimera → Переписать долг матрицы химер»), чтобы починенное не стало запасом:\n" + string.Join("\n", fixedOnes));
        }
    }
}
