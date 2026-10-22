using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>КУСКИ МЕША ВИДА НА ЧУЖОМ ТЕЛЕ (пилот Б, спека `2026-10-09-chistyj-vid-meshem.md` §7). Кусок встаёт жёстко по
    /// шву: центр шва донора в центр шва носителя, один масштаб по отношению эллипсов. Куски ОРГАНОВ приезжают графтом,
    /// куски-ЧЕРТЫ (грива, хвост, целая голова) — ступенью идентичности; место целиком забирает только кусок, названный
    /// этим местом. Правила выбора сняты с матрицы химер 10.10: первая версия ставила кусок и своему шасси (пропали чужие
    /// уши и рога на волке). В игре куски выключены (`PartAssembly.MeshPieces`) — сторож включает их сам.</summary>
    public class PartRingFitTests
    {
        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");
        readonly List<Object> trash = new();

        [SetUp] public void SetUp() { PartAssembly.MeshPieces = true; PartAssembly.ForceTraits = null; ChainSwap.ResetCache(); }
        [TearDown]
        public void TearDown()
        {
            PartAssembly.MeshPieces = true; PartAssembly.ForceTraits = null; PartAssembly.ForceStep = 2;
            BodyMesh.ForceKey = null; BodyMesh.ForceWeight = 0f; BodyMesh.ResetCache();
            foreach (var o in trash) if (o != null) Object.DestroyImmediate(o);
            trash.Clear();
            ChainSwap.ResetCache();
        }

        static BodyPart Trait(SpeciesSO sp, string objectSlot)
        {
            var p = sp.traits?.FirstOrDefault(x => x != null && x.objectSlot == objectSlot);
            if (p == null) Assert.Inconclusive($"у вида «{sp.speciesName}» нет куска-черты «{objectSlot}» — паспорта `handoff/cherty/` не приняты");
            return p;
        }

        static List<Organ> Native(SpeciesSO sp) => sp.organs.Where(o => o != null).ToList();

        static List<Organ> With(SpeciesSO chassis, SpeciesSO donor, string slot)
        {
            var worn = chassis.organs.Where(o => o != null && o.slot != slot).ToList();
            worn.Insert(0, donor.organs.First(o => o.slot == slot));
            return worn;
        }

        GameObject Build(SpeciesSO chassis, List<Organ> worn)
        {
            var go = new GameObject("~кольцо"); trash.Add(go);
            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(go.transform, chassis, worn, null);
            return go;
        }

        /// <summary>Механика кусков под ключом мерится без черты ключа на ступени: на полном весе она заменяет черты цели целиком
        /// (объект `@<ключ>`, 19.10), и мерить было бы нечего.</summary>
        static System.IDisposable NoStages(SpeciesSO sp) => new StagesOff(sp);

        /// <summary>Рост кольца шва формой-ключом на весе 1: средний радиус кольца после сдвига к среднему до (вершины меша тела,
        /// совпавшие с точками кольца).</summary>
        static float RingGrowth(SpeciesSO sp, Vector3[] ring, string key)
        {
            var shift = new Vector3[ring.Length]; var found = new bool[ring.Length];
            foreach (var smr in sp.bodyMesh.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh; int shape = mesh != null ? mesh.GetBlendShapeIndex(key) : -1;
                if (shape < 0 || !mesh.isReadable || smr.bones.Length == 0) continue;
                var toBody = sp.bodyMesh.transform.worldToLocalMatrix * smr.bones[0].localToWorldMatrix * mesh.bindposes[0];
                var v = mesh.vertices; var dv = new Vector3[v.Length];
                mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape) - 1, dv, null, null);
                for (int i = 0; i < ring.Length; i++)
                    for (int j = 0; j < v.Length && !found[i]; j++)
                        if ((toBody.MultiplyPoint3x4(v[j]) - ring[i]).sqrMagnitude < 4e-6f) { shift[i] = toBody.MultiplyVector(dv[j]); found[i] = true; }
            }
            int n = found.Count(f => f);
            if (n < 3) Assert.Inconclusive("кольцо шва не нашлось среди вершин меша тела");
            Vector3 c0 = Vector3.zero, c1 = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) if (found[i]) { c0 += ring[i] / n; c1 += (ring[i] + shift[i]) / n; }
            float r0 = 0f, r1 = 0f;
            for (int i = 0; i < ring.Length; i++) if (found[i]) { r0 += (ring[i] - c0).magnitude; r1 += (ring[i] + shift[i] - c1).magnitude; }
            return r1 / r0;
        }
        sealed class StagesOff : System.IDisposable
        {
            readonly SpeciesSO sp; readonly KeyStage[] was;
            public StagesOff(SpeciesSO s) { sp = s; was = s.keyStages; s.keyStages = new KeyStage[0]; ChainSwap.ResetCache(); BodyMesh.ResetCache(); }
            public void Dispose() { sp.keyStages = was; ChainSwap.ResetCache(); BodyMesh.ResetCache(); }
        }

        static List<BodyPartView> Pieces(GameObject go) => go.GetComponentsInChildren<BodyPartView>(true).ToList();

        // наибольшее расстояние от центра вершин — мера размера, не зависящая от поворота куска
        static float Span(Vector3[] v)
        {
            var c = v.Aggregate(Vector3.zero, (a, b) => a + b) / v.Length;
            return v.Max(p => (p - c).magnitude);
        }

        static Vector3[] Posed(SkinnedMeshRenderer smr, Transform root)
        {
            var m = new Mesh(); smr.BakeMesh(m, true);
            var v = m.vertices.Select(p => root.InverseTransformPoint(smr.transform.TransformPoint(p))).ToArray();
            Object.DestroyImmediate(m);
            return v;
        }

        /// <summary>ОБОРОТЕНЬ ПОЗДНЕЙ СТУПЕНИ НА МЕШЕ ЧЕЛОВЕКА: черты волка проступили — целая голова забрала место головы,
        /// грива села воротником и места не забрала, хвост встал на посадочное кольцо крестца. Тело — объекты меша человека.</summary>
        [Test]
        public void WolfTraits_OnHumanMesh_HeadTakesItsPlace_ManeAndTailSitOnTop()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Trait(wolf, "голова"); Trait(wolf, "Шкура"); Trait(wolf, "Хвост");
            if (human.bodyMesh == null) Assert.Inconclusive("у человека нет меша вида — `Models/chelovek_mesh.fbx` не принят");
            PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = 2;
            var go = Build(human, Native(human));
            var root = go.transform.Find("Morph");

            var pieces = Pieces(go);
            CollectionAssert.IsSubsetOf(new[] { "голова", "Шкура", "Хвост" }, pieces.Select(p => p.name).ToList(), "проступили не все черты волка");
            var whole = pieces.Where(p => !string.IsNullOrEmpty(p.whole)).Select(p => p.whole).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { "голова", "Хвост" }, whole, "место целиком забирают голова и хвост; грива и морда — нет");

            // тело — меш человека без его головы: ни поля, ни деталей органов
            var own = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.GetComponent<BodyPartView>() == null).ToList();
            var src = new HashSet<string>(human.bodyMesh.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh.vertexCount + "|" + r.name.Split('.')[0]));
            CollectionAssert.IsNotEmpty(own, "тело шасси не нарисовано");
            foreach (var r in own)
                Assert.IsTrue(src.Contains(r.sharedMesh.vertexCount + "|" + r.name), $"рендерер «{r.name}» — не объект меша шасси: тело собрано полем");
            CollectionAssert.DoesNotContain(own.Select(r => r.name), "голова", "своя голова шасси осталась под чужой");
            CollectionAssert.IsEmpty(go.GetComponentsInChildren<MeshRenderer>(true), "у химеры на меше детали органов не рисуются");

            // голова: один масштаб шва и взгляд вперёд — морда дальше всего по +Z, а не вверх
            Assert.IsTrue(PartAssembly.Seam(human, head.seam, out var neck), "у человека нет шва шеи");
            // голова обязана накрыть культю шеи — масштаб по большему из отношений полуосей
            float k = Mathf.Max(neck.ellipse.x / head.ellipse.x, neck.ellipse.y / head.ellipse.y);
            Assert.That(k, Is.InRange(0.3f, 1f), "шея человека тоньше волчьей — масштаб обязан быть меньше единицы");
            var skull = pieces.First(p => p.name == "голова").GetComponent<SkinnedMeshRenderer>();
            // меш донора — тем же путём, что посадка: с формой-ключом плана носителя (у черепа волка ключ «двуногий» опускает
            // дно юбки под точку посадки — форма законно другая, масштаб тот же)
            var raw = head.mesh.vertices;
            int key = head.mesh.GetBlendShapeIndex(human.Plan);
            if (key >= 0 && head.plan != human.Plan)
            {
                var dv = new Vector3[raw.Length];
                head.mesh.GetBlendShapeFrameVertices(key, head.mesh.GetBlendShapeFrameCount(key) - 1, dv, null, null);
                for (int i = 0; i < raw.Length; i++) raw[i] += dv[i];
            }
            var donorVerts = raw.Select(v => head.toBody.MultiplyPoint3x4(v)).ToArray();
            Assert.AreEqual(Span(donorVerts) * k, Span(skull.sharedMesh.vertices), Span(donorVerts) * 0.01f, "голова растянута не одним масштабом шва");
            // морда (объект `Пасть` целой головы) лежит впереди шеи, а не над ней: самая дальняя точка черепа — кончик уха
            var muzzle = Posed(pieces.First(p => p.name == BodySlots.Maw).GetComponent<SkinnedMeshRenderer>(), root);
            var nose = muzzle.Aggregate(Vector3.zero, (a, b) => a + b) / muzzle.Length - neck.center;
            Assert.Greater(nose.z, Mathf.Abs(nose.y), "волчья голова на человеке смотрит не вперёд");

            // хвост: сел на посадочное кольцо крестца и уходит от него наружу, а не внутрь тела
            Assert.IsTrue(PartAssembly.Seam(human, "корень хвоста", out var seat), "у человека нет посадочного места хвоста");
            var tail = Posed(pieces.First(p => p.name == "Хвост").GetComponent<SkinnedMeshRenderer>(), root);
            Assert.Less(tail.Min(p => (p - seat.center).magnitude), 0.08f, "хвост не у посадочного кольца");
            // «наружу» мерится НАЧАЛОМ хвоста, а не кончиком: хвост на двуногом висит дугой вниз-назад (ключ «двуногий», лист
            // ступеней; геймдизайнер 15.10), и кончик законно опущен почти поперёк оси шва. Начало — ближняя треть по удалению
            var reach = tail.Max(p => (p - seat.center).magnitude);
            var near = tail.Where(p => (p - seat.center).magnitude < reach / 3f).ToArray();
            var start = near.Aggregate(Vector3.zero, (a, b) => a + b) / near.Length - seat.center;
            Assert.Greater(Vector3.Dot(start.normalized, seat.axis.normalized), 0.3f, "хвост начинается не наружу от крестца");
            var tip = tail.OrderByDescending(p => (p - seat.center).magnitude).First() - seat.center;
            Assert.Less(tip.z, 0f, "кончик хвоста ушёл вперёд — хвост обязан висеть позади крупа");
        }

        [Test]
        public void TraitsAppearByStep_HeadOnlyAtLateStep()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            Trait(wolf, "голова"); Trait(wolf, "Шкура");
            PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = 1;
            var names = Pieces(Build(human, Native(human))).Select(p => p.name).ToList();
            CollectionAssert.Contains(names, "Шкура", "грива — черта первой ступени");
            CollectionAssert.DoesNotContain(names, "голова", "целая голова — общая форма вида, проступает с поздней ступени");
        }

        [Test]
        public void OwnChassis_NeverTakesItsPieces()
        {
            var wolf = Load("Волк"); var elk = Load("Лось");
            // волк с чужим Чутьём — на поле; свои куски ему не ставятся (своя голова проглотила бы лосиные уши)
            CollectionAssert.IsEmpty(Pieces(Build(wolf, With(wolf, elk, BodySlots.Sense))).Where(p => p.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.Contains("кусок")),
                                     "своему шасси кусок меша вида не ставится");
        }

        [Test]
        public void OrganPiece_WithoutCarrierSeam_KeepsOldPath()
        {
            var wolf = Load("Волк"); var snake = Load("Змея");
            var muzzle = wolf.parts?.FirstOrDefault(p => p != null && p.piece && p.slot == BodySlots.Maw);
            if (muzzle == null) Assert.Inconclusive("у волка нет куска морды — паспорт `volk-morda` не принят");
            Assert.IsFalse(PartAssembly.Seam(snake, muzzle.seam, out _), "у змеи появился шов морды — правило пора пересмотреть");
            CollectionAssert.IsEmpty(Pieces(Build(snake, With(snake, wolf, BodySlots.Maw))).Where(p => p.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.Contains("кусок")),
                                     "кусок встал на тело без шва");
        }

        /// <summary>КЛЮЧ = ИДЕНТИЧНОСТЬ, КУСОК = ОРГАН (15.10): ключ вида двигает кожу носителя, и кусок органа обязан сесть по
        /// шву, который ключ уже сдвинул, — иначе на весе 0.7 волчьи уши висят там, где были уши человека. Кольцо ушей
        /// человека — вершины его меша, сдвиг читается из самой формы-ключа.</summary>
        [Test]
        public void OrganPiece_OnKeyedBody_FollowsTheSeam()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            if (human.bodyMesh == null) Assert.Inconclusive("у человека нет меша вида — `Models/chelovek_mesh.fbx` не принят");
            Vector3 Ears(float w)
            {
                BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = w; BodyMesh.ResetCache(); ChainSwap.ResetCache();
                var go = Build(human, With(human, wolf, BodySlots.Sense));
                var ears = Pieces(go).FirstOrDefault(p => p.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.Contains("кусок") && p.name == BodySlots.Sense);
                if (ears == null) Assert.Inconclusive("уши волка кусками на человеке не встали — паспорт ушей или кольцо уха человека не приняты");
                var v = Posed(ears.GetComponent<SkinnedMeshRenderer>(), go.transform);
                return v.Aggregate(Vector3.zero, (a, b) => a + b) / v.Length;
            }
            var bodyKeyed = human.bodyMesh.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("Волк") >= 0);
            if (!bodyKeyed) Assert.Inconclusive("у меша человека нет формы-ключа «Волк»");
            using var _ = NoStages(human);
            float moved = (Ears(1f) - Ears(0f)).magnitude;
            Assert.Greater(moved, 0.01f, $"на полном весе ключа уши волка сдвинулись на {moved * 100f:0.0} см — сидят на шве базы, а не на шве под ключом");
        }

        /// <summary>ЧЕРТЫ РАСТУТ С ТЕЛОМ (19.10): масштаб куска берётся с шва ПОСЛЕ ключа. Голова волка на человеке при весе 1
        /// больше, чем при весе 0, ровно во столько, во сколько эллипс шеи под ключом больше базового (масштаб «накрыть» —
        /// по большему из отношений полуосей).</summary>
        [Test]
        public void PieceScale_FollowsSeamUnderKey()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Trait(wolf, "голова");
            // 22.10: записи `pod_klyuchom` сняты — эллипс шва под ключом считается из самой кожи: отношение средних радиусов
            // кольца шва после и до сдвига формой-ключом. Запись есть — эллипс берётся из неё
            var ring = (human.traits ?? new BodyPart[0]).Concat(human.parts ?? new BodyPart[0]).FirstOrDefault(p => p != null && p.seam == head.seam && p.ringM != null && p.ringM.Length >= 3);
            if (ring == null || human.bodyMesh == null) Assert.Inconclusive("у шва головы человека нет кольца");
            Assert.IsTrue(PartAssembly.Seam(human, head.seam, out var neck));
            float K(Vector2 e) => Mathf.Max(e.x / head.ellipse.x, e.y / head.ellipse.y);
            var rec = ring.keyed?.FirstOrDefault(k => k != null && k.key == "Волк" && k.ellipse.x > 0f && k.ellipse.y > 0f);
            float expected = rec != null ? K(rec.ellipse) / K(neck.ellipse) : RingGrowth(human, ring.ringM, "Волк");
            float Size(float w)
            {
                BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = w; BodyMesh.ResetCache(); ChainSwap.ResetCache();
                PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = 2;
                var go = Build(human, Native(human));
                var skull = Pieces(go).First(p => p.name == "голова");
                return Span(skull.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices);
            }
            using var _ = NoStages(human);
            float got = Size(1f) / Size(0f);
            Assert.AreEqual(expected, got, 0.01f, "голова на весе 1 не выросла вместе со швом под ключом");
        }

        /// <summary>КЛЮЧ ПЛАНА — ТОЛЬКО ПО СПИСКУ ПАСПОРТА (19.10): графт волчьей Пасти на двуногом встаёт без формы-ключа «двуногий»
        /// (в паспорте морды `keys: []`), даже если сам меш ключ несёт — тот же объект у черты-головы его использует.</summary>
        [Test]
        public void GraftMuzzle_OnBiped_NotShiftedByPlanKey()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var muzzle = wolf.parts?.FirstOrDefault(p => p != null && p.piece && p.slot == BodySlots.Maw);
            if (muzzle == null) Assert.Inconclusive("у волка нет куска морды");
            if (muzzle.keys != null && System.Array.IndexOf(muzzle.keys, human.Plan) >= 0) Assert.Inconclusive("паспорт морды перечисляет ключ «двуногий» — проверять нечего");
            Vector3 Center(bool planKey)
            {
                bool was = PartAssembly.UsePlanKey; PartAssembly.UsePlanKey = planKey;
                try
                {
                    ChainSwap.ResetCache();
                    var go = Build(human, With(human, wolf, BodySlots.Maw));
                    var p = Pieces(go).First(x => x.name == BodySlots.Maw && x.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.Contains("кусок"));
                    var v = Posed(p.GetComponent<SkinnedMeshRenderer>(), go.transform.Find("Morph"));
                    return v.Aggregate(Vector3.zero, (a, b) => a + b) / v.Length;
                }
                finally { PartAssembly.UsePlanKey = was; }
            }
            Assert.Less((Center(true) - Center(false)).magnitude, 0.001f, "графт Пасти сдвинут ключом плана, которого нет в его паспорте");
        }

        /// <summary>ОРГАН НА ЗАБРАННОМ МЕСТЕ — НА МЕСТАХ САМОГО КУСКА (19.10): волчьи уши графтом при волчьей голове-черте садятся на
        /// основания уха волчьего черепа (`places` куска), а не на шов уха человека сбоку черепа. Тот же донор — тот же перенос:
        /// ухо выше центра черепа, как у волка.</summary>
        [Test]
        public void OrganOnClaimedHead_SitsOnHeadPlaces()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Trait(wolf, "голова");
            if (head.places == null || head.places.Length == 0) Assert.Inconclusive("у волчьей головы нет мест органов (`places`)");
            PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = 2;
            var go = Build(human, With(human, wolf, BodySlots.Sense));
            var root = go.transform.Find("Morph");
            var pieces = Pieces(go);
            var ears = pieces.FirstOrDefault(p => p.name == BodySlots.Sense && p.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.Contains("кусок"));
            var skull = pieces.FirstOrDefault(p => p.name == "голова");
            if (ears == null || skull == null) Assert.Inconclusive("уши или голова волка кусками не встали");
            Vector3 C(BodyPartView v) { var a = Posed(v.GetComponent<SkinnedMeshRenderer>(), root); return a.Aggregate(Vector3.zero, (x, y) => x + y) / a.Length; }
            var e = C(ears); var h = C(skull);
            Assert.Greater(e.y, h.y, $"уши ниже центра волчьего черепа на {(h.y - e.y) * 100f:0.0} см — сели на шов уха человека, а не на места куска");
        }

        /// <summary>РОДНЫЕ УШИ НА ЧУЖОЙ ГОЛОВЕ (q-ushi-st2 «давай А», 19.10): на ст.2 без графта Чутья волчья голова забирает
        /// место головы, а уши человека — кусок его Чутья — встают на места ушей волчьей головы; объект ушей в меше человека снят.</summary>
        [Test]
        public void NativeEars_OnForeignHead_AtHeadPlaces()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Trait(wolf, "голова");
            if (head.places == null || head.places.Length == 0) Assert.Inconclusive("у волчьей головы нет мест органов");
            if (human.parts == null || !human.parts.Any(p => p != null && p.piece && p.slot == BodySlots.Sense)) Assert.Inconclusive("у человека нет куска ушей");
            PartAssembly.ForceTraits = wolf; PartAssembly.ForceStep = 2;
            var go = Build(human, Native(human));
            var root = go.transform.Find("Morph");
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var ears = smrs.Where(r => r.name == BodySlots.Sense).ToList();
            Assert.AreEqual(1, ears.Count, "уши человека: объект меша и кусок разом (или ни одного)");
            Assert.IsTrue(ears[0].sharedMesh.name.Contains("кусок"), "уши человека нарисованы объектом меша, а не куском на местах головы");
            Vector3 C(SkinnedMeshRenderer r) { var a = Posed(r, root); return a.Aggregate(Vector3.zero, (x, y) => x + y) / a.Length; }
            Assert.Greater(C(ears[0]).y, C(smrs.First(r => r.name == "голова")).y, "уши ниже центра волчьего черепа");
        }

        /// <summary>ВАРИАНТ Б (19.10): в игре куски включены, и химера на шасси с мешем вида рисуется мешем, даже если у
        /// чужого органа ещё нет куска (до 19.10 такая химера уходила на поле — 3 химеры из 78 на меше).</summary>
        [Test]
        public void InGame_ChimeraStaysOnMesh_EvenWithoutPiece()
        {
            PartAssembly.MeshPieces = true;
            ChainSwap.ResetCache();
            var human = Load("Человек"); var elk = Load("Лось");
            if (human.bodyMesh == null) Assert.Inconclusive("у человека нет меша вида");
            var go = Build(human, With(human, elk, BodySlots.Legs));
            CollectionAssert.IsEmpty(go.GetComponentsInChildren<MeshRenderer>(true), "химера ушла на поле: у поля детали-блоки, у меша их нет");
            CollectionAssert.IsNotEmpty(go.GetComponentsInChildren<SkinnedMeshRenderer>(true), "тело не нарисовано");
        }
    }
}
