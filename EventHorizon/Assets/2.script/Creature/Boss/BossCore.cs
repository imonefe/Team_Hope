using UnityEngine;

// [2026-11-16 추가] 보스방 코어 (기획서: 코어가 하나라도 살아 있으면 보스 무적)
public enum BossCoreKind { Archer, Swordsman, Mage }   // 궁수 · 검사 · 마법사

/// <summary>
/// 보스방 코어 1개. 자식 HitBody(Enemy 레이어 + Stats + Health)가 실제로 맞는 몸체이고,
/// 한 대당 무조건 체력 1이 깎여 hitsToBreak 대 맞으면 부서진다 (HitBody는 Health 규칙대로 사라짐).
/// 일반 적이 아니므로(Enemy 컴포넌트 없음) 처치 수·재화 드랍은 없다.
/// </summary>
public class BossCore : MonoBehaviour
{
    public BossCoreKind kind = BossCoreKind.Swordsman;

    [Tooltip("부서지기까지 맞아야 하는 횟수 (기획서: 평타 6~8대)")]
    public int hitsToBreak = 7;

    [Tooltip("처음부터 부서진 상태로 시작 (전시회 빌드 등에서 일부 코어만 살릴 때)")]
    public bool startBroken = false;

    [Header("연결")]
    public Health hitBody;
    public SpriteRenderer body;
    [Tooltip("[2026-11-24] 코어 겉모습 (안개 구 FX_FogSphere_Left). 부서지면 꺼짐")]
    public GameObject visual;
    [Tooltip("[2026-11-24] 부서질 때 그 자리에 생기는 연출 (FX_CoreBreak_*). 처음부터 부서진 채 시작할 때는 안 나옴")]
    public GameObject breakFx;

    [Header("색")]
    public Color activeColor = Color.white;
    public Color brokenColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);

    bool broken;
    public bool IsActive => !broken;

    /// <summary>중간 보스 처치 기록 키 (RunManager). 1 이상이면 이 코어는 부서진 채로 시작</summary>
    public static string DefeatKey(BossCoreKind k) => "MidBoss." + k;

    void Start()
    {
        bool defeated = RunManager.Instance != null && RunManager.Instance.GetUpgradeLevel(DefeatKey(kind)) > 0;
        if (startBroken || defeated || hitBody == null) { Break(true); return; }

        Stats s = hitBody.GetComponent<Stats>();
        if (s != null) { s.maxHp = hitsToBreak; s.currentHp = hitsToBreak; }
        hitBody.damageAbsorber = d => d > 0 ? 1 : 0;   // 피해량과 상관없이 한 대 = 1
        if (body != null) body.color = activeColor;
    }

    void Update()
    {
        // HitBody가 체력 0으로 사라지면 (Unity의 null 판정) 부서진 상태로
        if (!broken && hitBody == null) Break(false);
    }

    void Break(bool silent)
    {
        broken = true;
        if (body != null) body.color = brokenColor;
        if (!silent && breakFx != null)   // [2026-11-24] 부서짐 연출 (파편 · 글자 · 상징이 흩어짐)
            Instantiate(breakFx, visual != null ? visual.transform.position : transform.position, Quaternion.identity);
        if (visual != null) visual.SetActive(false);   // [2026-11-24] 안개 구 코어는 부서지면 사라짐
        if (hitBody != null) Destroy(hitBody.gameObject);
        if (!silent) FloatingTextSpawner.SpawnText(transform.position + Vector3.up, "코어 파괴", Color.yellow);
    }
}
