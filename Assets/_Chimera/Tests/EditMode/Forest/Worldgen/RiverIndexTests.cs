using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class RiverIndexTests
    {
        [Test]
        public void Index_ReturnsSameNearestAsBruteForce()
        {
            var s = new DomeHydro.State
            {
                riverPts = new List<Vector2>
                {
                    new Vector2(0f, 0f),
                    new Vector2(10f, 0f),
                    new Vector2(20f, 0f),
                    new Vector2(30f, 0f),
                },
            };
            var index = new DomeHydro.RiverIndex(s.riverPts, 10f);

            for (float x = -5f; x <= 35f; x += 2.5f)
                for (float z = -5f; z <= 5f; z += 2.5f)
                {
                    int brute = -1;
                    float best = float.PositiveInfinity;
                    for (int i = 0; i < s.riverPts.Count; i++)
                    {
                        float d = Vector2.Distance(new Vector2(x, z), s.riverPts[i]);
                        if (d < best) { best = d; brute = i; }
                    }
                    int got = index.Nearest(s, x, z, out float gotDist);
                    Assert.AreEqual(brute, got, $"точка ({x};{z})");
                    Assert.AreEqual(best, gotDist, 1e-3f, $"дистанция ({x};{z})");
                }
        }
    }
}
