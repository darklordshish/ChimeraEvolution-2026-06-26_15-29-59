using System.Collections.Generic;
using UnityEngine;

namespace Chimera.Forest.Water
{
    [System.Flags]
    public enum WaterBodyKind
    {
        None = 0,
        River = 1 << 0,
        Lake = 1 << 1,
        Reservoir = 1 << 2,
        Waterfall = 1 << 3,
    }

    public enum AqueductType
    {
        Absent = 0,
        River = 1,
        Lake = 2,
        Reservoir = 3,
        WaterfallSpillway = 4,
    }

    public readonly struct AqueductWaterSeed
    {
        public readonly int Seed;
        public readonly WaterBodyKind Bodies;
        public readonly AqueductType Primary;
        public readonly Vector2 LakeCenter;
        public readonly float LakeLevel;
        public readonly float LakeRadius;
        public readonly float RiverLength;
        public readonly Vector2 RiverMid;
        public readonly float RiverLevel;
        public readonly bool HasWaterfall;
        public readonly float FallHeight;
        public readonly Vector2 FallMouth;
        public readonly Vector2 FallLip;
        public readonly string ReservoirReason;
        public readonly string AbsentReason;

        public AqueductWaterSeed(int seed, WaterBodyKind bodies, AqueductType primary, Vector2 lakeCenter, float lakeLevel, float lakeRadius, float riverLength, Vector2 riverMid, float riverLevel, bool hasWaterfall, float fallHeight, Vector2 fallMouth, Vector2 fallLip, string reservoirReason, string absentReason)
        {
            Seed = seed; Bodies = bodies; Primary = primary;
            LakeCenter = lakeCenter; LakeLevel = lakeLevel; LakeRadius = lakeRadius;
            RiverLength = riverLength; RiverMid = riverMid; RiverLevel = riverLevel;
            HasWaterfall = hasWaterfall; FallHeight = fallHeight; FallMouth = fallMouth; FallLip = fallLip;
            ReservoirReason = reservoirReason; AbsentReason = absentReason;
        }

        public string PrimaryCode => AqueductWaterCatalog.CodeOf(Primary);

        public bool HasLake => (Bodies & WaterBodyKind.Lake) != 0;
        public bool HasRiver => (Bodies & WaterBodyKind.River) != 0;
        public bool HasReservoir => (Bodies & WaterBodyKind.Reservoir) != 0;
    }

    /// <summary>Task 0: каталог воды 20 сидов v2/2000/48. Это данные, не геометрия.</summary>
    /// <summary>Task 1: выбор по сиду. Планирует объект, но не строит геометрию.</summary>
    public readonly struct AqueductPlacementPlan
    {
        public readonly int Seed;
        public readonly bool ShouldBuild;
        public readonly AqueductType Type;
        public readonly string Reason;

        public AqueductPlacementPlan(int seed, bool shouldBuild, AqueductType type, string reason)
        {
            Seed = seed; ShouldBuild = shouldBuild; Type = type; Reason = reason;
        }

        public string Code => AqueductWaterCatalog.CodeOf(Type);
    }
    public static class AqueductWaterCatalog
    {
        public const int WorldVersion = 2;
        public const float MapDiameter = 2000f;
        public const int TerrainQuads = 48;
        /// <summary>AW-RS пока не имеет модели; выбор обязан вернуть долг, а не подменить озером.</summary>
        public const bool ReservoirModelAvailable = false;

