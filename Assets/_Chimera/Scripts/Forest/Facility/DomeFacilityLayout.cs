using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Корпуса павильона как данные (s10f-1): один микро-кит на сетке 2м —
/// лаборатория из 4 боксов (арена, клетки, хирургия, отдых) + ангар-Gateway в кольце.
/// Раскладка детерминирована сидом (угол ангара + мелкие варианты), строения —
/// boxes (фасад — плейсер s10f-2 по паттерну бункера). Сцена не тронута.
/// </summary>
public static class DomeFacilityLayout
{
    public enum FacMesh { Slab, Bars }
    public enum FacMat { Concrete, Dark, Glass, Rust, Void }

    public struct FacPart
    {
        public FacMesh mesh;
        public Vector3 size;
        public Vector3 pos;
        public float eulerY;
        public FacMat mat;
        public bool collider;
    }

    public struct Poi
    {
        public string id;
        public Vector2 pos;
        public float radius;
    }

    public class Facility
    {
        public readonly List<FacPart> parts = new List<FacPart>();
        public readonly List<Poi> pois = new List<Poi>();
        public Vector2 labCenter;
        public Vector2 hangarPos;
        public float hangarYaw;
    }

    static void Box(List<FacPart> parts, FacMesh mesh, float sx, float sy, float sz,
        float x, float y, float z, FacMat mat, bool collider, float eulerY = 0f)
    {
        parts.Add(new FacPart
        {
            mesh = mesh,
            size = new Vector3(sx, sy, sz),
            pos = new Vector3(x, y, z),
            eulerY = eulerY,
            mat = mat,
            collider = collider,
        });
    }

    static void Room(List<FacPart> parts, float cx, float cz, float w, float d,
        bool roof, FacMat wallMat)
    {
        const float h = 3f;
        Box(parts, FacMesh.Slab, w, 0.2f, d, cx, 0.1f, cz, FacMat.Concrete, false); // пол
        Box(parts, FacMesh.Slab, w, h, 0.2f, cx, h / 2, cz - d / 2, wallMat, true); // север
        Box(parts, FacMesh.Slab, w, h, 0.2f, cx, h / 2, cz + d / 2, wallMat, true); // юг
        Box(parts, FacMesh.Slab, 0.2f, h, d, cx - w / 2, h / 2, cz, wallMat, true); // запад
        // Восток — с проёмом 2м по центру: 2 сегмента + перемычка.
        float seg = (d - 2f) / 2;
        Box(parts, FacMesh.Slab, 0.2f, h, seg, cx + w / 2, h / 2, cz - d / 2 + seg / 2, wallMat, true);
        Box(parts, FacMesh.Slab, 0.2f, h, seg, cx + w / 2, h / 2, cz + d / 2 - seg / 2, wallMat, true);
        Box(parts, FacMesh.Slab, 0.2f, 0.8f, 2f, cx + w / 2, h - 0.4f, cz, wallMat, false);
        if (roof) Box(parts, FacMesh.Slab, w + 0.4f, 0.2f, d + 0.4f, cx, h + 0.1f, cz, FacMat.Dark, false);
    }

