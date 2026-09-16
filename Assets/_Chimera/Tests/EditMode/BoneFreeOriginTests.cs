using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>РОДИТЕЛЬ БЕЗ ПРОЕКЦИИ НАЧАЛА (`Bone.freeOrigin`). Грудина, скула, глазница и ухо связаны с
    /// родителем родством, но не лежат на его оси — доля `attach` ставит их не туда при любом значении.
    /// Сторожит, что флаг снимает ИМЕННО проекцию и НЕ снимает наследование поворота: иначе голова
    /// повернётся, а ухо останется на месте — ошибки нет нигде, ловится глазом на перекошенной морде.</summary>
    public class BoneFreeOriginTests
    {
        static (Vector3 pos, Quaternion rot) Place(Bone b, params Bone[] all)
        {
            var byName = new Dictionary<string, Bone>();
            foreach (var x in all) byName[x.name] = x;
            return SkeletonBuilder.Place(b, byName, new Dictionary<string, (Vector3, Quaternion)>());
        }

        static Bone Skull() => new Bone { name = "череп", length = 0.20f, origin = new Vector3(0f, 1.60f, 0f) };

        [Test]
        public void Attach_Default_StartsOnParentAxis()
        {
            var skull = Skull();
            var jaw = new Bone { name = "челюсть", parent = "череп", attach = 0.5f, length = 0.1f };

            var (pos, _) = Place(jaw, skull, jaw);

            Assert.AreEqual(1.70f, pos.y, 1e-5f, "обычная кость сошла с оси родителя");
            Assert.AreEqual(0f, pos.x, 1e-5f);
            Assert.AreEqual(0f, pos.z, 1e-5f);
        }

        [Test]
        public void FreeOrigin_StartsAtOwnOrigin_InParentFrame()
        {
            var skull = Skull();
            var ear = new Bone { name = "ухо.п", parent = "череп", freeOrigin = true, attach = 1f,
                                 origin = new Vector3(0.06f, 0.04f, -0.02f), length = 0.05f };

            var (pos, _) = Place(ear, skull, ear);

            // начало = начало родителя + собственное смещение; доля `attach` не участвует вовсе
            Assert.AreEqual(0.06f, pos.x, 1e-5f);
            Assert.AreEqual(1.64f, pos.y, 1e-5f);
            Assert.AreEqual(-0.02f, pos.z, 1e-5f);
        }

        [Test]
        public void FreeOrigin_FollowsParentRotation()
        {
            // Поворот родителя на 90° вокруг Y: смещение (0.06, 0.04, 0) уезжает по Z, а не остаётся по X.
            // Это и есть «родство осталось»: повернули голову — ухо поехало с ней
            var skull = new Bone { name = "череп", length = 0.20f, origin = new Vector3(0f, 1.60f, 0f),
                                   dir = new Vector3(0f, 90f, 0f) };
            var ear = new Bone { name = "ухо.п", parent = "череп", freeOrigin = true,
                                 origin = new Vector3(0.06f, 0.04f, 0f), length = 0.05f };

            var (pos, rot) = Place(ear, skull, ear);

            Assert.AreEqual(0f, pos.x, 1e-5f, "смещение не повернулось вместе с родителем");
            Assert.AreEqual(1.64f, pos.y, 1e-5f);
            Assert.AreEqual(-0.06f, pos.z, 1e-5f);
            Assert.AreEqual(90f, rot.eulerAngles.y, 1e-3f, "поворот родителя перестал наследоваться");
        }

        [Test]
        public void FreeOrigin_DoesNotChangeOrdinaryBones()
        {
            // Тождественность: у кости без флага числа те же, что и до правки, а `origin` не читается —
            // он есть только у корня цепи
            var skull = Skull();
            var neck = new Bone { name = "шея", parent = "череп", attach = 1f, length = 0.12f,
                                  origin = new Vector3(9f, 9f, 9f) };

            var (pos, _) = Place(neck, skull, neck);

            Assert.AreEqual(0f, pos.x, 1e-5f);
            Assert.AreEqual(1.80f, pos.y, 1e-5f);
            Assert.AreEqual(0f, pos.z, 1e-5f);
        }
    }
}
