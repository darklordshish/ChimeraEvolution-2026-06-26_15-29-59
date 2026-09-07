using UnityEngine;

/// <summary>
/// Челюсть Ф3: пасть замкнута своей клеткой 4×6, челюсть — кость "челюсть" (ветвь→челюсть),
/// зубы — FEATURE-детали (клык_в/клык_н) на ней. Открывается поворотом кости.
/// Приём Bite открывает на windup, закрывает на Done/Cancelled.
/// </summary>
public class JawController : MonoBehaviour
{
    Transform jaw; // кость "челюсть"
    Quaternion closedRot;
    Quaternion openRot;
    float t;
    [SerializeField] float openAngle = 28f; // градусов, ~ как на фото открытой пасти
    [SerializeField] float openSpeed = 12f;
    [SerializeField] float closeSpeed = 18f;

    bool wantsOpen;

    void Awake()
    {
        // Кость ищется после MorphBuilder/BoneMesher — те зовутся в CreatureBody.Recompute
        // Поэтому ленивый Find в первом Update, а не в Awake
    }

    public void Rebind()
    {
        jaw = null;
        // Ищем трансформ кости челюсти внутри Skeleton
        var skel = transform.Find("Morph/Skeleton");
        if (skel == null) skel = GetComponentInChildren<Transform>(); // fallback brute
        jaw = FindRecursive(transform, "челюсть");
        if (jaw != null)
        {
            closedRot = jaw.localRotation;
            openRot = closedRot * Quaternion.AngleAxis(openAngle, Vector3.right);
        }
    }

    static Transform FindRecursive(Transform root, string name)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            if (tr.name == name || tr.name == name + ".L" || tr.name.StartsWith(name))
                return tr;
        return null;
    }

    public void SetOpen(bool open) => wantsOpen = open;

    void Update()
    {
        if (jaw == null)
        {
            // пробуем перепривязать раз в секунду
            if (Time.frameCount % 60 == 0) Rebind();
            return;
        }
        float target = wantsOpen ? 1f : 0f;
        float speed = wantsOpen ? openSpeed : closeSpeed;
        t = Mathf.MoveTowards(t, target, Time.deltaTime * speed * 0.1f * 10f);
        jaw.localRotation = Quaternion.Slerp(closedRot, openRot, t);
    }
}
