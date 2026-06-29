using UnityEngine;

/// <summary>
/// Строит 4 стены по периметру квадратной арены на старте.
/// Повесь на пустой объект в центре арены, задай size (сторону) и высоту.
/// Стены — обычные коллайдеры, CharacterController игрока и волков о них останавливается.
/// </summary>
public class ArenaWalls : MonoBehaviour
{
    [SerializeField] float size = 50f;        // сторона арены (по полу)
    [SerializeField] float wallHeight = 4f;
    [SerializeField] float wallThickness = 1f;
    [SerializeField] bool visible = true;     // показывать стены (иначе только невидимые коллайдеры)

    void Awake()
    {
        float half = size * 0.5f;
        float len = size + wallThickness; // нахлёст в углах, чтобы не было щелей

        CreateWall("Wall_N", new Vector3(0,  wallHeight * 0.5f,  half), new Vector3(len, wallHeight, wallThickness));
        CreateWall("Wall_S", new Vector3(0,  wallHeight * 0.5f, -half), new Vector3(len, wallHeight, wallThickness));
        CreateWall("Wall_E", new Vector3( half, wallHeight * 0.5f, 0),  new Vector3(wallThickness, wallHeight, len));
        CreateWall("Wall_W", new Vector3(-half, wallHeight * 0.5f, 0),  new Vector3(wallThickness, wallHeight, len));
    }

    void CreateWall(string wallName, Vector3 localPos, Vector3 scale)
    {
        GameObject wall;
        if (visible)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); // уже с BoxCollider + рендером
        }
        else
        {
            wall = new GameObject(wallName);
            wall.AddComponent<BoxCollider>();
        }

        wall.name = wallName;
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = scale;
    }
}
