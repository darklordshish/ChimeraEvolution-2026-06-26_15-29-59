using UnityEngine;

/// <summary>
/// ЛЕТЯЩАЯ ИГЛА — первый снаряд в проекте (shared-инфра дальнего боя). Несёт `MeleeBlow`-паёк, летит по
/// прямой, бьёт ПЕРВОЕ тело на пути и гаснет. Замедление стаками (кит ежа: осыпал → добыча увязла → догнал).
///
/// САМ СЕБЯ СТРОИТ (`Spawn`): доставке не нужен префаб-ассет — тонкий вытянутый куб + триггер-коллайдер
/// собираются в рантайме. Так же, как психики до-создают компоненты: у снаряда нет тюнящихся полей в
/// инспекторе, только числа от доставки.
///
/// ЛУЧ, А НЕ ФИЗИКА: движемся сами и `Physics.Raycast` по шагу — так тонкая быстрая игла не проскакивает
/// сквозь цель между кадрами (классическая беда триггеров у мелких быстрых снарядов).
/// </summary>
public class Quill : MonoBehaviour
{
    Health owner;       // кто выпустил — себя не задеть, атрибуция убийства ему
    MeleeBlow blow;     // паёк: урон + Bleed + Slow (числа от доставки)
    float damageMult;   // мощь стрелка на момент выстрела (ярость/разброс)
    Vector3 vel;
    float dieAt;
    float radius;       // толщина луча — попадание засчитывается, если тело в этом радиусе от линии шага

    static readonly Color QuillColor = new(0.85f, 0.82f, 0.78f); // костяная игла

    public static void Spawn(Vector3 pos, Vector3 dir, float speed, float range, float hitRadius,
                             Health owner, in MeleeBlow blow, float damageMult)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Quill";
        Destroy(go.GetComponent<Collider>()); // столкновения считаем лучом сами, коллайдер не нужен
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(dir);
        go.transform.localScale = new Vector3(0.06f, 0.06f, 0.5f); // тонкая длинная игла

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(Shader.PropertyToID("_BaseColor"), QuillColor);
        go.GetComponent<Renderer>().SetPropertyBlock(mpb);

        var q = go.AddComponent<Quill>();
        q.owner = owner;
        q.blow = blow;
        q.damageMult = damageMult;
        q.vel = dir.normalized * speed;
        q.radius = hitRadius;
        q.dieAt = Time.time + range / Mathf.Max(0.01f, speed); // долетел до предельной дальности — гаснет
    }

    void Update()
    {
        float step = vel.magnitude * Time.deltaTime;
        Vector3 from = transform.position;

        // ПОПАДАНИЕ ЛУЧОМ: первое тело в пределах radius от отрезка шага (SphereCast, не Raycast — игла
        // не нулевой толщины). Стены/препятствия глушат иглу — за угол не залетает
        if (Physics.SphereCast(from, radius, vel.normalized, out var hit, step, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.collider.GetComponentInParent<Health>();
            if (target != null && !ReferenceEquals(target, owner)) // не в стрелка
            {
                blow.Deliver(new Hit(owner, from), target, damageMult);
                Destroy(gameObject);
                return;
            }
            if (target == null) { Destroy(gameObject); return; } // врезалась в стену
        }

        transform.position = from + vel * Time.deltaTime;
        if (Time.time >= dieAt) Destroy(gameObject);
    }
}
