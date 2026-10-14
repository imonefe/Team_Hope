using UnityEngine;
using TMPro;

public class EnemyCountUI : MonoBehaviour
{
    public TMP_Text countText;
    [Tooltip("숫자 앞에 붙일 텍스트 (필요 없으면 비워둠)")]
    public string prefix = "";
    [Tooltip("갱신 주기(초) — 씬에 남아있는 Enemy/BossController 개수를 주기적으로 다시 센다")]
    public float refreshInterval = 0.3f;

    void OnEnable()
    {
        Refresh();
        InvokeRepeating(nameof(Refresh), refreshInterval, refreshInterval);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(Refresh));
    }

    void Refresh()
    {
        if (countText != null)
            countText.text = $"{prefix}{CountAliveEnemies()}";
    }

    int CountAliveEnemies()
    {
        int count = 0;

        foreach (var e in FindObjectsOfType<Enemy>())
            if (e != null && e.gameObject.activeInHierarchy)
                count++;

        foreach (var b in FindObjectsOfType<BossController>())
            if (b != null && b.gameObject.activeInHierarchy)
                count++;

        return count;
    }
}
