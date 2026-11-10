using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>КОЖА ОРГАНА ПРОЕКЦИЕЙ (спека `2026-11-10-kozha-organa-proekciej.md`): прививка Шкуры донора с плиткой надевает на тело
    /// шасси материал кожи; органы на шаблоне, цветные куски и черты кожу не получают; вид без плитки — тело как раньше.</summary>
    public class KozhaOrganaTests
    {
        GameObject go;

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        static List<Organ> WithHideOf(SpeciesSO chassis, SpeciesSO donor) =>
            chassis.organs.Where(o => o != null).Select(o => !o.chassisOnly && o.slot == KozhaOrgana.Slot
                ? donor.organs.FirstOrDefault(d => d != null && d.slot == KozhaOrgana.Slot && !d.chassisOnly && !d.nativeOnly) ?? o : o).ToList();

        SkinnedMeshRenderer[] Build(SpeciesSO chassis, List<Organ> worn)
        {
            go = new GameObject("~ТестКожи");
            MorphBuilder.Build(go.transform, chassis, worn);
            return go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        // объект тела — без `@`, без точки в имени меша (имя рендерера всегда слот, поэтому судим по мешу)
        static bool IsBodyObject(SkinnedMeshRenderer r) => !r.sharedMesh.name.Contains("@") && !r.sharedMesh.name.Contains(".");
        static bool HasSkin(SkinnedMeshRenderer r) => r.sharedMaterial != null && r.sharedMaterial.shader.name == KozhaOrgana.ShaderName;

        [Test]
        public void HumanWithSnakeHide_BodyObjectsGetSkin_OrganAndColoredPiecesDoNot()
        {
            var human = Load("Человек"); var snake = Load("Змея");
            Assert.IsNotNull(KozhaOrgana.For(snake), "плитка змеи не найдена в Resources/Кожа/Змея");
            var smrs = Build(human, WithHideOf(human, snake));
            var body = smrs.Where(IsBodyObject).ToArray();
            Assert.IsNotEmpty(body, "тела нет");
            Assert.IsTrue(body.All(HasSkin), "объекты тела должны получить материал кожи");
            Assert.IsFalse(smrs.Where(r => !IsBodyObject(r)).Any(HasSkin), "органы `слот@вид`, цветные куски и черты кожу не получают");
            var block = new MaterialPropertyBlock();
            body[0].GetPropertyBlock(block);
            Assert.Greater(block.GetFloat(KozhaOrgana.BodyHeight), 1.5f, "рост тела в блоке свойств не записан");
            Assert.AreEqual(1f, block.GetFloat(KozhaOrgana.SkinWeight), 1e-4f, "надетый графт Шкуры включает кожу полностью");
            var mean = body[0].sharedMaterial.GetColor("_SkinMean");
            Assert.That(mean.r, Is.InRange(0.2f, 0.9f), "среднее плитки не посчитано");
            Assert.AreEqual(snake.tint.g, body[0].sharedMaterial.GetColor("_SkinTint").g, 1e-4f, "цвет кожи — цвет вида-донора");
        }

        /// <summary>Крупный узор необязателен: у змеи плитка `uzor` подгружена и контраст включён, без неё слой выключен.</summary>
        [Test]
        public void LargePattern_OnForSnake_OffWithoutTile()
        {
            var snakeMat = KozhaOrgana.For(Load("Змея"));
            Assert.IsNotNull(snakeMat.GetTexture("_PatternTex"), "у змеи узор не задан");
            Assert.Greater(snakeMat.GetFloat("_PatternContrast"), 0f);
            var plain = new Material(Shader.Find(KozhaOrgana.ShaderName));
            KozhaOrgana.ApplyPattern(plain, null);
            Assert.AreEqual(0f, plain.GetFloat("_PatternContrast"), "донор без узора — слой выключен");
            Object.DestroyImmediate(plain);
        }

        [Test]
        public void HumanWithoutSnakeHide_DrawnWithOrdinaryMaterial()
        {
            var human = Load("Человек");
            var smrs = Build(human, human.organs.Where(o => o != null).ToList());
            Assert.IsNotEmpty(smrs);
            Assert.IsFalse(smrs.Any(HasSkin), "родное тело кожи не получает");
        }

        [Test]
        public void DonorWithoutTile_LeavesBodyAsBefore()
        {
            var human = Load("Человек"); var moose = Load("Лось");
            Assume.That(Resources.Load<Texture2D>("Кожа/Лось/cvet") == null, "плитка лося появилась — тест про вид без плитки перепиши на другой вид");
            var worn = WithHideOf(human, moose);
            Assume.That(worn.Any(o => o != null && o.slot == KozhaOrgana.Slot && moose.organs.Contains(o)), "у лося нет Шкуры для прививки");
            var smrs = Build(human, worn);
            Assert.IsNotEmpty(smrs);
            Assert.IsFalse(smrs.Any(HasSkin), "у вида без плитки кожи нет");
        }
    }
}
