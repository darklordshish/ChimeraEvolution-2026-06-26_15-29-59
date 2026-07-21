using UnityEngine;

/// <summary>
/// Импульс-отбрасывание для существ на CharacterController. Пока активен — ИИ уступает управление движением.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Knockback : MonoBehaviour
{
    [SerializeField] float drag = 10f; // как быстро гаснет импульс

    CharacterController cc;
    Vector3 vel;

    public bool IsActive => vel.sqrMagnitude > 0.02f;

    void Awake() => cc = GetComponent<CharacterController>();

    public void Push(Vector3 velocity)
    {
        if (GetComponent<Massive>() != null) return; // массивную тушу не откинуть (вервольф, лось) — один механизм для всех источников
        velocity.y = 0f;
        vel = velocity;
    }

    public void Cancel() => vel = Vector3.zero; // обхват змеи (стадия 2+) гасит отлёт от пинка — сжатие держит

    void Update()
    {
        if (vel.sqrMagnitude < 0.02f) { vel = Vector3.zero; return; }
        cc.Move(vel * Time.deltaTime);
        vel = Vector3.MoveTowards(vel, Vector3.zero, drag * Time.deltaTime);
    }
}