        public static readonly IReadOnlyList<AqueductWaterSeed> Seeds = new[]
        {
            new AqueductWaterSeed(1, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(49.196f, 593.29f), -5.31f, 150f, 589.567f, new Vector2(230.469f, -738.281f), -14.708f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(2, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(673.433f, 0.97f), -2.419f, 150f, 598.891f, new Vector2(410.156f, 785.156f), -11.519f, true, 17.787f, new Vector2(739.363f, 162.075f), new Vector2(806.39f, 320.988f), "not modeled in v2", ""),
            new AqueductWaterSeed(3, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(536.93f, -19.588f), -24.335f, 150f, 595.765f, new Vector2(-277.344f, 714.844f), -13.514f, true, 16.717f, new Vector2(703.233f, -97.895f), new Vector2(849.75f, -159.371f), "not modeled in v2", ""),
            new AqueductWaterSeed(4, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(52.671f, -47.914f), -2.303f, 150f, 598.994f, new Vector2(-535.156f, -753.906f), -27.096f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(5, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(210.201f, -466.63f), 4.005f, 82.461f, 596.091f, new Vector2(738.281f, 3.906f), -42.595f, true, 12.262f, new Vector2(107.625f, -500.299f), new Vector2(-35.077f, -555.892f), "not modeled in v2", ""),
            new AqueductWaterSeed(6, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(490.81f, 9.38f), 5.489f, 150f, 589.687f, new Vector2(-50.781f, 761.719f), -19.166f, true, 16.46f, new Vector2(629.598f, -106.972f), new Vector2(808.152f, -266.54f), "not modeled in v2", ""),
            new AqueductWaterSeed(7, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(-55.66f, -53.69f), -6.469f, 150f, 589.67f, new Vector2(-519.531f, -777.344f), -20.136f, true, 12.373f, new Vector2(64.094f, 87.608f), new Vector2(161.435f, 202.217f), "not modeled in v2", ""),
            new AqueductWaterSeed(8, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(-655.26f, -483.229f), 0.061f, 116.784f, 585.896f, new Vector2(-433.594f, 769.531f), -31.056f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(9, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(-26.14f, -427.166f), -13.339f, 150f, 595.032f, new Vector2(-144.531f, 769.531f), -12.435f, true, 14.681f, new Vector2(150.85f, -393.235f), new Vector2(356.839f, -356.785f), "not modeled in v2", ""),
            new AqueductWaterSeed(10, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(119.678f, -196.716f), -13.415f, 150f, 594.264f, new Vector2(167.969f, -738.281f), -16.321f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(11, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(-391.394f, 464.94f), -7.323f, 150f, 596.979f, new Vector2(339.844f, 738.281f), -31.585f, true, 12.826f, new Vector2(-575.531f, 432.288f), new Vector2(-733.053f, 397.84f), "not modeled in v2", ""),
            new AqueductWaterSeed(12, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(48.237f, -8.26f), 1.455f, 150f, 581.354f, new Vector2(-738.281f, 449.219f), -19.424f, true, 13.222f, new Vector2(222.619f, -50.615f), new Vector2(367.637f, -94.133f), "not modeled in v2", ""),
            new AqueductWaterSeed(13, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(314.891f, 503.771f), 10.479f, 150f, 591.412f, new Vector2(-863.281f, -386.719f), -21.95f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(14, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(-22.99f, -202.99f), -6.95f, 150f, 595.954f, new Vector2(-42.969f, 730.469f), -29.256f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(15, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(551.015f, -176.776f), 7.322f, 150f, 587.62f, new Vector2(-730.469f, 503.906f), -23.907f, true, 16.761f, new Vector2(679.912f, -53.006f), new Vector2(841.653f, 108.244f), "not modeled in v2", ""),
            new AqueductWaterSeed(16, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(570.112f, -32.679f), -13.088f, 150f, 591.36f, new Vector2(261.719f, -74.219f), -14.66f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(17, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(856.23f, 217.741f), 16.002f, 72.023f, 598.26f, new Vector2(519.531f, 769.531f), -17.159f, true, 14.066f, new Vector2(832.47f, 113.482f), new Vector2(801.671f, -54.012f), "not modeled in v2", ""),
            new AqueductWaterSeed(18, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(-163.464f, 207.689f), -3.561f, 150f, 595.032f, new Vector2(-386.719f, 722.656f), -19.204f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(19, WaterBodyKind.River | WaterBodyKind.Lake, AqueductType.Lake, new Vector2(204.307f, -487.645f), -7.873f, 150f, 598.72f, new Vector2(-746.094f, 597.656f), -15.167f, false, 0f, new Vector2(0f, 0f), new Vector2(0f, 0f), "not modeled in v2", ""),
            new AqueductWaterSeed(20, WaterBodyKind.River | WaterBodyKind.Lake | WaterBodyKind.Waterfall, AqueductType.WaterfallSpillway, new Vector2(137.177f, -535.243f), -5.154f, 150f, 582.105f, new Vector2(-269.531f, 722.656f), -22.689f, true, 15.827f, new Vector2(-29.332f, -458.718f), new Vector2(-241.83f, -362.006f), "not modeled in v2", ""),
        };

        public static bool TryGet(int seed, out AqueductWaterSeed row)
        {
            foreach (var r in Seeds) if (r.Seed == seed) { row = r; return true; }
            row = default; return false;
        }

        public static AqueductPlacementPlan PlanFor(int seed)
        {
            if (!TryGet(seed, out var row))
                return new AqueductPlacementPlan(seed, false, AqueductType.Absent, "seed not in catalog");
            return PlanFor(row.Seed, row.Bodies, ReservoirModelAvailable);
        }

        public static AqueductPlacementPlan PlanFor(int seed, WaterBodyKind bodies,
            bool reservoirModelAvailable = false)
        {
            var type = PrimaryFor(bodies);
            if (type == AqueductType.Absent)
                return new AqueductPlacementPlan(seed, false, type, "no water body");
            if (type == AqueductType.Reservoir && !reservoirModelAvailable)
                return new AqueductPlacementPlan(seed, false, type,
                    "AW-RS model not delivered; absent/debt, no lake fallback");
            return new AqueductPlacementPlan(seed, true, type, "water: " + CodeOf(type));
        }

        public static string CodeOf(AqueductType type)

        {
            switch (type)
            {
                case AqueductType.River: return "AW-R";
                case AqueductType.Lake: return "AW-L";
                case AqueductType.Reservoir: return "AW-RS";
                case AqueductType.WaterfallSpillway: return "AW-F";
                default: return "absent";
            }
        }

        public static AqueductType PrimaryFor(WaterBodyKind bodies)
        {
            if ((bodies & WaterBodyKind.Waterfall) != 0) return AqueductType.WaterfallSpillway;
            if ((bodies & WaterBodyKind.Reservoir) != 0) return AqueductType.Reservoir;
            if ((bodies & WaterBodyKind.Lake) != 0) return AqueductType.Lake;
            if ((bodies & WaterBodyKind.River) != 0) return AqueductType.River;
            return AqueductType.Absent;
        }
    }
}
