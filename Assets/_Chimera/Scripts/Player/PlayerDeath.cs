using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Смерть игрока: лог + перезапуск сцены (временный «game over» для отладки).
/// Health на игроке держим с выключенным Destroy On Death — рестарт делаем здесь.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
    [SerializeField] float restartDelay = 1.5f;

    void Awake() => GetComponent<Health>().onDeath.AddListener(OnDeath);

    void OnDeath()
    {
        Time.timeScale = 1f; // на случай, если смерть совпала с хитстопом

        // отключаем управление и атаку, чтобы «труп» не бегал
        if (TryGetComponent<PlayerController>(out var pc)) pc.enabled = false;
        if (TryGetComponent<PlayerAttack>(out var pa)) pa.enabled = false;

        StartCoroutine(Restart());
    }

    IEnumerator Restart()
    {
        yield return new WaitForSecondsRealtime(restartDelay); // реальное время, не зависит от timeScale
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
