using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S5 шаг 4, ворота G0: профиль теста = профиль сцены по ВСЕМ сериализованным
    /// полям <see cref="ForestBuilder"/>. Перечень ведёт <see cref="SerializedObject"/>,
    /// а не рукописный список шести полей: иначе новое поле (эрозия, дороги, пятна)
    /// молча выпадет из паритета. Сцена — источник правды: расхождение — красное.
    /// </summary>
    public class ForestSceneParityTests
    {
        const string Sandbox = "Assets/Scenes/ForestSandbox.unity";
        const string Fragment = "Assets/Scenes/ForestFragment.unity";
        const string Pavilion = "Assets/Scenes/ForestPavilion.unity";

        static readonly string[] BuiltIn =
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
            "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags",
            "m_Script", "m_Name", "m_EditorClassIdentifier",
        };

        GameObject expectedHost;
        ForestBuilder expected;

        [TearDown]
        public void TearDown()
        {
            if (expectedHost != null) Object.DestroyImmediate(expectedHost);
            expectedHost = null;
            expected = null;
        }

        [Test]
        public void Sandbox_Profile_Matches_GateProfile()
        {
            ReadScene(Sandbox, scene =>
            {
                expectedHost = new GameObject("~ExpectedA");
                expected = ForestProfiles.A(expectedHost);
                AddPopulation(expected, "Змея");
                AssertNoDiff(scene, expected, "A/ForestSandbox");
            });
        }

        [Test]
        public void Fragment_Profile_Matches_GateProfile()
        {
            ReadScene(Fragment, scene =>
            {
                expectedHost = new GameObject("~ExpectedB0");
                expected = ForestProfiles.B0(expectedHost);
                AssertNoDiff(scene, expected, "B0/ForestFragment (сид 9)");
            });
        }

        [Test]
        public void Pavilion_Profile_Matches_GateProfile()
        {
            ReadScene(Pavilion, scene =>
            {
                expectedHost = new GameObject("~ExpectedPavilion");
                expected = ForestProfiles.PavilionV1(expectedHost);
                AddPopulation(expected, "Волк", "Змея", "Лось", "Ёж"); // первая зона, 13.11
                AssertNoDiff(scene, expected, "Pavilion v1");
            });
        }

        /// <summary>
        /// Паритет не должен быть списком: меняем поле, которого в рукописном списке
        /// «шести» нет, и проверяем, что детектор его видит.
        /// </summary>
        [Test]
        public void Parity_CatchesFieldOutsideHandWrittenList()
        {
            expectedHost = new GameObject("~ExpectedSelf");
            var a = ForestProfiles.PavilionV1(expectedHost);
            var host = new GameObject("~ActualSelf");
            try
            {
                var b = ForestProfiles.PavilionV1(host);
                Assert.IsEmpty(Diff(a, b), "одинаковые профили не должны расходиться");
                b.erosionVersion = 7;
                var diff = Diff(a, b);
                Assert.IsTrue(diff.Exists(d => d.StartsWith("erosionVersion")),
                    "поле версии эрозии обязано попасть в паритет автоматически");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ------------------------------------------------------------------

        static void ReadScene(string path, System.Action<ForestBuilder> check)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                ForestBuilder found = null;
                foreach (var b in Object.FindObjectsByType<ForestBuilder>(FindObjectsInactive.Include))
                    if (b.gameObject.scene == scene) found = b;
                Assert.IsNotNull(found, $"{path}: ForestBuilder не найден");
                check(found);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Сцена держит ссылку на пул видов — ожидаемый профиль обязан её повторить.</summary>
        static void AddPopulation(ForestBuilder b, params string[] species)
        {
            b.speciesPool = new List<SpeciesSO>();
            foreach (var name in species)
            {
                var so = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");
                Assert.IsNotNull(so, $"вид «{name}» не загружен");
                b.speciesPool.Add(so);
            }
        }

        void AssertNoDiff(ForestBuilder scene, ForestBuilder expectedBuilder, string what)
        {
            var diff = Diff(scene, expectedBuilder);
            Assert.IsEmpty(diff,
                $"{what}: профиль теста разошёлся со сценой по полям:\n  " +
                string.Join("\n  ", diff));
        }

        /// <summary>
        /// Все сериализованные поля ForestBuilder. Список полей не пишется руками:
        /// <see cref="SerializedObject"/> сам отдаёт появившиеся поля.
        /// </summary>
        static List<string> Diff(ForestBuilder scene, ForestBuilder expectedBuilder)
        {
            var diff = new List<string>();
            var a = new SerializedObject(scene);
            var b = new SerializedObject(expectedBuilder);
            var it = a.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.depth != 0) continue;
                string name = it.name;
                if (System.Array.IndexOf(BuiltIn, name) >= 0) continue;
                var other = b.FindProperty(name);
                if (other == null)
                {
                    diff.Add($"{name}: поле есть в сцене, нет в профиле");
                    continue;
                }
                switch (it.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        // long (seed) и int приходят одним типом Integer; longValue
                        // корректно читает оба, intValue — только int.
                        if (it.longValue != other.longValue)
                            diff.Add($"{name}: сцена {it.longValue}, профиль {other.longValue}");
                        break;
                    case SerializedPropertyType.Boolean:
                        if (it.boolValue != other.boolValue)
                            diff.Add($"{name}: сцена {it.boolValue}, профиль {other.boolValue}");
                        break;
                    case SerializedPropertyType.Float:
                        if (Mathf.Abs(it.floatValue - other.floatValue) > 1e-4f)
                            diff.Add($"{name}: сцена {it.floatValue}, профиль {other.floatValue}");
                        break;
                    case SerializedPropertyType.Enum:
                        if (it.enumValueIndex != other.enumValueIndex)
                            diff.Add($"{name}: сцена {it.enumValueIndex}, профиль {other.enumValueIndex}");
                        break;
                    case SerializedPropertyType.String:
                        if (it.stringValue != other.stringValue)
                            diff.Add($"{name}: сцена «{it.stringValue}», профиль «{other.stringValue}»");
                        break;
                    case SerializedPropertyType.ObjectReference:
                        CompareObject(it, other, name, diff);
                        break;
                    case SerializedPropertyType.Generic:
                        if (it.isArray)
                        {
                            CompareArray(it, other, name, diff);
                        }
                        break;
                }
            }
            return diff;
        }

        static void CompareObject(SerializedProperty a, SerializedProperty b, string name,
            List<string> diff)
        {
            var oa = a.objectReferenceValue;
            var ob = b.objectReferenceValue;
            if (oa == null && ob == null) return;
            if (oa == null || ob == null)
            {
                diff.Add($"{name}: сцена {(oa == null ? "null" : oa.name)}, " +
                         $"профиль {(ob == null ? "null" : ob.name)}");
                return;
            }
            if (oa.name != ob.name)
                diff.Add($"{name}: сцена «{oa.name}», профиль «{ob.name}»");
        }

        static void CompareArray(SerializedProperty a, SerializedProperty b, string name,
            List<string> diff)
        {
            if (a.arraySize != b.arraySize)
            {
                diff.Add($"{name}: длина сцены {a.arraySize}, профиля {b.arraySize}");
                return;
            }
            if (name != "speciesPool") return;
            for (int i = 0; i < a.arraySize; i++)
                CompareObject(a.GetArrayElementAtIndex(i), b.GetArrayElementAtIndex(i),
                    $"{name}[{i}]", diff);
        }
    }
}
