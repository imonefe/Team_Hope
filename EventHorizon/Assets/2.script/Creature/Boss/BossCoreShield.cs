using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 보스방에 1개. 방 안의 보스(BossController)를 찾아
/// "살아 있는 코어가 하나라도 있으면 무적"으로 연결한다. 보스는 나중에 생성되므로 0.5초마다 다시 찾는다.
/// </summary>
public class BossCoreShield : MonoBehaviour
{
    public BossCore[] cores;

    float scanTimer;

    void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = 0.5f;

        foreach (BossController boss in FindObjectsOfType<BossController>())
        {
            Health h = boss.GetComponent<Health>();
            if (h != null && h.invulnerable == null) h.invulnerable = AnyCoreActive;
        }
    }

    public bool AnyCoreActive()
    {
        if (cores == null) return false;
        foreach (BossCore c in cores)
            if (c != null && c.IsActive) return true;
        return false;
    }
}
