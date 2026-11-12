using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 1: treeVersion — только v2. У v1 хэш тождественен, ключ эрозии
    /// по-прежнему (seed, worldVersion, erosionVersion, resolution).
    /// </summary>
    public class TreeFingerprintTests
    {
        const ulong H = 0x0123456789abcdefUL;

        [Test]
        public void V1_TreeVersion_IsIdentity()
        {
            Assert.AreEqual(H, WorldFingerprint.MixTreeVersion(H, 1, 2));
            Assert.AreEqual(H, WorldFingerprint.MixTreeVersion(H, 1, 0));
        }

        [Test]
        public void V2_TreeVersion_ChangesFingerprint()
        {
            ulong a = WorldFingerprint.MixTreeVersion(H, 2, 0);
            ulong b = WorldFingerprint.MixTreeVersion(H, 2, 2);
            ulong c = WorldFingerprint.MixTreeVersion(H, 2, 3);
            Assert.AreNotEqual(a, b, "treeVersion не вошёл в v2-отпечаток");
            Assert.AreNotEqual(b, c, "смена treeVersion не меняет v2-отпечаток");
            Assert.AreEqual(b, WorldFingerprint.MixTreeVersion(H, 2, 2),
                "тот же treeVersion — тот же отпечаток");
        }

        [Test]
        public void FingerprintVersion_IsStillV1()
        {
            Assert.AreEqual(1, WorldFingerprint.Version,
                "fingerprintVersion не поднимается ради treeVersion");
        }

        [Test]
        public void ErosionKey_HasNoTreeVersion()
        {
            string before = DomeErosion.Key(1337, 2, 3, 128);
            string same = DomeErosion.Key(1337, 2, 3, 128);
            Assert.AreEqual(before, same);
            StringAssert.DoesNotContain("tree", before);
            // Ключ реагирует на свои поля, а не на treeVersion.
            Assert.AreNotEqual(before, DomeErosion.Key(1338, 2, 3, 128));
            Assert.AreNotEqual(before, DomeErosion.Key(1337, 3, 3, 128));
            Assert.AreNotEqual(before, DomeErosion.Key(1337, 2, 4, 128));
            Assert.AreNotEqual(before, DomeErosion.Key(1337, 2, 3, 256));
        }
    }
}
