using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Раскладка бункера-лаборатории из примитивов кита флоры (дублей winding нет —
/// меши берёт у FloraMeshKit). Комплекс: портал (пилоны + перемычка + тёмная ниша),
/// стены, крыша под углом, вент-гриб (марка «подземное дышит»), ржавая дверь акцентом,
/// мох-пятна, обломки кольцом, ростовая веха 1.8м (масштаб). Детерминирован сидом.
/// Лес, слайс s9.
/// </summary>
public static class BunkerKit
{
    public enum PartMesh { Slab, Trunk }
    public enum PartMat { Concrete, Dark, Void, Moss, Rust }

    public struct BunkerPart
    {
        public PartMesh mesh;
        public Vector3 size; // Slab: габариты; Trunk: (r0, r1, h)
        public Vector3 pos; // локально, центр комплекса в нуле, портал смотрит +Z
        public Vector3 euler;
        public PartMat mat;
        public bool collider;
    }

    public static List<BunkerPart> Complex(long seed)
    {
        var parts = new List<BunkerPart>();
        // портал: пилоны + перемычка + тёмная ниша позади
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(0.5f, 2.6f, 0.5f), pos = new Vector3(-1.6f, 0f, 0f), mat = PartMat.Concrete, collider = true });
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(0.5f, 2.6f, 0.5f), pos = new Vector3(1.6f, 0f, 0f), mat = PartMat.Concrete, collider = true });
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(3.7f, 0.5f, 0.6f), pos = new Vector3(0f, 2.85f, 0f), mat = PartMat.Concrete, collider = false });
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(2.6f, 2.6f, 0.2f), pos = new Vector3(0f, 0f, -0.4f), mat = PartMat.Void, collider = false });
        // стены по бокам с джиттером
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(2.5f, 2.2f, 0.4f), pos = new Vector3(-3.2f, 0f, -0.5f), euler = new Vector3(0f, 8f, 0f), mat = PartMat.Concrete, collider = true });
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(2.5f, 2.2f, 0.4f), pos = new Vector3(3.2f, 0f, -0.5f), euler = new Vector3(0f, -6f, 0f), mat = PartMat.Concrete, collider = true });
        // рухнувшая крыша под углом
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(4.5f, 0.35f, 3f), pos = new Vector3(0f, 2.4f, -0.8f), euler = new Vector3(-12f, 3f, 0f), mat = PartMat.Dark, collider = false });
        // вент-гриб: труба + колпак
        parts.Add(new BunkerPart { mesh = PartMesh.Trunk, size = new Vector3(0.2f, 0.2f, 1.5f), pos = new Vector3(2.8f, 0f, -1.5f), mat = PartMat.Concrete, collider = true });
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(0.7f, 0.15f, 0.7f), pos = new Vector3(2.8f, 1.55f, -1.5f), mat = PartMat.Dark, collider = false });
        // ржавая дверь прислонена
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(0.9f, 1.9f, 0.12f), pos = new Vector3(1.1f, 0f, 0.8f), euler = new Vector3(-8f, -25f, 0f), mat = PartMat.Rust, collider = true });
        // мох-пятна на стенах
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(0.4f, 0.05f, 0.3f), pos = new Vector3(-3.2f, 1.2f, -0.28f), mat = PartMat.Moss, collider = false });
        parts.Add(new BunkerPart { mesh = PartMesh.Slab, size = new Vector3(0.3f, 0.05f, 0.5f), pos = new Vector3(3.2f, 0.8f, -0.28f), mat = PartMat.Moss, collider = false });
        // ростовая веха 1.8м (масштаб рядом с порталом)
        parts.Add(new BunkerPart { mesh = PartMesh.Trunk, size = new Vector3(0.03f, 0.03f, 1.8f), pos = new Vector3(2.6f, 0f, 1.2f), mat = PartMat.Dark, collider = false });
        // обломки кольцом 4–9м
        for (int i = 0; i < 8; i++)
        {
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 100, i)) * Mathf.PI * 2f;
            float d = 4f + SeededHash.ToFloat01(SeededHash.Hash(seed, 101, i)) * 5f;
            float s = 0.3f + SeededHash.ToFloat01(SeededHash.Hash(seed, 102, i)) * 0.4f;
            parts.Add(new BunkerPart
            {
                mesh = PartMesh.Slab,
                size = new Vector3(s, s * 0.6f, s),
                pos = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d),
                euler = new Vector3(0f, SeededHash.ToFloat01(SeededHash.Hash(seed, 103, i)) * 90f, 0f),
                mat = (i % 3 == 0) ? PartMat.Dark : PartMat.Concrete,
                collider = true
            });
        }
        return parts;
    }
}
