using UnityEngine;

/// <summary>
/// Follow-камера от третьего лица + лёгкая тряска (juice). Временная, до Cinemachine.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 6f, -7f); // над и позади цели
    [SerializeField] float followLerp = 10f;
    [SerializeField] float lookHeight = 1.2f;

    Vector3 followPos;                       // позиция без тряски
    float shakeTimer, shakeDuration, shakeMag;

    void Awake() => followPos = transform.position;

    /// <summary>Тряхнуть камеру (зовётся из боя при попадании).</summary>
    public void Shake(float duration = 0.12f, float magnitude = 0.3f)
    {
        shakeDuration = duration;
        shakeMag = magnitude;
        shakeTimer = duration;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        followPos = Vector3.Lerp(followPos, desired, followLerp * Time.deltaTime);

        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime; // живёт даже во время хитстопа
            float k = Mathf.Clamp01(shakeTimer / shakeDuration);
            shakeOffset = Random.insideUnitSphere * (shakeMag * k);
        }

        transform.position = followPos + shakeOffset;
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
