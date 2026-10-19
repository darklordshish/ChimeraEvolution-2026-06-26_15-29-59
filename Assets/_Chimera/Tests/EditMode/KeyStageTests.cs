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

        [Test]
        public void HumanWerewolf_Stage2_DrawsKeyObject_InsteadOfWolfTraits()
        {
            var human = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset");
            var wolf = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
            var stage = human?.keyStages?.FirstOrDefault(s => s != null && s.key == "Волк");
            if (stage == null) Assert.Inconclusive("у человека нет черты ключа «Волк» (`handoff/klyuchi/chelovek-volk-st2.json`)");
            GameObject Build(float w)
            {
                BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = w; BodyMesh.ResetCache(); ChainSwap.ResetCache();
                PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = w >= 1f ? 2 : 1;
                var go = new GameObject("~ступень"); var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
                MorphBuilder.Build(go.transform, human, human.organs.Where(o => o != null).ToList(), null);
                return go;
            }
            var st2 = Build(1f);
            try
            {
                var names = st2.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name).ToList();
                CollectionAssert.Contains(names, stage.objectName, "на ступени 2 черта ключа не нарисована");
                foreach (var h in stage.hides ?? new string[0]) CollectionAssert.DoesNotContain(names, h, $"объект шасси «{h}» под чертой ключа остался");
                var pieces = st2.GetComponentsInChildren<BodyPartView>(true).Select(v => v.name).ToList();
                foreach (var t in stage.replacesTraits ?? new string[0]) CollectionAssert.DoesNotContain(pieces, t, $"черта волка «{t}» куском встала поверх черты ключа");
            }
            finally { Object.DestroyImmediate(st2); }
            var st1 = Build(BodyMesh.KeyStep1);
            try { CollectionAssert.DoesNotContain(st1.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name).ToList(), stage.objectName, "на ступени 1 нарисована черта ступени 2"); }
            finally { Object.DestroyImmediate(st1); }
        }
    }
}
