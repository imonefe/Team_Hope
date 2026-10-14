using UnityEngine;

public class HealObject : MonoBehaviour
{
    [Header("회복량")]
    [Range(0f, 1f)]
    public float healPercent = 0.3f; // 최대 HP의 30%

    [Header("애니메이션")]
    public Animator animator;
    public string healTrigger = "Heal";

    [Header("콜라이더")]
    public Collider2D healCollider;

    [Header("힐 이펙트 (선택 — 이 자리에 고정)")]
    public GameObject healEffectPrefab;

    [Header("고정 회복량 오버라이드 (0 이하면 미사용 — 위 healPercent 대신 이 값 사용)")]
    [Tooltip("금속 속성 드랍처럼 %가 아닌 고정 HP량으로 회복시켜야 할 때 코드에서 SetFixedHeal()로 설정")]
    public int fixedHealAmount = 0;

    [Header("소멸")]
    [Tooltip("켜면 먹은 뒤 오브젝트를 실제로 Destroy함. 꺼두면 스프라이트는 그대로 남고 콜라이더만 비활성화됨(재사용 방지)")]
    public bool destroyAfterUse = false;
    [Tooltip("destroyAfterUse가 켜져 있을 때만 사용 — 사라지기까지 대기 시간(초). 힐 애니메이션/이펙트가 보일 시간을 준다")]
    public float destroyDelay = 0.5f;

    bool used = false;

    public void SetFixedHeal(int amount)
    {
        fixedHealAmount = amount;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        Health health = other.GetComponent<Health>();
        Stats stats = other.GetComponent<Stats>();

        if (health == null || stats == null) return;

        used = true;

        // 최대 HP의 n% 회복 (캐릭터 힐 이펙트 포함) — 고정 회복량이 설정되어 있으면 그걸 우선 사용
        int healAmount = fixedHealAmount > 0
            ? fixedHealAmount
            : Mathf.RoundToInt(stats.maxHp * healPercent);
        stats.HealWithEffect(healAmount);

        // 콜라이더 비활성화 (중복 방지)
        if (healCollider != null)
            healCollider.enabled = false;

        // 애니메이션만 재생
        if (animator != null)
            animator.SetTrigger(healTrigger);

        // 힐 이펙트 (오브젝트 자리에 고정)
        Debug.Log($"[HealObject] healEffectPrefab = {(healEffectPrefab != null ? healEffectPrefab.name : "NULL")}");
        if (healEffectPrefab != null)
            Instantiate(healEffectPrefab, transform.position, Quaternion.identity);

        // 스프라이트는 그대로 남기고 콜라이더만 꺼진 채로 둔다 (요청에 따라 기본은 미제거).
        // destroyAfterUse를 켜면 예전처럼 잠시 뒤 오브젝트 자체를 없앤다.
        if (destroyAfterUse)
            Destroy(gameObject, destroyDelay);
    }
}