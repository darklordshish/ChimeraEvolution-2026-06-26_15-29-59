using UnityEngine;

/// <summary>
/// Простая follow-камера от третьего лица: едет за целью с фиксированным оффсетом и смотрит на неё.
/// Временное решение, чтобы видеть тело в движении. Позже заменим на Cinemachine + систему восприятия.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 6f, -7f); // над и позади цели
    [SerializeField] float followLerp = 10f;                    // плавность следования
    [SerializeField] float lookHeight = 1.2f;                   // куда смотреть — чуть выше пивота

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
