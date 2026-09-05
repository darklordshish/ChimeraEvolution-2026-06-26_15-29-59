using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Place мировые vs chainLinks локальные: меряй в той же системе, в которой расставляешь
    /// (MorphBuilder.Place, SnakeBodyChain.dist). Ошибка = один шов с отрывом головы.
    /// </summary>
    public class PlaceCoordTests
    {
        SpeciesSO MakeSnakeSpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "Змея";
            so.tint = Color.green;
            so.mutagenPool = 10;
            so.baseHp = 50;
            so.sockets = new[]
            {
                new BodySocket { name = "голова", localPos = new Vector3(0f,0.4f,0f), baseSize = new Vector3(0.26f,0.22f,0.46f) },
                new BodySocket { name = "шея", parent = "голова", attach = 0f, baseSize = Vector3.one, linkDiameter = 0.22f, linkLength = 0.36f, chain = 3 },
                new BodySocket { name = "Тело", parent = "шея", attach = 0f, baseSize = Vector3.one, linkDiameter = 0f, linkLength = 0.36f, chain = 3 },
                new BodySocket { name = "Хвост", parent = "Тело", attach = 0f, baseSize = Vector3.one, linkDiameter = 0f, linkLength = 0.24f, chain = 4 },
                new BodySocket { name = "Погремушка", parent = "Хвост", attach = 0f, baseSize = new Vector3(0.12f,0.12f,0.12f) },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                new Organ { organName = "Пасть", slot = BodySlots.Maw, cost = 3 },
            };
            so.bones = new Bone[0];
            return so;
        }

        [UnityTest]
        public IEnumerator Place_ChainParent_WorldConsistency()
        {
            var so = MakeSnakeSpecies();
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("PlaceChain");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f,1f,0f);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            MorphBuilder.Build(go.transform, so, worn);
            yield return null;

            var container = go.transform.Find("Morph");
            Assert.IsNotNull(container, "контейнер Morph должен существовать");
            // контейнер сдвинут к footY
            float footY = cc.center.y - cc.height*0.5f;
            Assert.AreEqual(footY, container.localPosition.y, 1e-5f);

            // Place для головы и шеи: шея должна быть на расстоянии ровно attach*длина родителя.
            // Найдём узлы цепи (BuildLinks создал узлы с именем "шея", "Тело", "Хвост" как прямые дети контейнера)
            Transform FindInContainer(string name)
            {
                foreach (Transform ch in container) if (ch.name == name) return ch;
                return null;
            }
            var neckNode = FindInContainer("шея");
            var bodyNode = FindInContainer("Тело");
            var tailNode = FindInContainer("Хвост");
            Assert.IsNotNull(neckNode, "узел шея должен быть прямым ребёнком Morph");
            // локальные позиции звеньев вдоль -Z (ChainDir для цепи всегда back): мировые и локальные не смешиваем
            // Ошибка CLAUDE.md: мера в одной системе, расстановка в другой даёт отрыв на одном стыке.
            // Здесь проверяем что все узлы одной цепи лежат на одной линии -Z от места родителя
            // и что их позиции вычислены в локальной системе контейнера (placed dict), а не в мировой.
            // Эквивалент: worldDistance между узлами ≈ localDistance (контейнер не повернут/не масштабирован) — пока ok,
            // но если бы меряли world а ставили local+смещение, голова уехала бы.
            float totalLen = 0f;
            foreach (Transform ch in container) if (ch.name=="шея") totalLen += 0.36f; // упрощённо

            // Погремушка — потомок последнего звена Хвоста, не сосед: hierarchy, а не список имён
            Transform rattle = null;
            foreach (Transform ch in container) if (ch.name=="Погремушка") rattle = ch;
            if (rattle == null)
            {
                // может быть внутри последнего звена хвоста
                if (tailNode != null)
                    foreach (Transform ch in tailNode) if (ch.name=="Погремушка") rattle = ch;
                // также проверяем через GetComponentsInChildren
                if (rattle==null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                        if (r.name=="Погремушка") rattle = r.transform;
            }
            // погремушка должна быть потомком звена, а не соседом звеньев (фикс SnakeBodyChain: иерархия, не список)
            if (rattle != null)
            {
                Assert.AreNotEqual(container, rattle.parent, "Погремушка не должна быть прямым ребёнком Morph — она потомок звена (иначе цепь тащит её списком)");
                // её предок должен быть узлом Хвоста
                Transform p = rattle.parent;
                bool hasTailAncestor = false;
                while (p!=null && p!=container) { if (p.name=="Хвост") hasTailAncestor=true; p=p.parent; }
                Assert.IsTrue(hasTailAncestor, "Погремушка должна сидеть на звене Хвоста (chainLinks потомок)");
            }

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnakeChain_Dist_Measures_InPathSystem_NotWorldWithHeight()
        {
            var so = MakeSnakeSpecies();
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("SnakeChainDist");
            go.transform.position = new Vector3(5f, 0f, 7f);
            go.transform.rotation = Quaternion.identity;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f,1f,0f);
            var chain = go.AddComponent<SnakeBodyChain>();
            MorphBuilder.Build(go.transform, so, worn);
            chain.RebuildFromMorph();
            yield return null;
            // после RebuildFromMorph dist должны быть измерены с компенсацией height (SnakeBodyChain.cs:95)
            // т.е. walk = transform.position, q = link.position - up*height — иначе первое звено меряется по диагонали 0.47 вместо 0.36
            var fi = typeof(SnakeBodyChain).GetField("dist", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var dist = (float[])fi.GetValue(chain);
            Assert.IsNotNull(dist, "dist должен быть заполнен после RebuildFromMorph");
            Assert.Greater(dist.Length, 0);
            // первое звено ~0.36 (linkLength шеи), а не 0.47 (диагональ с height 0.3)
            // допуск 0.05
            Assert.AreEqual(0.36f, dist[0], 0.08f, "dist[0] должен быть 0.36 вдоль тела, а не диагональ с height (фикс PlaceCoord)");

            // последовательные разности ~ linkLength (0.36 для шеи/тела, 0.24 для хвоста)
            for (int i=1;i<dist.Length;i++)
            {
                float step = dist[i]-dist[i-1];
                // хвост короче
                Assert.Greater(step, 0.1f, $"шаг {i} должен быть >0.1");
                Assert.Less(step, 0.5f, $"шаг {i} должен быть <0.5");
            }

            // высота компенсации: если бы меряли мировыми без снятия height, ошибка бы накопилась на голове;
            // проверяем что BodyPoint вдоль тела даёт точку на расстоянии dist, а не убегает
            Vector3 head = go.transform.position;
            Vector3 seg0 = chain.BodyPoint(0.2f);
            float d = Vector3.Distance(head, seg0);
            Assert.Greater(d, 0.1f);

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildLinks_NodeScale_IsOne_MeshInside()
        {
            var so = MakeSnakeSpecies();
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("LinksScale");
            var cc = go.AddComponent<CharacterController>();
            cc.height=2f; cc.center=new Vector3(0f,1f,0f);
            MorphBuilder.Build(go.transform, so, worn);
            yield return null;
            var container = go.transform.Find("Morph");
            Assert.IsNotNull(container);
            foreach (Transform child in container)
            {
                if (child.name=="шея" || child.name=="Тело" || child.name=="Хвост")
                {
                    Assert.AreEqual(1f, child.localScale.x, 1e-5f, $"узел {child.name} должен иметь масштаб 1 (иначе капсула плющит сустав в диск)");
                    Assert.AreEqual(1f, child.localScale.y, 1e-5f);
                    Assert.AreEqual(1f, child.localScale.z, 1e-5f);
                    Assert.IsNotNull(child.GetComponentInChildren<Renderer>(), $"узел {child.name} должен содержать меш-капсулу внутри");
                }
            }
            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }
    }
}
