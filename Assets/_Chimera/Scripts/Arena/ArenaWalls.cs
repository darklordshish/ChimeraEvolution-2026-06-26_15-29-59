using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Строит арену целиком на старте: пол + 4 стены по периметру + рыхлый лабиринт внутри,
/// затем РАНТАЙМ-БЕЙК NavMesh по этой геометрии (волки маршрутят вокруг стен).
/// Лабиринт «рыхлый» (широкие проходы + лишние дыры) — укрытия и разрывы видимости,
/// но открытости хватает, чтобы стая окружала. Повесь на пустой объект в центре арены.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class ArenaWalls : MonoBehaviour
{
    [Header("Арена")]
    [SerializeField] float arenaSide = 100f;  // сторона арены (×4 площади от прежних 50)
    [SerializeField] float wallHeight = 8f;  // повыше: даёт змее вертикаль-убежище (climb) + меньше «вида поверх»
    [SerializeField] float wallThickness = 1f;
    [SerializeField] bool buildFloor = true;  // строить свой пол (отключи старый пол в сцене)
    [SerializeField] Color floorColor = new Color(0.34f, 0.33f, 0.31f); // приглушённый грунт
    [SerializeField] Color wallColor = new Color(0.24f, 0.24f, 0.27f);  // тёмный камень

    [Header("Лабиринт")]
    [SerializeField] int mazeCells = 4;         // сетка N×N (широкие ячейки → широкие коридоры)
    [SerializeField] float mazeFill = 0.7f;     // доля арены под лабиринт (остальное — кольцо у стен)
    [SerializeField, Range(0f, 1f)] float openness = 0.5f; // сколько лишних стен убрать (loops/открытость)
    [SerializeField] float centerClear = 9f;    // радиус чистого «кармана» под спавн игрока в центре
    [SerializeField] int seed = 12345;          // фикс. сид → стабильный уровень (поменяй для другой раскладки)

    void Awake()
    {
        if (buildFloor)
            CreateBox("Floor", new Vector3(0f, -3f, 0f), new Vector3(arenaSide, 6f, arenaSide), floorColor); // толстый — не продавить

        BuildPerimeter();
        BuildMaze();

        // рантайм-бейк навмеша по всей собранной геометрии
        var surface = GetComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();
    }

    void BuildPerimeter()
    {
        float half = arenaSide * 0.5f;
        float len = arenaSide + wallThickness;
        CreateBox("Wall_N", new Vector3(0f, wallHeight * 0.5f,  half), new Vector3(len, wallHeight, wallThickness), wallColor);
        CreateBox("Wall_S", new Vector3(0f, wallHeight * 0.5f, -half), new Vector3(len, wallHeight, wallThickness), wallColor);
        CreateBox("Wall_E", new Vector3( half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, len), wallColor);
        CreateBox("Wall_W", new Vector3(-half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, len), wallColor);
    }

    void BuildMaze()
    {
        int n = Mathf.Max(2, mazeCells);
        float mazeSize = arenaSide * mazeFill;
        float cell = mazeSize / n;
        float origin = -mazeSize * 0.5f; // левый/нижний край сетки

        Random.InitState(seed);

        // внутренние стены: east[x,z] — между (x,z) и (x+1,z); north[x,z] — между (x,z) и (x,z+1)
        bool[,] east = new bool[n, n];
        bool[,] north = new bool[n, n];
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                if (x < n - 1) east[x, z] = true;
                if (z < n - 1) north[x, z] = true;
            }

        // карвим связный лабиринт (DFS-бэктрекер) — гарантирует, что всё достижимо
        bool[,] visited = new bool[n, n];
        var stack = new System.Collections.Generic.Stack<Vector2Int>();
        var cur = new Vector2Int(0, 0);
        visited[0, 0] = true;
        stack.Push(cur);
        var dirs = new Vector2Int[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        while (stack.Count > 0)
        {
            cur = stack.Peek();
            // неисследованные соседи
            var free = new System.Collections.Generic.List<Vector2Int>();
            foreach (var d in dirs)
            {
                var nb = cur + d;
                if (nb.x >= 0 && nb.x < n && nb.y >= 0 && nb.y < n && !visited[nb.x, nb.y]) free.Add(d);
            }
            if (free.Count == 0) { stack.Pop(); continue; }
            var dir = free[Random.Range(0, free.Count)];
            var next = cur + dir;
            // убрать стену между cur и next
            if (dir.x == 1) east[cur.x, cur.y] = false;
            else if (dir.x == -1) east[next.x, next.y] = false;
            else if (dir.y == 1) north[cur.x, cur.y] = false;
            else north[next.x, next.y] = false;
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // лишние дыры — открытость/петли
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                if (east[x, z] && Random.value < openness) east[x, z] = false;
                if (north[x, z] && Random.value < openness) north[x, z] = false;
            }

        // материализуем оставшиеся стены (с чистым карманом в центре под спавн)
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                if (east[x, z])
                {
                    var p = new Vector3(origin + (x + 1) * cell, wallHeight * 0.5f, origin + (z + 0.5f) * cell);
                    if (new Vector2(p.x, p.z).magnitude > centerClear)
                        CreateBox($"Maze_E_{x}_{z}", p, new Vector3(wallThickness, wallHeight, cell + wallThickness), wallColor);
                }
                if (north[x, z])
                {
                    var p = new Vector3(origin + (x + 0.5f) * cell, wallHeight * 0.5f, origin + (z + 1) * cell);
                    if (new Vector2(p.x, p.z).magnitude > centerClear)
                        CreateBox($"Maze_N_{x}_{z}", p, new Vector3(cell + wallThickness, wallHeight, wallThickness), wallColor);
                }
            }
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void CreateBox(string boxName, Vector3 localPos, Vector3 scale, Color color)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); // MeshRenderer + BoxCollider
        box.name = boxName;
        box.transform.SetParent(transform, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = scale;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(BaseColorId, color);
        box.GetComponent<Renderer>().SetPropertyBlock(mpb); // явный цвет вместо бирюзового дефолта
    }
}
