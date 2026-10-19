using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ЧЕРТА КЛЮЧА НА СТУПЕНИ (19.10): объект меша шасси `@<ключ>` (голова, воротник, загривок и хвост из самой цели
    /// ключа) рисуется только на своей ступени — при полном весе ключа — и на ней заменяет черты цели кусками.</summary>
    public class KeyStageTests
    {
        [TearDown] public void TearDown() { BodyMesh.ForceKey = null; BodyMesh.ForceWeight = 0f; BodyMesh.ResetCache(); PartAssembly.ForceTraits = null; PartAssembly.ForceStep = 2; ChainSwap.ResetCache(); }

        [Test]
        public void Stage_ActiveOnlyAtFullWeight()
        {
            var sp = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                sp.keyStages = new[] { new KeyStage { key = "Волк", objectName = "@Волк", step = 2 } };
                Assert.IsEmpty(BodyMesh.ActiveStages(sp, new Dictionary<string, float> { ["Волк"] = BodyMesh.KeyStep1 }), "на ступени 1 черта ступени 2 не рисуется");
                Assert.AreEqual(1, BodyMesh.ActiveStages(sp, new Dictionary<string, float> { ["Волк"] = 1f }).Count, "на ступени 2 черта рисуется");
                Assert.IsEmpty(BodyMesh.ActiveStages(sp, new Dictionary<string, float> { ["Лось"] = 1f }), "ключ другого вида черту не включает");
            }
            finally { Object.DestroyImmediate(sp); }
        }

        /// <summary>Каждая ступень ключа «Волк» у человека рисует СВОЮ черту и только её: на ст.1 — объект ст.1, на ст.2 — объект
        /// ст.2 (объект ст.1 под ним снят, если он в `skryvaet`); черты цели из `zamenyaet_cherty` кусками не ставятся.</summary>
        [Test]
        public void HumanWerewolf_EachStage_DrawsItsKeyObject()
        {
            var human = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset");
            var wolf = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
            var stages = human?.keyStages?.Where(s => s != null && s.key == "Волк").ToList();
            if (stages == null || stages.Count == 0) Assert.Inconclusive("у человека нет черт ключа «Волк» (`handoff/klyuchi/chelovek-volk-st*.json`)");
            foreach (var stage in stages)
            {
                BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = BodyMesh.KeyWeight(stage.step); BodyMesh.ResetCache(); ChainSwap.ResetCache();
                PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = stage.step;
                var go = new GameObject("~ступень"); var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
                try
                {
                    MorphBuilder.Build(go.transform, human, human.organs.Where(o => o != null).ToList(), null);
                    var names = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name).ToList();
                    CollectionAssert.Contains(names, stage.objectName, $"на ступени {stage.step} её черта ключа не нарисована");
                    foreach (var other in stages.Where(o => o.step > stage.step))
                        CollectionAssert.DoesNotContain(names, other.objectName, $"на ступени {stage.step} нарисована черта ступени {other.step}");
                    foreach (var h in stage.hides ?? new string[0]) CollectionAssert.DoesNotContain(names, h, $"ступень {stage.step}: «{h}» под чертой ключа остался");
                    var pieces = go.GetComponentsInChildren<BodyPartView>(true).Select(v => v.name).ToList();
                    foreach (var t in stage.replacesTraits ?? new string[0]) CollectionAssert.DoesNotContain(pieces, t, $"ступень {stage.step}: черта волка «{t}» куском встала поверх черты ключа");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
