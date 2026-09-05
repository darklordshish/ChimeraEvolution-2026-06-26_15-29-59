using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Unity-гочи: FindAnyObjectByType без сортировки, InputAction без layout, UnityEvent=new()
    /// (CLAUDE.md Unity-гочи: FindAnyObjectByType (не FindFirst), FindObjectsByType без параметра сортировки;
    /// конструктор InputAction — БЕЗ expectedControlLayout; UnityEvent-поля = new())
    /// </summary>
    public class UnityApiTests
    {
        string ScriptsRoot
        {
            get
            {
                // Поддерживаем оба расположения: ChimeraEvolution/Assets и Assets
                var candidates = new[]
                {
                    Path.Combine(Application.dataPath, "_Chimera", "Scripts"),
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Assets", "_Chimera", "Scripts")),
                };
                foreach (var c in candidates) if (Directory.Exists(c)) return c;
                return Path.Combine(Application.dataPath, "_Chimera", "Scripts");
            }
        }

        string[] AllCsFiles()
        {
            var root = ScriptsRoot;
            if (!Directory.Exists(root)) return new string[0];
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        }

        [Test]
        public void FindAnyObjectByType_WithoutSortParam_And_NoFindFirst()
        {
            var files = AllCsFiles();
            Assert.Greater(files.Length, 0, "скрипты должны существовать");
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                // запрещённый API Unity 2023: FindFirstObjectByType
                Assert.IsFalse(text.Contains("FindFirstObjectByType"),
                    $"{Path.GetFileName(f)} содержит FindFirstObjectByType — должен быть FindAnyObjectByType (Unity 6 API)");

                // FindObjectsByType не должен вызываться с параметром сортировки (FindObjectsSortMode)
                // ловим второй аргумент: FindObjectsByType<T>(SortMode) или FindObjectsByType<T>(..., SortMode)
                var m = Regex.Matches(text, @"FindObjectsByType\s*<[^>]+>\s*\([^)]*FindObjectsSortMode[^)]*\)");
                Assert.AreEqual(0, m.Count,
                    $"{Path.GetFileName(f)} вызывает FindObjectsByType с сортировкой: { (m.Count>0?m[0].Value:"") } — должен быть без параметра сортировки");
            }
            // позитивная проверка: проект действительно использует FindAnyObjectByType
            bool any = files.Any(f => File.ReadAllText(f).Contains("FindAnyObjectByType"));
            Assert.IsTrue(any, "проект должен использовать FindAnyObjectByType");
        }

        [Test]
        public void InputAction_Constructor_WithoutExpectedControlLayout()
        {
            var files = AllCsFiles();
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                Assert.IsFalse(text.Contains("expectedControlLayout"),
                    $"{Path.GetFileName(f)} содержит expectedControlLayout — конструктор InputAction БЕЗ этого параметра (контракт интерфейса в коде)");
            }
            // позитивная проверка: InputAction создаётся как new InputAction("Name", InputActionType.Value/Button)
            bool any = files.Any(f => Regex.IsMatch(File.ReadAllText(f), @"new\s+InputAction\s*\(\s*""[^""]+""\s*,\s*InputActionType\."));
            Assert.IsTrue(any, "должен существовать new InputAction(name, type) без layout");
            // через рефлексию убедимся что ctor c (string, InputActionType) существует — в InputSystem 1.19 это один ctor
            // с опциональными параметрами (name, type, binding, interactions, processors, expectedControlType), поэтому
            // длина параметров >=2, первые два — string + InputActionType, остальные optional
            var ctors = typeof(InputAction).GetConstructors();
            bool hasTwo = ctors.Any(c =>
            {
                var p = c.GetParameters();
                return p.Length >= 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(InputActionType)
                       && p.Skip(2).All(pp => pp.IsOptional);
            });
            Assert.IsTrue(hasTwo, "InputAction(string, InputActionType) должен существовать (в 1.19 ctor c optional expectedControlType)");
        }

        [Test]
        public void UnityEvent_Fields_InitializedWithNew()
        {
            var files = AllCsFiles();
            // Health.cs должен иметь onDamaged/onDeath = new()
            var healthFile = files.FirstOrDefault(f => Path.GetFileName(f) == "Health.cs");
            Assert.IsNotNull(healthFile, "Health.cs должен существовать");
            var text = File.ReadAllText(healthFile);
            Assert.IsTrue(text.Contains("onDamaged = new()") || text.Contains("onDamaged = new UnityEvent()"),
                "Health.onDamaged должен быть инициализирован = new() (иначе null при AddComponent в рантайме)");
            Assert.IsTrue(text.Contains("onDeath = new()") || text.Contains("onDeath = new UnityEvent()"),
                "Health.onDeath должен быть инициализирован = new()");

            // глобально: любое поле UnityEvent без инициализатора — флаг
            foreach (var f in files)
            {
                var t = File.ReadAllText(f);
                // ищем `public UnityEvent xyz;` без `= new` — но допускаем [SerializeField] private
                var matches = Regex.Matches(t, @"UnityEvent\s+(\w+)\s*;");
                foreach (Match m in matches)
                {
                    string line = m.Value;
                    // если в строке нет `= new`, считаем нарушением (упрощённо)
                    // реальные поля Health уже проверены выше — они с инициализатором, сюда не попадут
                    Assert.Fail($"{Path.GetFileName(f)} содержит неинициализированное UnityEvent поле: {line.Trim()} — должно быть = new()");
                }
            }

            // runtime проверка: AddComponent<Health> даёт неnull события
            var go = new GameObject("UnityEventCheck");
            try
            {
                var h = go.AddComponent<Health>();
                Assert.IsNotNull(h.onDamaged, "Health.onDamaged не должен быть null после AddComponent");
                Assert.IsNotNull(h.onDeath, "Health.onDeath не должен быть null после AddComponent");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

// InputSystem check requires Unity.InputSystem assembly - add reference if needed
