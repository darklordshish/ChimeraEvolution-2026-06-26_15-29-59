using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Мост «данные → сцена»: строит меш террейна из WorldHeightField и печёт NavMesh.
/// Ничего не хранит: построенное сносится Clear(), сцена после работы чистая
/// (как стенд Полигона — второй источник правды запрещён).
/// Паттерн запекания — как у арены (NavMeshSurface, collectObjects=Children, sync BuildNavMesh).
/// Лес, слайс s1b.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class TerrainApplier : MonoBehaviour
{
    public WorldGenConfigSO config;
    public int resolution = 32;
    public float size = 60f;

    public Mesh BuiltMesh { get; private set; }
    public GameObject TerrainRoot { get; private set; }
    public NavMeshSurface Surface => GetComponent<NavMeshSurface>();
    public bool IsBaked => Surface != null && Surface.navMeshData != null;

    public void Configure(WorldGenConfigSO cfg, int quadsPerSide, float sizeMeters)
    {
        config = cfg;
        resolution = Mathf.Clamp(quadsPerSide, 4, 128);
        size = Mathf.Max(sizeMeters, 4f);
    }

    /// <summary>
    /// Фасеточная сетка resolution×resolution квадов. Вершины продублированы под грани
    /// (тот же язык, что BoneMesher.Flat); обход (p00,p11,p10)+(p00,p01,p11) даёт нормали +Y
    /// (проверено тестом: обратный порядок смотрит вниз).
    /// </summary>
    public void Build()
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        Clear();
        int res = resolution;
        float step = size / res;
        float half = size * 0.5f;
        var verts = new Vector3[res * res * 6];
        int v = 0;
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                Vector3 p00 = Point(ix, iz, step, half);
                Vector3 p10 = Point(ix + 1, iz, step, half);
                Vector3 p01 = Point(ix, iz + 1, step, half);
                Vector3 p11 = Point(ix + 1, iz + 1, step, half);
                verts[v++] = p00;
                verts[v++] = p11;
                verts[v++] = p10;
                verts[v++] = p00;
                verts[v++] = p01;
                verts[v++] = p11;
            }
        var tris = new int[verts.Length];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        var mesh = new Mesh { name = "ForestTerrain" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        BuiltMesh = mesh;

        TerrainRoot = new GameObject("Terrain");
        TerrainRoot.transform.SetParent(transform, false);
        var filter = TerrainRoot.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        TerrainRoot.AddComponent<MeshRenderer>();
        TerrainRoot.AddComponent<MeshCollider>();

        Surface.collectObjects = CollectObjects.Children;
    }

    public void BakeNavMesh()
    {
        if (TerrainRoot == null) throw new InvalidOperationException("сначала Build()");
        Surface.BuildNavMesh();
    }

    public bool TrySample(float x, float z, out Vector3 pos, float maxDistance = 2f)
    {
        var probe = new Vector3(x, WorldHeightField.SampleHeight(config, x, z) + 1f, z);
        NavMeshHit hit;
        bool ok = NavMesh.SamplePosition(probe, out hit, maxDistance, NavMesh.AllAreas);
        pos = hit.position;
        return ok;
    }

    public bool PathExists(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete;
    }

    public void Clear()
    {
        if (TerrainRoot != null)
        {
            if (Application.isPlaying) Destroy(TerrainRoot);
            else DestroyImmediate(TerrainRoot);
            TerrainRoot = null;
        }
        if (BuiltMesh != null)
        {
            if (Application.isPlaying) Destroy(BuiltMesh);
            else DestroyImmediate(BuiltMesh);
            BuiltMesh = null;
        }
        var surface = GetComponent<NavMeshSurface>();
        if (surface != null && surface.navMeshData != null)
            surface.RemoveData();
    }

    Vector3 Point(int ix, int iz, float step, float half)
    {
        float x = ix * step - half;
        float z = iz * step - half;
        return new Vector3(x, WorldHeightField.SampleHeight(config, x, z), z);
    }
}
