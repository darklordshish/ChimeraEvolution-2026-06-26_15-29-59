using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ЗАПИСИ ПРИЁМОВ В НАСТОЯЩИХ АССЕТАХ ВИДОВ. Ловит два молчаливых класса ошибок: «приём есть, числа ноль» —
    /// ровно то, на чём до 12.09 стояли «Рога» и «Игломёт», — и «виды не пересозданы после правки бутстрапа».
    /// Проверяются только обязательные числа ([MustBePositive]), а не их величина: баланс — не дело теста.
    /// </summary>
    public class OrganAbilityAssetsTests
    {
        static SpeciesSO[] All() => AssetDatabase.FindAssets("t:SpeciesSO", new[] { "Assets/_Chimera/Data" })
            .Select(g => AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).ToArray();

        [Test]
        public void SpeciesAssets_CarryAbilityRecords()
        {
            var species = All();
            Assert.IsNotEmpty(species, "нет ассетов видов в Assets/_Chimera/Data");
            int records = species.SelectMany(s => s.organs).Sum(o => o.abilities?.Count(r => r != null) ?? 0);
            Assert.Greater(records, 0,
                "ни одной записи приёма в ассетах — виды не пересозданы после правки SpeciesBootstrap («Chimera → Создать дефолтные виды»)");
        }

        [Test]
        public void Records_NoEmptyEntries_RequiredNumbersPositive()
        {
            var bad = new List<string>();
            foreach (var s in All())
                foreach (var o in s.organs)
                {
                    if (o.abilities == null) continue;
                    foreach (var r in o.abilities)
                    {
                        if (r == null) { bad.Add($"{s.speciesName}/{o.organName}: пустая запись (класс переименован без [MovedFrom]?)"); continue; }
                        foreach (var f in r.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            if (f.IsDefined(typeof(MustBePositiveAttribute)) && Convert.ToSingle(f.GetValue(r)) <= 0f)
                                bad.Add($"{s.speciesName}/{o.organName}: {r.GetType().Name}.{f.Name} = {f.GetValue(r)}");
                    }
                }
            Assert.IsEmpty(bad, "запись приёма с нулём в обязательном числе — приём молча не работает:\n  " + string.Join("\n  ", bad));
        }
    }
}