    public static Facility Build(DomeGenConfigSO cfg, long seed, Vector2 labCenter)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var f = new Facility { labCenter = labCenter };
        var parts = f.parts;
        float lx = labCenter.x, lz = labCenter.y;
        // Двор 12×12.
        Box(parts, FacMesh.Slab, 12f, 0.2f, 12f, lx, 0.1f, lz, FacMat.Concrete, false);
        // Север — арена открытая: пол 16×12 + 6 столбов (крыши нет — небо видно).
        Box(parts, FacMesh.Slab, 16f, 0.2f, 12f, lx, 0.1f, lz + 12f, FacMat.Concrete, false);
        for (int i = 0; i < 6; i++)
        {
            float px = lx - 7f + i * 2.8f;
            Box(parts, FacMesh.Slab, 0.4f, 3.5f, 0.4f, px, 1.75f, lz + 17.5f, FacMat.Dark, true);
        }
        // Восток — клетки: 3 бокса 4×3 с решёткой спереди.
        for (int i = 0; i < 3; i++)
        {
            float cz = lz - 5f + i * 5f;
            Room(parts, lx + 11f, cz, 6f, 4f, true, FacMat.Concrete);
            Box(parts, FacMesh.Bars, 0.15f, 2.4f, 3.6f, lx + 8.2f, 1.3f, cz, FacMat.Rust, true);
        }
        // Юг — хирургия (стол + стекло) и отдых (нары).
        Room(parts, lx - 5f, lz - 11f, 8f, 6f, true, FacMat.Concrete);
        Box(parts, FacMesh.Slab, 2.4f, 0.15f, 1.2f, lx - 5f, 1f, lz - 11f, FacMat.Dark, true); // стол
        Box(parts, FacMesh.Slab, 0.6f, 1f, 0.6f, lx - 5f, 0.5f, lz - 11f, FacMat.Dark, false); // тумба
        Box(parts, FacMesh.Slab, 3f, 1.8f, 0.1f, lx - 5f, 1.6f, lz - 8.2f, FacMat.Glass, false); // окно
        Room(parts, lx + 5f, lz - 11f, 8f, 6f, true, FacMat.Concrete);
        Box(parts, FacMesh.Slab, 2f, 0.5f, 1f, lx + 3.5f, 0.45f, lz - 11f, FacMat.Dark, true); // нары 1
        Box(parts, FacMesh.Slab, 2f, 0.5f, 1f, lx + 6.5f, 0.45f, lz - 11f, FacMat.Dark, true); // нары 2
        // Запад — ворота во двор (2 пилона).
        Box(parts, FacMesh.Slab, 0.6f, 3.5f, 0.6f, lx - 7f, 1.75f, lz - 1.5f, FacMat.Rust, true);
        Box(parts, FacMesh.Slab, 0.6f, 3.5f, 0.6f, lx - 7f, 1.75f, lz + 1.5f, FacMat.Rust, true);

        f.pois.Add(new Poi { id = "Lab", pos = labCenter, radius = 25f });
        f.pois.Add(new Poi { id = "Arena", pos = new Vector2(lx, lz + 12f), radius = 8f });
        f.pois.Add(new Poi { id = "Cells", pos = new Vector2(lx + 11f, lz), radius = 6f });
        f.pois.Add(new Poi { id = "Surgery", pos = new Vector2(lx - 5f, lz - 11f), radius = 6f });
        f.pois.Add(new Poi { id = "Rest", pos = new Vector2(lx + 7f, lz - 12f), radius = 5f });

        // Ангар-Gateway в кольце: угол от сида, лицом в центр.
        float angle = SeededHash.ToFloat01(SeededHash.Hash(seed, 900, 1)) * Mathf.PI * 2f;
        float r = cfg.mapDiameter * 0.5f + 40f;
        Vector2 hp = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        f.hangarPos = hp;
        f.hangarYaw = Mathf.Atan2(-hp.x, -hp.y) * Mathf.Rad2Deg;
        float hx = hp.x, hz = hp.y;
        float yaw = f.hangarYaw * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)); // в центр
        Vector2 side = new Vector2(fwd.y, -fwd.x);
        void HangarBox(FacMesh mesh, float sx, float sy, float sz, Vector2 off, float up, FacMat mat, bool col)
        {
            Vector2 p = hp + side * off.x + fwd * off.y;
            parts.Add(new FacPart
            {
                mesh = mesh, size = new Vector3(sx, sy, sz),
                pos = new Vector3(p.x, up, p.y), eulerY = f.hangarYaw, mat = mat, collider = col,
            });
        }
        HangarBox(FacMesh.Slab, 1.5f, 8f, 1.5f, new Vector2(-6f, 0f), 4f, FacMat.Concrete, true); // пилон Л
        HangarBox(FacMesh.Slab, 1.5f, 8f, 1.5f, new Vector2(6f, 0f), 4f, FacMat.Concrete, true); // пилон П
        HangarBox(FacMesh.Slab, 14f, 1.5f, 2f, new Vector2(0f, 0f), 8.5f, FacMat.Dark, false); // перемычка
        HangarBox(FacMesh.Slab, 10f, 6.5f, 0.3f, new Vector2(0f, -1f), 3.25f, FacMat.Void, false); // шторка
        HangarBox(FacMesh.Slab, 20f, 0.2f, 20f, new Vector2(0f, 8f), 0.1f, FacMat.Concrete, false); // площадка
        f.pois.Add(new Poi { id = "Hangar", pos = hp + fwd * 8f, radius = 12f });
        return f;
    }
}
