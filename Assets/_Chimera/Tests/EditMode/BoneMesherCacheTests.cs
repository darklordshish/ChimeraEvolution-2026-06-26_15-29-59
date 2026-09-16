using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>МЁРТВАЯ ЗАПИСЬ КЭША ОБОЛОЧКИ. `BoneMesher` кэширует меши по виду в статическом словаре, а
    /// Unity уничтожает меши, на которые не ссылается сцена (выход из Play, выгрузка сцены,
    /// `UnloadUnusedAssets`). Словарь отдаёт уничтоженный меш, и существо собирается невидимым — без единой
    /// ошибки. Поймано 17.09 на волке после PlayMode-тестов: в кэше 7 мешей, живых 0.</summary>
    public class BoneMesherCacheTests
    {
        GameObject go;

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        static SpeciesSO Species()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "~кэш-оболочки";
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[] { new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.5f, 0f), baseSize = new Vector3(0.3f, 0.3f, 0.6f) } };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } };
            so.bones = new[]
            {
                new Bone { name = "хребет", socket = "хребет", origin = new Vector3(0f, 0.5f, -0.3f),
                           dir = new Vector3(90f, 0f, 0f), length = 0.6f, r0 = 0.15f, r1 = 0.15f },
            };
            return so;
        }

        Mesh Shell(SpeciesSO so)
        {
            go = new GameObject("~ТестКэша");
            MorphBuilder.Build(go.transform, so, so.organs.ToList());
            var sk = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
            Assert.IsNotNull(sk, "оболочка поля не построена вовсе");
            return sk.sharedMesh;
        }

        [Test]
        public void DestroyedCachedMesh_IsRebuilt_NotReturnedDead()
        {
            var so = Species();

            var first = Shell(so);
            Assert.IsNotNull(first, "первая сборка дала пустую оболочку");
            Object.DestroyImmediate(go);

            // то, что делает Unity при выходе из Play или выгрузке сцены: меш уничтожен, ссылка в кэше осталась
            Object.DestroyImmediate(first);

            var second = Shell(so);
            Assert.IsTrue(second != null, "кэш отдал уничтоженный меш — существо было бы невидимым");
            Assert.Greater(second.vertexCount, 0, "пересобранная оболочка пуста");
        }
    }
}
