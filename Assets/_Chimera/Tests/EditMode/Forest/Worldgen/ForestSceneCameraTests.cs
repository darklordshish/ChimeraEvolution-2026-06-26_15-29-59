using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// В игровых сценах стендовые камеры (TopCam/SandboxCam) выключены:
    /// иначе их depth 0 перекрывает Main Camera (-1) и Game view показывает не игрока.
    /// </summary>
    public class ForestSceneCameraTests
    {
        [TestCase("Assets/Scenes/ForestFragment.unity")]
        [TestCase("Assets/Scenes/ForestSandbox.unity")]
        public void GameScene_HasOnlyMainCameraEnabled(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
                    .Where(c => c.gameObject.scene == scene).ToArray();

                Assert.AreEqual(1, cameras.Count(c => c.enabled),
                    $"{path}: в игре должна быть одна включённая камера");
                var main = cameras.FirstOrDefault(c => c.CompareTag("MainCamera"));
                Assert.IsNotNull(main, $"{path}: Main Camera с тегом MainCamera");
                Assert.IsTrue(main.enabled, $"{path}: Main Camera включена");
                Assert.IsNotNull(main.GetComponent<CameraFollow>(), $"{path}: у Main Camera есть CameraFollow");

                var stand = cameras.Where(c => c != main).ToArray();
                Assert.IsTrue(stand.All(c => !c.enabled),
                    $"{path}: стендовые камеры выключены");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
